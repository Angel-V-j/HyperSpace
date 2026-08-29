using System;
using System.Collections.Generic;
using HyperSpace.Diagnostics;
using HyperSpace.Mathematics;

namespace HyperSpace.Physics;

/// <summary>
/// Deterministic fully inelastic point-sphere aggregation using true 4D distance.
/// </summary>
public sealed class AggregationCollisionSystem4D
{
    private readonly SpatialHashGrid4D _grid = new();
    private readonly List<int> _neighbors = [];

    public double RadiusScale { get; private set; } = 0.08;

    public void SetRadiusScale(double value)
    {
        if (!double.IsFinite(value) || value <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        RadiusScale = value;
    }

    public int Resolve(IReadOnlyList<PhysicsBody4D> bodies) =>
        Resolve(bodies, selectedBody: null, out _);

    public int Resolve(
        IReadOnlyList<PhysicsBody4D> bodies,
        PhysicsBody4D? selectedBody,
        out PhysicsBody4D? selectedSurvivor,
        PerformanceProfiler? performance = null)
    {
        selectedSurvivor = selectedBody;
        if (bodies.Count < 2)
        {
            return 0;
        }

        var collisionDetectionStartedAt = performance?.BeginPhase() ?? 0L;
        var maximumRadius = 0.0;
        foreach (var body in bodies)
        {
            if (body.IsAlive)
            {
                maximumRadius = Math.Max(maximumRadius, body.Radius);
            }
        }

        _grid.Reset(Math.Max(2.0 * maximumRadius, 1e-6));
        for (var index = 0; index < bodies.Count; index++)
        {
            if (bodies[index].IsAlive)
            {
                _grid.Add(index, bodies[index].Position);
            }
        }

        var collisionCount = 0;
        long candidateCount = 0;
        for (var firstIndex = 0; firstIndex < bodies.Count; firstIndex++)
        {
            var first = bodies[firstIndex];
            if (!first.IsAlive || first.IsStatic)
            {
                continue;
            }

            _grid.CollectNeighborIndices(first.Position, _neighbors);
            _neighbors.Sort();
            foreach (var secondIndex in _neighbors)
            {
                if (secondIndex <= firstIndex || !first.IsAlive)
                {
                    continue;
                }

                var second = bodies[secondIndex];
                if (!second.IsAlive || second.IsStatic)
                {
                    continue;
                }

                candidateCount++;
                if (!AreOverlapping(first, second))
                {
                    continue;
                }

                performance?.EndPhase(
                    PerformancePhase.CollisionDetection,
                    collisionDetectionStartedAt);
                var aggregationStartedAt = performance?.BeginPhase() ?? 0L;
                var (survivor, absorbed) = Merge(first, second);
                if (ReferenceEquals(selectedSurvivor, absorbed))
                {
                    selectedSurvivor = survivor;
                }
                collisionCount++;
                performance?.EndPhase(PerformancePhase.Aggregation, aggregationStartedAt);
                collisionDetectionStartedAt = performance?.BeginPhase() ?? 0L;
            }
        }

        performance?.EndPhase(
            PerformancePhase.CollisionDetection,
            collisionDetectionStartedAt);
        performance?.AddCollisionCandidates(candidateCount);
        performance?.AddMerges(collisionCount);
        return collisionCount;
    }

    public static double RadiusFromMass(double mass, double radiusScale)
    {
        if (!double.IsFinite(mass) || mass <= 0.0 ||
            !double.IsFinite(radiusScale) || radiusScale <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(mass));
        }

        // Four-dimensional hypersphere volume is proportional to r^4.
        return radiusScale * Math.Pow(mass, 0.25);
    }

    private static bool AreOverlapping(PhysicsBody4D first, PhysicsBody4D second)
    {
        var radiusSum = first.Radius + second.Radius;
        return (second.Position - first.Position).LengthSquared <= radiusSum * radiusSum;
    }

    private (PhysicsBody4D Survivor, PhysicsBody4D Absorbed) Merge(
        PhysicsBody4D first,
        PhysicsBody4D second)
    {
        var survivor = first.Mass > second.Mass ||
            (first.Mass == second.Mass && first.Id < second.Id)
            ? first
            : second;
        var absorbed = ReferenceEquals(survivor, first) ? second : first;
        var totalMass = survivor.Mass + absorbed.Mass;

        survivor.Position =
            ((survivor.Position * survivor.Mass) + (absorbed.Position * absorbed.Mass)) *
            (1.0 / totalMass);
        survivor.Velocity =
            ((survivor.Velocity * survivor.Mass) + (absorbed.Velocity * absorbed.Mass)) *
            (1.0 / totalMass);
        survivor.Mass = totalMass;
        survivor.Radius = RadiusFromMass(totalMass, RadiusScale);
        survivor.Acceleration = Vector4D.Zero;
        absorbed.IsAlive = false;
        absorbed.Acceleration = Vector4D.Zero;
        return (survivor, absorbed);
    }
}
