// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

namespace FiveStar.Formats.Ttpf;

/// <summary>
/// A lane model byte split into its two fields: bits 5-7 a model family (0-7), bits 0-4 a parameter. The meaning of
/// both is still open; the split is what the statistics show (family 6 parameters 27-30 are the near-constant models).
/// </summary>
/// <param name="Family">Bits 5-7 of the model byte.</param>
/// <param name="Parameter">Bits 0-4 of the model byte.</param>
public readonly record struct BcPackModel(int Family, int Parameter)
{
    private const int FamilyShift = 5;
    private const int ParameterMask = 0x1F;

    public static BcPackModel FromByte(int mode)
    {
        return new BcPackModel(mode >> FamilyShift, mode & ParameterMask);
    }

    public override string ToString()
    {
        return $"{Family}.{Parameter:D2}";
    }
}
