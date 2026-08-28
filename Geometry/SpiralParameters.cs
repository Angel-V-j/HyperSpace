using System;

namespace HyperSpace.Geometry;

/// <summary>
/// Immutable parameters for the sampled 4D dual-circle spiral.
/// </summary>
public sealed record SpiralParameters(
    double R1,
    double R2,
    double K,
    int SampleCount,
    double TStart,
    double TEnd)
{
    // A non-integer frequency keeps P(0) and P(4pi) distinct. K=2 would
    // retrace a closed curve twice and hide START beneath END.
    public static SpiralParameters Default => new(
        R1: 1.0,
        R2: 0.5,
        K: 2.25,
        SampleCount: 600,
        TStart: 0.0,
        TEnd: 4.0 * Math.PI);

    public void Validate()
    {
        if (!double.IsFinite(R1) || R1 <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(R1), "R1 must be finite and positive.");
        }

        if (!double.IsFinite(R2) || R2 <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(R2), "R2 must be finite and positive.");
        }

        if (!double.IsFinite(K) || Math.Abs(K) < 0.05)
        {
            throw new ArgumentOutOfRangeException(nameof(K), "K must be finite and non-zero.");
        }

        if (SampleCount is < 2 or > 5000)
        {
            throw new ArgumentOutOfRangeException(nameof(SampleCount), "Use between 2 and 5000 samples.");
        }

        if (!double.IsFinite(TStart) || !double.IsFinite(TEnd) || TEnd <= TStart)
        {
            throw new ArgumentOutOfRangeException(nameof(TEnd), "TEnd must be finite and greater than TStart.");
        }
    }
}
