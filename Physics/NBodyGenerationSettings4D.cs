using System;
using System.Globalization;
using HyperSpace.Mathematics;

namespace HyperSpace.Physics;

public sealed class NBodyGenerationSettings4D
{
    public const int MinimumBodyCount = 2;
    public const int MaximumBodyCount = PhysicsWorld4D.MaximumBodyCount;

    public int BodyCount { get; private set; } = 500;
    public int Seed { get; private set; } = 1337;
    public Vector4D PositionHalfRanges { get; private set; } = new(10.0, 10.0, 10.0, 10.0);
    public double MinimumSpeed { get; private set; }
    public double MaximumSpeed { get; private set; } = 1.0;
    public double MinimumMass { get; private set; } = 1.0;
    public double MaximumMass { get; private set; } = 10.0;
    public double RadiusScale { get; private set; } = 0.08;
    public double PointScale { get; private set; } = 1.0;

    public bool TryApplyBodyCount(string text, out bool wasClamped)
    {
        wasClamped = false;
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return false;
        }

        var clamped = Math.Clamp(parsed, MinimumBodyCount, MaximumBodyCount);
        wasClamped = parsed != clamped;
        BodyCount = clamped;
        return true;
    }

    public bool TryApplySeed(string text)
    {
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return false;
        }

        Seed = parsed;
        return true;
    }

    public void SetRandomSeed() => Seed = Random.Shared.Next(int.MinValue, int.MaxValue);

    public void AdjustPositionHalfRange(int axis, double delta)
    {
        var x = PositionHalfRanges.X;
        var y = PositionHalfRanges.Y;
        var z = PositionHalfRanges.Z;
        var w = PositionHalfRanges.W;
        switch (axis)
        {
            case 0: x = ClampRange(x + delta); break;
            case 1: y = ClampRange(y + delta); break;
            case 2: z = ClampRange(z + delta); break;
            case 3: w = ClampRange(w + delta); break;
            default: throw new ArgumentOutOfRangeException(nameof(axis));
        }

        PositionHalfRanges = new Vector4D(x, y, z, w);
    }

    public void AdjustMinimumSpeed(double delta) =>
        MinimumSpeed = Math.Clamp(MinimumSpeed + delta, 0.0, MaximumSpeed);

    public void AdjustMaximumSpeed(double delta) =>
        MaximumSpeed = Math.Clamp(MaximumSpeed + delta, MinimumSpeed, 20.0);

    public void AdjustMinimumMass(double delta) =>
        MinimumMass = Math.Clamp(MinimumMass + delta, 0.1, MaximumMass);

    public void AdjustMaximumMass(double delta) =>
        MaximumMass = Math.Clamp(MaximumMass + delta, MinimumMass, 10_000.0);

    public void AdjustRadiusScale(double delta) =>
        RadiusScale = Math.Clamp(RadiusScale + delta, 0.01, 0.5);

    public void AdjustPointScale(double delta) =>
        PointScale = Math.Clamp(PointScale + delta, 0.25, 4.0);

    private static double ClampRange(double value) => Math.Clamp(value, 0.5, 100.0);
}
