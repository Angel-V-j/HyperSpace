using System;
using System.Collections.Generic;
using HyperSpace.Diagnostics;
using HyperSpace.Mathematics;

namespace HyperSpace.Physics;

/// <summary>
/// Pairwise Newtonian-like central gravity in four spatial dimensions.
/// Force magnitude falls as 1/r^3, so vector acceleration uses r-vector/r^4.
/// </summary>
public sealed class GravitySystem4D
{
    public const double DefaultGravitationalConstant = 0.05;
    public const double DefaultSoftening = 0.25;

    public double GravitationalConstant { get; private set; } = DefaultGravitationalConstant;

    public double Softening { get; private set; } = DefaultSoftening;
    public double MeanFieldRadius { get; private set; }
    public int LastWorkerCount { get; private set; } = 1;

    public void SetGravitationalConstant(double value)
    {
        if (!double.IsFinite(value) || value < 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "G must be finite and non-negative.");
        }

        GravitationalConstant = value;
    }

    public void SetSoftening(double value)
    {
        if (!double.IsFinite(value) || value <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Softening must be finite and positive.");
        }

        Softening = value;
    }

    public void ConfigureMeanField(IReadOnlyList<PhysicsBody4D> bodies)
    {
        var totalMass = 0.0;
        var weightedPosition = Vector4D.Zero;
        foreach (var body in bodies)
        {
            if (!body.IsAlive) continue;
            totalMass += body.Mass;
            weightedPosition += body.Position * body.Mass;
        }

        if (totalMass <= 0.0)
        {
            MeanFieldRadius = 0.0;
            return;
        }

        var center = weightedPosition * (1.0 / totalMass);
        var weightedRadiusSquared = 0.0;
        foreach (var body in bodies)
        {
            if (body.IsAlive)
            {
                weightedRadiusSquared += body.Mass * (body.Position - center).LengthSquared;
            }
        }
        MeanFieldRadius = Math.Sqrt(weightedRadiusSquared / totalMass);
    }

    /// <summary>
    /// Adds all O(N^2) pair contributions to the supplied acceleration buffer.
    /// For a pair, a1=G*m2*r/(r^2+epsilon^2)^2 and a2 is the opposite mass-weighted term.
    /// This approaches the exact 4D inverse-cube field as r becomes large compared with epsilon.
    /// </summary>
    public void AccumulatePairwise(
        IReadOnlyList<PhysicsBody4D> bodies,
        Vector4D[] accelerations)
    {
        LastWorkerCount = 1;
        if (accelerations.Length < bodies.Count)
        {
            throw new ArgumentException("The acceleration buffer is too small.", nameof(accelerations));
        }

        if (GravitationalConstant == 0.0)
        {
            return;
        }

        var softeningSquared = Softening * Softening;
        LastWorkerCount = bodies.Count >= 256
            ? ParallelWork.WorkerCountFor(bodies.Count, 64)
            : 1;
        if (LastWorkerCount > 1)
        {
            AccumulatePairwiseByTarget(bodies, accelerations, softeningSquared);
            return;
        }
        for (var firstIndex = 0; firstIndex < bodies.Count - 1; firstIndex++)
        {
            var first = bodies[firstIndex];
            for (var secondIndex = firstIndex + 1; secondIndex < bodies.Count; secondIndex++)
            {
                var second = bodies[secondIndex];
                if (first.IsStatic && second.IsStatic)
                {
                    continue;
                }

                var displacement = second.Position - first.Position;
                if (!TryInverseEffectiveDistanceFourth(
                    displacement,
                    softeningSquared,
                    out var inverseEffectiveDistanceFourth))
                {
                    continue;
                }

                var commonFactor = GravitationalConstant * inverseEffectiveDistanceFourth;
                if (!first.IsStatic)
                {
                    accelerations[firstIndex] += displacement * (commonFactor * second.Mass);
                }

                if (!second.IsStatic)
                {
                    accelerations[secondIndex] -= displacement * (commonFactor * first.Mass);
                }
            }
        }
    }

    private void AccumulatePairwiseByTarget(
        IReadOnlyList<PhysicsBody4D> bodies,
        Vector4D[] accelerations,
        double softeningSquared)
    {
        // Each target repeats pair-distance work, but owns its acceleration and
        // visits source indices in exactly the original accumulation order.
        // No shared reductions or floating-point reorderings are introduced.
        ParallelWork.ForRanges(bodies.Count, 64, (_, start, end) =>
        {
            for (var targetIndex = start; targetIndex < end; targetIndex++)
            {
                var target = bodies[targetIndex];
                if (target.IsStatic) continue;
                var acceleration = accelerations[targetIndex];
                for (var sourceIndex = 0; sourceIndex < bodies.Count; sourceIndex++)
                {
                    if (sourceIndex == targetIndex) continue;
                    var source = bodies[sourceIndex];
                    var displacement = sourceIndex < targetIndex
                        ? target.Position - source.Position
                        : source.Position - target.Position;
                    if (!TryInverseEffectiveDistanceFourth(displacement, softeningSquared, out var inverseFourth))
                        continue;
                    var commonFactor = GravitationalConstant * inverseFourth;
                    var contribution = displacement * (commonFactor * source.Mass);
                    acceleration = sourceIndex < targetIndex
                        ? acceleration - contribution
                        : acceleration + contribution;
                }
                accelerations[targetIndex] = acceleration;
            }
        });
    }

    /// <summary>
    /// O(N) conservative mean-field approximation. The generated cloud is
    /// represented by a smooth mass distribution centered on its global COM.
    /// A uniform correction term is the COM derivative of the shared potential;
    /// it makes the internal forces sum to zero instead of injecting momentum.
    /// </summary>
    public void AccumulateMeanField(
        IReadOnlyList<PhysicsBody4D> bodies,
        Vector4D[] accelerations)
    {
        LastWorkerCount = 1;
        if (accelerations.Length < bodies.Count)
        {
            throw new ArgumentException("The acceleration buffer is too small.", nameof(accelerations));
        }

        if (GravitationalConstant == 0.0 || bodies.Count < 2)
        {
            return;
        }

        var totalMass = 0.0;
        var weightedPosition = Vector4D.Zero;
        foreach (var body in bodies)
        {
            if (!body.IsAlive)
            {
                continue;
            }

            totalMass += body.Mass;
            weightedPosition += body.Position * body.Mass;
        }

        var centerOfMass = weightedPosition * (1.0 / totalMass);
        var effectiveSofteningSquared =
            (Softening * Softening) + (MeanFieldRadius * MeanFieldRadius);
        var correction = Vector4D.Zero;
        foreach (var body in bodies)
        {
            if (!body.IsAlive) continue;
            var offset = body.Position - centerOfMass;
            var effectiveDistanceSquared = offset.LengthSquared + effectiveSofteningSquared;
            correction += offset *
                (body.Mass / (effectiveDistanceSquared * effectiveDistanceSquared));
        }
        correction *= GravitationalConstant;

        var capturedTotalMass = totalMass;
        var capturedCenterOfMass = centerOfMass;
        var capturedEffectiveSofteningSquared = effectiveSofteningSquared;
        var capturedCorrection = correction;
        LastWorkerCount = ParallelWork.WorkerCountFor(bodies.Count, 2_048);
        ParallelWork.ForRanges(
            bodies.Count,
            minimumItemsPerWorker: 2_048,
            (_, start, end) =>
            {
                for (var index = start; index < end; index++)
                {
                    var body = bodies[index];
                    if (!body.IsAlive || body.IsStatic)
                    {
                        continue;
                    }

                    var offset = body.Position - capturedCenterOfMass;
                    var effectiveDistanceSquared =
                        offset.LengthSquared + capturedEffectiveSofteningSquared;
                    var inverseEffectiveDistanceFourth =
                        1.0 / (effectiveDistanceSquared * effectiveDistanceSquared);
                    accelerations[index] +=
                        offset * (-GravitationalConstant * capturedTotalMass * inverseEffectiveDistanceFourth) +
                        capturedCorrection;
                }
            });
    }

    public Vector4D AccelerationToward(
        Vector4D targetPosition,
        Vector4D sourcePosition,
        double sourceMass) =>
        CalculateAcceleration(
            targetPosition,
            sourcePosition,
            sourceMass,
            GravitationalConstant,
            Softening);

    /// <summary>
    /// Potential whose negative gradient produces
    /// a = G*m*r/(r^2+epsilon^2)^2.
    /// </summary>
    public double PairPotentialEnergy(PhysicsBody4D first, PhysicsBody4D second)
    {
        var effectiveDistanceSquared =
            (second.Position - first.Position).LengthSquared + (Softening * Softening);
        if (!double.IsFinite(effectiveDistanceSquared) || effectiveDistanceSquared <= 0.0)
        {
            return 0.0;
        }

        return -GravitationalConstant * first.Mass * second.Mass /
            (2.0 * effectiveDistanceSquared);
    }

    public double CalculateExactPotentialEnergy(IReadOnlyList<PhysicsBody4D> bodies)
    {
        var potential = 0.0;
        for (var firstIndex = 0; firstIndex < bodies.Count - 1; firstIndex++)
        {
            var first = bodies[firstIndex];
            if (!first.IsAlive) continue;
            for (var secondIndex = firstIndex + 1; secondIndex < bodies.Count; secondIndex++)
            {
                var second = bodies[secondIndex];
                if (second.IsAlive)
                {
                    potential += PairPotentialEnergy(first, second);
                }
            }
        }
        return potential;
    }

    /// <summary>
    /// Shared potential whose gradient is the conservative mean-field force.
    /// </summary>
    public double CalculateMeanFieldPotentialEnergy(IReadOnlyList<PhysicsBody4D> bodies)
    {
        var totalMass = 0.0;
        var weightedPosition = Vector4D.Zero;
        foreach (var body in bodies)
        {
            if (!body.IsAlive) continue;
            totalMass += body.Mass;
            weightedPosition += body.Position * body.Mass;
        }

        if (totalMass <= 0.0) return 0.0;
        var center = weightedPosition * (1.0 / totalMass);
        var effectiveSofteningSquared =
            (Softening * Softening) + (MeanFieldRadius * MeanFieldRadius);
        var reciprocalSum = 0.0;
        foreach (var body in bodies)
        {
            if (!body.IsAlive) continue;
            var effectiveDistanceSquared =
                (body.Position - center).LengthSquared + effectiveSofteningSquared;
            reciprocalSum += body.Mass / effectiveDistanceSquared;
        }
        return -0.5 * GravitationalConstant * totalMass * reciprocalSum;
    }

    public static Vector4D CalculateAcceleration(
        Vector4D targetPosition,
        Vector4D sourcePosition,
        double sourceMass,
        double gravitationalConstant,
        double softening)
    {
        if (!targetPosition.IsFinite || !sourcePosition.IsFinite ||
            !double.IsFinite(sourceMass) || sourceMass <= 0.0 ||
            !double.IsFinite(gravitationalConstant) || gravitationalConstant < 0.0 ||
            !double.IsFinite(softening) || softening <= 0.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sourceMass),
                "Gravity inputs must be finite, with positive mass/softening and non-negative G.");
        }

        if (gravitationalConstant == 0.0)
        {
            return Vector4D.Zero;
        }

        var displacement = sourcePosition - targetPosition;
        if (!TryInverseEffectiveDistanceFourth(
            displacement,
            softening * softening,
            out var inverseEffectiveDistanceFourth))
        {
            return Vector4D.Zero;
        }

        var factor = gravitationalConstant * sourceMass * inverseEffectiveDistanceFourth;
        return double.IsFinite(factor) ? displacement * factor : Vector4D.Zero;
    }

    private static bool TryInverseEffectiveDistanceFourth(
        Vector4D displacement,
        double softeningSquared,
        out double inverseEffectiveDistanceFourth)
    {
        var distanceSquared = displacement.LengthSquared;
        var effectiveDistanceSquared = distanceSquared + softeningSquared;
        var effectiveDistanceFourth = effectiveDistanceSquared * effectiveDistanceSquared;
        if (!displacement.IsFinite || !double.IsFinite(effectiveDistanceFourth) ||
            effectiveDistanceFourth <= 0.0)
        {
            inverseEffectiveDistanceFourth = 0.0;
            return false;
        }

        inverseEffectiveDistanceFourth = 1.0 / effectiveDistanceFourth;
        return double.IsFinite(inverseEffectiveDistanceFourth);
    }
}
