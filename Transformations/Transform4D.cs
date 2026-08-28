using System;
using HyperSpace.Mathematics;

namespace HyperSpace.Transformations;

/// <summary>
/// The uniform scale, orientation and position of an object in the 4D world.
/// </summary>
public sealed class Transform4D
{
    // These bounds prevent repeated UI scaling from reaching zero or infinity.
    public const double MinimumScale = 0.05;
    public const double MaximumScale = 20.0;

    public Vector4D Position { get; private set; } = Vector4D.Zero;

    public Rotation4D Rotation { get; private set; } = Rotation4D.Identity;

    public double Scale { get; private set; } = 1.0;

    /// <summary>
    /// Applies the uniform 4D scale sI, then the 4D rotation, then translation.
    /// Every spatial coordinate, including W, is scaled by the same factor.
    /// </summary>
    public Vector4D TransformPoint(Vector4D localPoint) =>
        Rotation.Apply(localPoint * Scale) + Position;

    public void Rotate(RotationPlane4D plane, double deltaRadians)
    {
        Rotation = Rotation.WithAddedAngle(plane, deltaRadians);
    }

    public void MoveWorld(Vector4D worldOffset)
    {
        Position += worldOffset;
    }

    public void MultiplyScale(double factor)
    {
        if (!double.IsFinite(factor) || factor <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(factor), "Scale factor must be finite and positive.");
        }

        Scale = Math.Clamp(Scale * factor, MinimumScale, MaximumScale);
    }

    public void Reset()
    {
        Position = Vector4D.Zero;
        Rotation = Rotation4D.Identity;
        Scale = 1.0;
    }
}
