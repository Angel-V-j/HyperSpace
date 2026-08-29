using System;
using System.Collections.Generic;
using System.Diagnostics;
using HyperSpace.Mathematics;

namespace HyperSpace.Physics;

/// <summary>
/// Deterministic random-cloud generator with true 4D overlap rejection.
/// </summary>
public sealed class NBodyGenerator4D
{
    private const int MaximumPlacementAttemptsPerBody = 256;

    private readonly SpatialHashGrid4D _grid = new();
    private readonly List<int> _neighbors = [];

    public NBodyGenerationResult4D Generate(NBodyGenerationSettings4D settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var stopwatch = Stopwatch.StartNew();
        var random = new DeterministicRandom4D(settings.Seed);
        var bodies = new List<PhysicsBodyInitialState4D>(settings.BodyCount);
        var maximumRadius = AggregationCollisionSystem4D.RadiusFromMass(
            settings.MaximumMass,
            settings.RadiusScale);
        _grid.Reset(Math.Max(2.0 * maximumRadius, 1e-6));

        var rejectedAttempts = 0;
        for (var index = 0; index < settings.BodyCount; index++)
        {
            var mass = random.NextDouble(settings.MinimumMass, settings.MaximumMass);
            var radius = AggregationCollisionSystem4D.RadiusFromMass(mass, settings.RadiusScale);
            var foundPosition = false;
            var position = Vector4D.Zero;
            for (var attempt = 0; attempt < MaximumPlacementAttemptsPerBody; attempt++)
            {
                position = RandomPosition(random, settings.PositionHalfRanges);
                if (!OverlapsExisting(position, radius, bodies))
                {
                    foundPosition = true;
                    break;
                }

                rejectedAttempts++;
            }

            if (!foundPosition)
            {
                throw new InvalidOperationException(
                    $"Could not place body {index + 1:N0} without a 4D overlap after " +
                    $"{MaximumPlacementAttemptsPerBody} attempts. Increase the spawn ranges or reduce radius scale/count.");
            }

            var velocity = RandomVelocity(random, settings.MinimumSpeed, settings.MaximumSpeed);
            bodies.Add(new PhysicsBodyInitialState4D(position, velocity, mass, radius));
            _grid.Add(index, position);
        }

        stopwatch.Stop();
        return new NBodyGenerationResult4D(bodies, rejectedAttempts, stopwatch.Elapsed.TotalMilliseconds);
    }

    private bool OverlapsExisting(
        Vector4D position,
        double radius,
        IReadOnlyList<PhysicsBodyInitialState4D> bodies)
    {
        _grid.CollectNeighborIndices(position, _neighbors);
        foreach (var index in _neighbors)
        {
            var other = bodies[index];
            var radiusSum = radius + other.Radius;
            if ((position - other.Position).LengthSquared < radiusSum * radiusSum)
            {
                return true;
            }
        }

        return false;
    }

    private static Vector4D RandomPosition(DeterministicRandom4D random, Vector4D ranges) =>
        new(
            random.NextDouble(-ranges.X, ranges.X),
            random.NextDouble(-ranges.Y, ranges.Y),
            random.NextDouble(-ranges.Z, ranges.Z),
            random.NextDouble(-ranges.W, ranges.W));

    private static Vector4D RandomVelocity(
        DeterministicRandom4D random,
        double minimumSpeed,
        double maximumSpeed)
    {
        // Four independent normal components normalized to S^3 give an isotropic 4D direction.
        var u1 = Math.Max(random.NextUnitDouble(), 1e-15);
        var u2 = random.NextUnitDouble();
        var u3 = Math.Max(random.NextUnitDouble(), 1e-15);
        var u4 = random.NextUnitDouble();
        var radius1 = Math.Sqrt(-2.0 * Math.Log(u1));
        var radius2 = Math.Sqrt(-2.0 * Math.Log(u3));
        var angle1 = 2.0 * Math.PI * u2;
        var angle2 = 2.0 * Math.PI * u4;
        var direction = new Vector4D(
            radius1 * Math.Cos(angle1),
            radius1 * Math.Sin(angle1),
            radius2 * Math.Cos(angle2),
            radius2 * Math.Sin(angle2));
        var inverseLength = 1.0 / direction.Length;
        var speed = random.NextDouble(minimumSpeed, maximumSpeed);
        return direction * (inverseLength * speed);
    }
}
