using System;
using HyperSpace.Mathematics;

namespace HyperSpace.Geometry;

/// <summary>
/// Sampling and iteration parameters for q(n+1) = q(n)^2 + C.
/// </summary>
public sealed record JuliaParameters(
    Quaternion4D Constant,
    int MaxIterations,
    double EscapeRadius,
    int Resolution,
    double MinimumCoordinate,
    double MaximumCoordinate)
{
    public const int MinimumSupportedResolution = 2;
    public const int MaximumSupportedResolution = 32;

    public static JuliaParameters Default => new(
        Preset2,
        MaxIterations: 24,
        EscapeRadius: 4.0,
        Resolution: 12,
        MinimumCoordinate: -1.5,
        MaximumCoordinate: 1.5);

    public static Quaternion4D Preset1 => Quaternion4D.Zero;

    public static Quaternion4D Preset2 => new(-0.35, 0.15, 0.10, 0.00);

    public static Quaternion4D Preset3 => new(-0.20, 0.35, -0.15, 0.10);

    public int TotalSampleCount => checked(Resolution * Resolution * Resolution * Resolution);

    public double GridSpacing =>
        (MaximumCoordinate - MinimumCoordinate) / (Resolution - 1);

    public void Validate()
    {
        if (!Constant.IsFinite)
        {
            throw new ArgumentOutOfRangeException(nameof(Constant), "Julia C must be finite.");
        }

        if (MaxIterations < 1 || MaxIterations > 4096)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaxIterations),
                "Max iterations must be between 1 and 4096.");
        }

        if (!double.IsFinite(EscapeRadius) || EscapeRadius <= 0.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(EscapeRadius),
                "Escape radius must be finite and positive.");
        }

        if (Resolution < MinimumSupportedResolution ||
            Resolution > MaximumSupportedResolution)
        {
            throw new ArgumentOutOfRangeException(
                nameof(Resolution),
                $"Resolution must be between {MinimumSupportedResolution} and {MaximumSupportedResolution}.");
        }

        if (!double.IsFinite(MinimumCoordinate) ||
            !double.IsFinite(MaximumCoordinate) ||
            MinimumCoordinate >= MaximumCoordinate)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MinimumCoordinate),
                "Sampling bounds must be finite and ordered.");
        }
    }
}
