using System;
using HyperSpace.Mathematics;

namespace HyperSpace.Physics;

/// <summary>
/// An oriented 4D hyperplane n dot p + d = 0. Its positive half-space is valid.
/// </summary>
public readonly record struct Hyperplane4D
{
    public Hyperplane4D(Vector4D normal, double offset)
    {
        if (!normal.IsFinite || !double.IsFinite(offset) || normal.LengthSquared <= 1e-24)
        {
            throw new ArgumentOutOfRangeException(
                nameof(normal),
                "A hyperplane needs a finite, non-zero normal and finite offset.");
        }

        var inverseLength = 1.0 / normal.Length;
        Normal = normal * inverseLength;
        Offset = offset * inverseLength;
    }

    public static Hyperplane4D WZero => new(new Vector4D(0.0, 0.0, 0.0, 1.0), 0.0);

    public Vector4D Normal { get; }

    public double Offset { get; }

    public double SignedDistance(Vector4D point) => Vector4D.Dot(Normal, point) + Offset;

    /// <summary>
    /// Projects penetration back to the boundary, then reflects only the inward
    /// normal velocity. Tangential velocity in the other three directions is unchanged.
    /// </summary>
    public bool ResolveCollision(PhysicsBody4D body, double restitution)
    {
        if (!double.IsFinite(restitution) || restitution < 0.0 || restitution > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(restitution));
        }

        var distance = SignedDistance(body.Position);
        if (!double.IsFinite(distance) || distance >= 0.0)
        {
            return false;
        }

        body.Position -= Normal * distance;
        var normalVelocity = Vector4D.Dot(body.Velocity, Normal);
        if (normalVelocity < 0.0)
        {
            body.Velocity -= Normal * ((1.0 + restitution) * normalVelocity);
        }

        return true;
    }
}
