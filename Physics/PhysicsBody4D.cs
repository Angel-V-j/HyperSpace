using System;
using HyperSpace.Mathematics;

namespace HyperSpace.Physics;

/// <summary>
/// Minimal translational state for one point-like body in four spatial dimensions.
/// </summary>
public sealed class PhysicsBody4D
{
    public PhysicsBody4D(
        int id,
        Vector4D position,
        Vector4D velocity,
        double mass = 1.0,
        bool isStatic = false,
        double radius = 0.05)
    {
        if (!position.IsFinite || !velocity.IsFinite)
        {
            throw new ArgumentOutOfRangeException(
                nameof(position),
                "Position and velocity must be finite.");
        }

        if (!double.IsFinite(mass) || mass <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(mass), "Mass must be finite and positive.");
        }

        if (!double.IsFinite(radius) || radius <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(radius), "Radius must be finite and positive.");
        }

        Id = id;
        Position = position;
        Velocity = velocity;
        Mass = mass;
        IsStatic = isStatic;
        Radius = radius;
    }

    public int Id { get; }

    public Vector4D Position { get; internal set; }

    public Vector4D Velocity { get; internal set; }

    public Vector4D Acceleration { get; internal set; }

    public double Mass { get; internal set; }

    public double Radius { get; internal set; }

    public bool IsAlive { get; internal set; } = true;

    public bool IsStatic { get; }

    public double KineticEnergy => 0.5 * Mass * Velocity.LengthSquared;

    /// <summary>
    /// Semi-implicit Euler: update velocity first, then position with new velocity.
    /// X, Y, Z, and W follow the identical equation.
    /// </summary>
    public void Integrate(double deltaTime)
    {
        if (!double.IsFinite(deltaTime) || deltaTime < 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(deltaTime));
        }

        if (IsStatic || !IsAlive)
        {
            Acceleration = Vector4D.Zero;
            return;
        }

        Velocity += Acceleration * deltaTime;
        Position += Velocity * deltaTime;
    }
}
