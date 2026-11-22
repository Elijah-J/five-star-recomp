// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using System.Text.RegularExpressions;

namespace FiveStar.Shaders.Translation.Core;

/// <summary>
/// Reads and writes operands as HLSL. Registers are uint variables; EXEC is the per-lane bool <c>active</c>; SGPRs that
/// hold lane masks are per-lane bools <c>m_sN</c>. VALU and VMEM results are written only on active lanes.
/// </summary>
internal sealed partial class OperandAccess
{
    private const int LowDword = 0;
    private const int HighDword = 1;
    private const string Halves = "xy";

    private readonly TranslationState _state;
    private readonly ImplicitInputs _inputs;

    public OperandAccess(TranslationState state, ImplicitInputs inputs)
    {
        _state = state;
        _inputs = inputs;
    }

    /// <summary>The uint value of one dword of an operand.</summary>
    public string Read(Operand operand, int component = 0)
    {
        if (operand.Kind == OperandKind.Immediate)
        {
            return Hlsl.HexUint(operand.Value);
        }

        if (operand.Kind == OperandKind.Expression)
        {
            return operand.Expression;
        }

        var key = operand.At(component);

        if (operand.Kind == OperandKind.Scalar && key == SpecialRegisters.Exec)
        {
            // EXEC lives in `active`; s_exec is only refreshed by integer writes, so rebuild the bits from the lanes.
            return SnapOne($"{WaveOps.Ballot}(active).{Halves[LowDword]}");
        }

        if (operand.Kind == OperandKind.Scalar && _state.Masks.Contains(operand.At(0)))
        {
            // A lane mask used as an integer: rebuild it with a ballot in program lane numbers. Ballots are snapped into
            // temporaries so they never run under a per-lane condition (HLSL 2021 short-circuits ?:, && and ||).
            var half = component <= HighDword ? Halves[component] : Halves[LowDword];

            return SnapOne($"{WaveOps.Ballot}({Mask(operand)}).{half}");
        }

        if (operand.Kind == OperandKind.Scalar && IsMaskHighHalf(key))
        {
            // The high dword of a wave64 lane mask named on its own (vcc_hi, exec_hi, s11 after v_cmp_e64 s[10:11]).
            return SnapOne($"{WaveOps.Ballot}({Mask(OperandParser.Sgpr(key.Index - 1))}).y");
        }

        if (!_state.Written.Contains(key))
        {
            _inputs.Load(key);
        }

        _state.Variables.Add(key);

        return key.Name;
    }

    /// <summary>The float value of an operand with its neg/abs modifiers.</summary>
    public string ReadFloat(Operand operand, int component = 0)
    {
        var value = $"asfloat({Read(operand, component)})";

        return ApplyModifiers(operand, value);
    }

    /// <summary>The int value of an operand; integer operands take no modifiers.</summary>
    public string ReadInt(Operand operand, int component = 0)
    {
        if (operand.Negate || operand.Absolute)
        {
            throw new UnsupportedProgramException("int modifier");
        }

        return $"asint({Read(operand, component)})";
    }

    /// <summary>One f16 half of an operand as float, with neg/abs.</summary>
    public string ReadHalf(Operand operand, bool high)
    {
        var value = Read(operand);
        var half = high ? $"f16tof32({value} >> 16)" : $"f16tof32({value})";

        return ApplyModifiers(operand, half);
    }

    /// <summary>Applies the neg/abs input modifiers of an operand to a float expression.</summary>
    public static string ApplyModifiers(Operand operand, string value)
    {
        if (operand.Absolute)
        {
            value = $"abs({value})";
        }

        if (operand.Negate)
        {
            value = $"(-{value})";
        }

        return value;
    }

    /// <summary>Writes one dword of a destination. Writes to null are dropped.</summary>
    public void Write(Operand destination, string value, int component = 0)
    {
        if (destination.Kind == OperandKind.Immediate)
        {
            return;
        }

        var key = destination.At(component);
        _state.Touch(key);
        _state.Masks.Remove(key);
        _state.MaskHighHalves.Remove(key);
        _state.MaskLanes.Remove(key);
        TrackConstant(key, value);

        if (key.Kind == RegisterKey.Scalar && (key.Index == SpecialRegisters.ExecLo || key.Index == SpecialRegisters.ExecHi))
        {
            WriteExecInteger(key, value);

            return;
        }

        if (key.Kind == RegisterKey.Scalar)
        {
            _state.Emit($"  {key.Name} = {value};");

            return;
        }

        WriteVector(key, value);
    }

    /// <summary>Writes the low or high 16 bits of a destination, keeping the other half.</summary>
    public void WriteHalf(Operand destination, string value16, bool high)
    {
        var current = "0u";

        if (_state.Written.Contains(destination.At(0)))
        {
            current = Read(destination);
        }

        if (high)
        {
            Write(destination, $"(({current}) & 0xffffu) | (({value16}) << 16)");
        }
        else
        {
            Write(destination, $"(({current}) & 0xffff0000u) | (({value16}) & 0xffffu)");
        }
    }

    /// <summary>Writes a lane mask (per-lane bool). Writing EXEC sets <c>active</c>.</summary>
    public void WriteMask(Operand destination, string condition)
    {
        if (destination.Kind == OperandKind.Immediate)
        {
            return;
        }

        var key = destination.At(0);

        if (key == SpecialRegisters.Exec)
        {
            _state.Emit($"  active = {condition};");

            return;
        }

        MarkMask(key);
        _state.Emit($"  {key.MaskName} = {condition};");
    }

