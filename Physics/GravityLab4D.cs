using System;
using System.Linq;
using HyperSpace.Mathematics;

namespace HyperSpace.Physics;

/// <summary>
/// Small resettable two-body experiment built on PhysicsWorld4D.
/// It owns initial conditions and an unprojected 4D trajectory, not rendering state.
/// </summary>
public sealed class GravityLab4D : IDisposable
{
    public const double DefaultCentralMass = 1000.0;
    public const double OrbiterMass = 1.0;
    public const double LowVelocity = 1.20;
    public const double MediumVelocity = 1.75;
    public const double HighVelocity = 2.30;
    public const double DefaultAdditionalWVelocity = 0.75;

    private readonly PhysicsWorld4D _world;

    public GravityLab4D(PhysicsWorld4D world)
    {
        _world = world;
        _world.FixedStepCompleted += RecordTrailPoint;
    }

    public double CentralMass { get; private set; } = DefaultCentralMass;

    public Vector4D CentralPosition { get; } = Vector4D.Zero;

    public Vector4D OrbiterInitialPosition { get; private set; } = new(4.0, 0.0, 0.0, 0.0);

    public Vector4D OrbiterInitialVelocity { get; private set; } = new(0.0, MediumVelocity, 0.0, 0.0);

    public PhysicsBody4D? CentralBody { get; private set; }

    public PhysicsBody4D? Orbiter { get; private set; }

    public Trajectory4D Trail { get; } = new();

    public bool HasExperiment =>
        CentralBody is not null &&
        Orbiter is not null &&
        _world.Bodies.Any(body => ReferenceEquals(body, CentralBody)) &&
        _world.Bodies.Any(body => ReferenceEquals(body, Orbiter));

    public GravityLabDiagnostics4D Diagnostics
    {
        get
        {
            if (!HasExperiment)
            {
                return GravityLabDiagnostics4D.Unavailable;
            }

            var displacementTowardCentral = CentralBody!.Position - Orbiter!.Position;
            var distance = displacementTowardCentral.Length;
            var direction = distance > 1e-12
                ? displacementTowardCentral * (1.0 / distance)
                : Vector4D.Zero;
            var acceleration = _world.MutualGravityEnabled
                ? _world.GravitySystem.AccelerationToward(
                    Orbiter.Position,
                    CentralBody.Position,
                    CentralBody.Mass)
                : Vector4D.Zero;
            return new GravityLabDiagnostics4D(
                true,
                distance,
                Orbiter.Velocity.Length,
                acceleration.Length,
                direction,
                Orbiter.Position.W);
        }
    }

    public void ResetExperiment()
    {
        _world.Clear();
        _world.Pause();
        _world.SetGravity(Vector4D.Zero);
        _world.SetGravityMode(GravityMode4D.Exact);
        _world.SetMutualGravityEnabled(true);
        _world.SetAggregationEnabled(false);
        _world.SetCollisionsEnabled(false);

        CentralBody = _world.AddBody(
            CentralPosition,
            Vector4D.Zero,
            CentralMass,
            isStatic: true);
        Orbiter = _world.AddBody(
            OrbiterInitialPosition,
            OrbiterInitialVelocity,
            OrbiterMass);

        Trail.Clear();
        Trail.Append(Orbiter.Position);
    }

    public void DetachBodies()
    {
        CentralBody = null;
        Orbiter = null;
        Trail.Clear();
    }

    public void AdjustCentralMass(double delta) =>
        CentralMass = Math.Clamp(CentralMass + delta, 100.0, 5000.0);

    public void AdjustOrbiterInitialPosition(Vector4D delta) =>
        OrbiterInitialPosition = ClampComponents(OrbiterInitialPosition + delta, -20.0, 20.0);

    public void AdjustOrbiterInitialVelocity(Vector4D delta) =>
        OrbiterInitialVelocity = ClampComponents(OrbiterInitialVelocity + delta, -10.0, 10.0);

    public void SetVelocityPreset(double yVelocity)
    {
        if (!double.IsFinite(yVelocity))
        {
            throw new ArgumentOutOfRangeException(nameof(yVelocity));
        }

        OrbiterInitialVelocity = new Vector4D(0.0, yVelocity, 0.0, 0.0);
    }

    public void UseXYVelocity() =>
        OrbiterInitialVelocity = new Vector4D(0.0, OrbiterInitialVelocity.Y, 0.0, 0.0);

    public void UseXYWVelocity()
    {
        var wVelocity = Math.Abs(OrbiterInitialVelocity.W) > 1e-12
            ? OrbiterInitialVelocity.W
            : DefaultAdditionalWVelocity;
        OrbiterInitialVelocity = new Vector4D(0.0, OrbiterInitialVelocity.Y, 0.0, wVelocity);
    }

    public void AdjustTrailCapacity(int delta)
    {
        var requested = Math.Clamp(
            Trail.Capacity + delta,
            Trajectory4D.MinimumCapacity,
            Trajectory4D.MaximumCapacity);
        Trail.SetCapacity(requested);
    }

    public void ClearTrail() => Trail.Clear();

    public void Dispose() => _world.FixedStepCompleted -= RecordTrailPoint;

    private void RecordTrailPoint()
    {
        if (HasExperiment)
        {
            Trail.Append(Orbiter!.Position);
        }
    }

    private static Vector4D ClampComponents(Vector4D value, double minimum, double maximum) =>
        new(
            Math.Clamp(value.X, minimum, maximum),
            Math.Clamp(value.Y, minimum, maximum),
            Math.Clamp(value.Z, minimum, maximum),
            Math.Clamp(value.W, minimum, maximum));
}
