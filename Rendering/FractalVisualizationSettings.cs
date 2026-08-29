using System;

namespace HyperSpace.Rendering;

public enum FractalColorMode
{
    WCoordinate,
    EscapeIterations
}

/// <summary>
/// Small, renderer-facing state for the Julia point cloud. The W slice is an
/// optional diagnostic filter; the normal mode always projects the full 4D set.
/// </summary>
public sealed class FractalVisualizationSettings
{
    public FractalColorMode ColorMode { get; private set; } =
        FractalColorMode.EscapeIterations;

    public bool ShowWSlice { get; private set; }

    public double SliceW { get; private set; }

    public int PointSize { get; private set; } = 1;

    public void SetColorMode(FractalColorMode mode) => ColorMode = mode;

    public void ToggleWSlice() => ShowWSlice = !ShowWSlice;

    public void AdjustSliceW(double delta, double minimum, double maximum) =>
        SliceW = Math.Clamp(SliceW + delta, minimum, maximum);

    public void CyclePointSize() => PointSize = PointSize == 3 ? 1 : PointSize + 1;

    public void Reset()
    {
        ColorMode = FractalColorMode.EscapeIterations;
        ShowWSlice = false;
        SliceW = 0.0;
        PointSize = 1;
    }
}
