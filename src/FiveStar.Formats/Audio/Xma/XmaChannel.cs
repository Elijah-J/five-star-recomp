// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

namespace FiveStar.Formats.Audio.Xma;

/// <summary>
/// The decode state of one channel: its subframe tiling in the current frame, scale factors (kept across subframes),
/// and the sample buffer the inverse transforms overlap into.
/// </summary>
internal sealed class XmaChannel
{
    /// <summary>
    /// Where a frame's first subframe writes its transform output: half a frame in, so the half of the previous
    /// frame's last block that overlaps it sits in front.
    /// </summary>
    public const int FrameStart = XmaLayout.FrameSamples / 2;

    /// <summary>Gets the samples: the frame's output is [0, 512); [512, 768) carries into the next frame.</summary>
    public float[] Samples { get; } = new float[XmaLayout.FrameSamples + FrameStart];

    /// <summary>Gets the subframe lengths of the current frame.</summary>
    public int[] SubframeLengths { get; } = new int[XmaLayout.MaxSubframes];

    public int SubframeCount { get; set; }

    public int CurrentSubframe { get; set; }

    /// <summary>Gets or sets the samples of the current frame decoded so far.</summary>
    public int DecodedSamples { get; set; }

    /// <summary>Gets or sets the length of the last decoded block, which sets the next overlap.</summary>
    public int PreviousBlockLength { get; set; } = XmaLayout.FrameSamples;

    /// <summary>Gets or sets where in <see cref="Samples"/> the current subframe's coefficients and output go.</summary>
    public int BlockOffset { get; set; }

    public bool CodesCoefficients { get; set; }

    public int QuantStep { get; set; }

    /// <summary>Gets or sets the count of vector-coded coefficients before run-level coding may start.</summary>
    public int VectorCoefficients { get; set; }

    /// <summary>Gets or sets a value indicating whether earlier scale factors of this frame can be carried over.</summary>
    public bool HasScaleFactors { get; set; }

    public int ScaleFactorStep { get; set; }

    public int MaxScaleFactor { get; set; }

    /// <summary>Gets the last transmitted scale factors and the ones resampled from them (two buffers that swap).</summary>
    public int[][] ScaleFactorSets { get; } = [new int[XmaLayout.MaxBands], new int[XmaLayout.MaxBands]];

    /// <summary>Gets or sets which of <see cref="ScaleFactorSets"/> holds the last transmitted values.</summary>
    public int TransmittedSet { get; set; }

    /// <summary>Gets or sets the block size the transmitted scale factors belong to.</summary>
    public int TransmittedBlock { get; set; }

    /// <summary>Gets or sets the scale factors of the current subframe (one of <see cref="ScaleFactorSets"/>).</summary>
    public int[] ScaleFactors { get; set; } = [];

    /// <summary>Clears everything a lost or broken frame could leave behind.</summary>
    public void Reset()
    {
        Array.Clear(Samples);
        PreviousBlockLength = XmaLayout.FrameSamples;
        HasScaleFactors = false;
        SubframeCount = 0;
    }
}
