// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using System.Text.RegularExpressions;
using FiveStar.Shaders.Disassembly;

namespace FiveStar.Shaders.Translation.Core;

/// <summary>
/// A branch whose target is out of simm16 range is compiled as
/// <c>s_getpc_b64 s[n:n+1]; s_add_u32 sn, sn, C; s_addc_u32 sn+1, sn+1, 0; s_setpc_b64 s[n:n+1]</c> (sub/subb backwards).
/// The s_setpc becomes a static <c>s_branch</c> to getpc address + 4 + C when that address is an instruction.
/// </summary>
internal static partial class LongJumpRewriter
{
    // The four-instruction sequence ends at the s_setpc; the s_getpc sits three instructions before it.
    private const int SequenceLength = 3;
    private const int AddOffset = 2;
    private const uint AddressMask = 0xffffffff;

    public static void Rewrite(List<Instruction> instructions)
    {
        for (var index = SequenceLength; index < instructions.Count; index++)
        {
            var target = LongJumpTarget(instructions, index);

            if (target is not null && instructions.Exists(instruction => instruction.Address == target.Value))
            {
                var setpc = instructions[index];
                instructions[index] = setpc with { Text = $"s_branch 0 {BranchTargets.Marker(target.Value)}" };
            }
        }
    }

    private static long? LongJumpTarget(List<Instruction> instructions, int index)
    {
        var setpc = SetPcPattern().Match(instructions[index].Text);
        var getpcInstruction = instructions[index - SequenceLength];
        var getpc = GetPcPattern().Match(getpcInstruction.Text);
        var add = AddPattern().Match(instructions[index - AddOffset].Text);
        var carry = CarryPattern().Match(instructions[index - 1].Text);

        if (!setpc.Success || !getpc.Success || !add.Success || !carry.Success)
        {
            return null;
        }

        var low = setpc.Groups["lo"].Value;
        var high = setpc.Groups["hi"].Value;
        var sameLow = low == getpc.Groups["lo"].Value && low == add.Groups["dst"].Value && low == add.Groups["src"].Value;
        var sameHigh = carry.Groups["dst"].Value == high && carry.Groups["src"].Value == high;
        var forward = add.Groups["op"].Value == "add";

        if (!sameLow || !sameHigh || forward != (carry.Groups["op"].Value == "addc"))
        {
            return null;
        }

        var distance = ParseImmediate(add.Groups["imm"].Value);
        var delta = forward ? distance : -distance;

        return (getpcInstruction.Next + delta) & AddressMask;
    }

    private static long ParseImmediate(string text)
    {
        const string HexPrefix = "0x";

        if (text.StartsWith(HexPrefix, StringComparison.Ordinal))
        {
            return long.Parse(text[HexPrefix.Length..], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }

        return long.Parse(text, CultureInfo.InvariantCulture);
    }

    [GeneratedRegex(@"^s_setpc_b64 s\[(?<lo>\d+):(?<hi>\d+)\]")]
    private static partial Regex SetPcPattern();

    [GeneratedRegex(@"^s_getpc_b64 s\[(?<lo>\d+):(?<hi>\d+)\]")]
    private static partial Regex GetPcPattern();

    [GeneratedRegex(@"^s_(?<op>add|sub)_u32 s(?<dst>\d+), s(?<src>\d+), (?<imm>0x[0-9a-f]+|-?\d+)$")]
    private static partial Regex AddPattern();

    [GeneratedRegex(@"^s_(?<op>addc|subb)_u32 s(?<dst>\d+), s(?<src>\d+), 0$")]
    private static partial Regex CarryPattern();
}