    /// <summary>Records that a register now holds a lane mask (and, on wave64, that the next one is its high half).</summary>
    public void MarkMask(RegisterKey key)
    {
        _state.Touch(key);
        _state.Masks.Add(key);
        _state.MasksEver.Add(key);

        if (_state.IsWave64)
        {
            _state.MaskHighHalves.Add(key.Plus(1));
        }
    }

    /// <summary>True when an operand is an immediate or a register currently holding a lane mask.</summary>
    public bool IsMask(Operand operand)
    {
        if (operand.Kind == OperandKind.Immediate)
        {
            return true;
        }

        return HoldsMask(operand);
    }

    /// <summary>True when an operand is a register currently holding a lane mask.</summary>
    public bool HoldsMask(Operand operand)
    {
        var key = operand.Key;

        return key is not null && _state.Masks.Contains(key.Value);
    }

    /// <summary>This lane's bit of a mask operand as a bool expression.</summary>
    public string Mask(Operand operand)
    {
        var key = operand.Key;

        if (key is not null && key.Value == SpecialRegisters.Exec)
        {
            return "active";
        }

        if (key is not null && _state.Masks.Contains(key.Value))
        {
            return key.Value.MaskName;
        }

        if (operand.Kind == OperandKind.Immediate)
        {
            var constant = operand.Value != 0 ? "true" : "false";

            return constant;
        }

        if (operand.Kind != OperandKind.Scalar)
        {
            throw new UnsupportedProgramException($"mask read of non-mask {operand.Text}");
        }

        // An integer SGPR pair used as a lane mask (loaded or computed with scalar ALU): test this lane's bit.
        var low = Read(OperandParser.Sgpr(operand.Low));
        var high = "0u";

        if (operand.High > operand.Low)
        {
            high = Read(OperandParser.Sgpr(operand.Low + 1));
        }

        return $"lane_bit({low}, {high})";
    }

    /// <summary>Copies values into fresh temporaries so multi-dword writes cannot clobber their own inputs.</summary>
    public string[] Snap(params string[] values)
    {
        var names = new string[values.Length];

        for (var index = 0; index < values.Length; index++)
        {
            var name = "_t" + _state.NextTemporary().ToString(CultureInfo.InvariantCulture);
            _state.Emit($"  uint {name} = {values[index]};");
            names[index] = name;
        }

        return names;
    }

    /// <summary>Copies one value into a fresh temporary.</summary>
    public string SnapOne(string value)
    {
        return Snap(value)[0];
    }

    /// <summary>Copies a uint2 value (a lane-mask ballot) into a fresh temporary.</summary>
    public string SnapPair(string value)
    {
        var name = "_t" + _state.NextTemporary().ToString(CultureInfo.InvariantCulture);
        _state.Emit($"  uint2 {name} = {value};");

        return name;
    }

    public void SetScc(string condition)
    {
        _state.Emit($"  scc = {condition};");
    }

    /// <summary>
    /// The SamplerDescriptorHeap index of an S#: a literal S# built from constants gets a static-sampler slot, otherwise
    /// dword0 holds the heap index.
    /// </summary>
    public string SamplerIndex(Operand sampler)
    {
        const int SamplerDwords = 4;

        var words = new long[SamplerDwords];

        for (var dword = 0; dword < SamplerDwords; dword++)
        {
            if (!_state.Constants.TryGetValue(sampler.At(dword), out words[dword]))
            {
                return Read(sampler, 0);
            }
        }

        var slot = _state.StaticSamplers.FindIndex(existing => existing.SequenceEqual(words));

        if (slot < 0)
        {
            _state.StaticSamplers.Add(words);
            slot = _state.StaticSamplers.Count - 1;
        }

        return $"STATIC_SAMPLER_BASE + {slot}u";
    }

    private bool IsMaskHighHalf(RegisterKey key)
    {
        if (_state.MaskHighHalves.Contains(key))
        {
            return true;
        }

        return key.Index == SpecialRegisters.ExecHi && _state.IsWave64;
    }

    private void TrackConstant(RegisterKey key, string value)
    {
        var literal = HexLiteralPattern().Match(value);

        if (literal.Success)
        {
            _state.Constants[key] = long.Parse(literal.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);

            return;
        }

        foreach (var (known, constant) in _state.Constants)
        {
            if (known.Name == value)
            {
                _state.Constants[key] = constant;

                return;
            }
        }

        _state.Constants.Remove(key);
    }

    private void WriteExecInteger(RegisterKey key, string value)
    {
        // An integer write to EXEC: refresh the per-lane flag from the mask bits.
        _state.Emit($"  {key.Name} = {value};");
        _state.Variables.Add(SpecialRegisters.Exec);
        _state.Variables.Add(RegisterKey.Sgpr(SpecialRegisters.ExecHi));
        _state.Emit("  active = lane_bit(s_exec, s_exech);");
    }

    private void WriteVector(RegisterKey key, string value)
    {
        if (WaveIntrinsicPattern().IsMatch(value))
        {
            // Wave and quad intrinsics must run in uniform control flow: hoist them out of the active guard.
            value = SnapOne(value);
        }

        var keep = _state.DppKeep;

        if (keep is not null)
        {
            value = $"({keep} ? ({value}) : {key.Name})";
        }

        _state.Emit($"  if (active) {key.Name} = {value};");
    }

    [GeneratedRegex(@"^0x([0-9a-f]+)u$")]
    private static partial Regex HexLiteralPattern();

    [GeneratedRegex(@"\b(Wave|Quad|vw_)\w*\(|quad_perm\(|readfirstlane\(")]
    private static partial Regex WaveIntrinsicPattern();
}
