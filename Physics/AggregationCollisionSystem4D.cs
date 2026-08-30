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
    private readonly List<(int FirstIndex, int SecondIndex)> _candidatePairs = [];
    private readonly CollisionPairSorter _pairSorter = new();
    // Collision-only SoA snapshot: repeated candidate checks avoid pointer chasing
    // through body objects. Serial merges update this snapshot immediately.
    private double[] _positionX = [];
    private double[] _positionY = [];
    private double[] _positionZ = [];
    private double[] _positionW = [];
    private double[] _radii = [];
    private bool[] _eligible = [];
    private (double MaximumRadius, int Count)[] _workerBounds = [];

    public double RadiusScale { get; private set; } = 0.08;
    public int LastCandidatePairCount { get; private set; }

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
        LastCandidatePairCount = 0;
        if (bodies.Count < 2)
        {
            return 0;
        }

        var collisionDetectionStartedAt = performance?.BeginPhase() ?? 0L;
        var collisionGridStartedAt = performance?.BeginPhase() ?? 0L;
        var (maximumRadius, eligibleBodyCount) = CaptureCollisionState(bodies);

        if (eligibleBodyCount < 2)
        {
            performance?.EndPhase(PerformancePhase.CollisionGrid, collisionGridStartedAt);
            performance?.EndPhase(
                PerformancePhase.CollisionDetection,
                collisionDetectionStartedAt);
            return 0;
        }

        _grid.Build(bodies, _eligible, Math.Max(2.0 * maximumRadius, 1e-6));
        performance?.EndPhase(PerformancePhase.CollisionGrid, collisionGridStartedAt);

        var collisionCount = 0;
        long candidateCount = 0;
        var candidatesStartedAt = performance?.BeginPhase() ?? 0L;
        _grid.CollectCandidatePairs(_candidatePairs);
        LastCandidatePairCount = _candidatePairs.Count;
        performance?.RecordParallelWork(_grid.LastCandidateWorkerCount);
        performance?.EndPhase(PerformancePhase.CollisionCandidates, candidatesStartedAt);
        var sortStartedAt = performance?.BeginPhase() ?? 0L;
        _pairSorter.Sort(_candidatePairs, bodies.Count);
        performance?.EndPhase(PerformancePhase.CollisionSort, sortStartedAt);
        var resolutionStartedAt = performance?.BeginPhase() ?? 0L;
        foreach (var candidate in _candidatePairs)
        {
            if (!_eligible[candidate.FirstIndex])
            {
                continue;
            }

            if (!_eligible[candidate.SecondIndex])
            {
                continue;
            }

            candidateCount++;
            if (!AreOverlapping(candidate.FirstIndex, candidate.SecondIndex))
            {
                continue;
            }

            performance?.EndPhase(PerformancePhase.CollisionResolution, resolutionStartedAt);
            performance?.EndPhase(
                PerformancePhase.CollisionDetection,
                collisionDetectionStartedAt);
            var aggregationStartedAt = performance?.BeginPhase() ?? 0L;
            var first = bodies[candidate.FirstIndex];
            var second = bodies[candidate.SecondIndex];
            var (survivor, absorbed) = Merge(first, second);
            var survivorIndex = ReferenceEquals(survivor, first) ? candidate.FirstIndex : candidate.SecondIndex;
            var absorbedIndex = survivorIndex == candidate.FirstIndex ? candidate.SecondIndex : candidate.FirstIndex;
            CaptureBody(survivorIndex, survivor);
            _eligible[absorbedIndex] = false;
            if (ReferenceEquals(selectedSurvivor, absorbed))
            {
                selectedSurvivor = survivor;
            }
            collisionCount++;
            performance?.EndPhase(PerformancePhase.Aggregation, aggregationStartedAt);
            collisionDetectionStartedAt = performance?.BeginPhase() ?? 0L;
            resolutionStartedAt = performance?.BeginPhase() ?? 0L;
        }

        performance?.EndPhase(PerformancePhase.CollisionResolution, resolutionStartedAt);
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

    private bool AreOverlapping(int first, int second)
    {
        var radiusSum = _radii[first] + _radii[second];
        var dx = _positionX[second] - _positionX[first];
        var dy = _positionY[second] - _positionY[first];
        var dz = _positionZ[second] - _positionZ[first];
        var dw = _positionW[second] - _positionW[first];
        return dx * dx + dy * dy + dz * dz + dw * dw <= radiusSum * radiusSum;
    }

    private (double MaximumRadius, int Count) CaptureCollisionState(IReadOnlyList<PhysicsBody4D> bodies)
    {
        if (_eligible.Length < bodies.Count)
        {
            var capacity = Math.Max(bodies.Count, _eligible.Length * 2);
            _positionX = new double[capacity];
            _positionY = new double[capacity];
            _positionZ = new double[capacity];
            _positionW = new double[capacity];
            _radii = new double[capacity];
            _eligible = new bool[capacity];
        }
        var workers = ParallelWork.WorkerCountFor(bodies.Count, 2_048);
        if (_workerBounds.Length < workers) Array.Resize(ref _workerBounds, workers);
        ParallelWork.ForRanges(bodies.Count, 2_048, (worker, start, end) =>
        {
            var maximum = 0.0;
            var count = 0;
            for (var index = start; index < end; index++)
            {
                var body = bodies[index];
                CaptureBody(index, body);
                if (_eligible[index])
                {
                    maximum = Math.Max(maximum, body.Radius);
                    count++;
                }
            }
            _workerBounds[worker] = (maximum, count);
        });
        var maximumRadius = 0.0;
        var eligibleCount = 0;
        for (var worker = 0; worker < workers; worker++)
        {
            maximumRadius = Math.Max(maximumRadius, _workerBounds[worker].MaximumRadius);
            eligibleCount += _workerBounds[worker].Count;
        }
        return (maximumRadius, eligibleCount);
    }

    private void CaptureBody(int index, PhysicsBody4D body)
    {
        _positionX[index] = body.Position.X;
        _positionY[index] = body.Position.Y;
        _positionZ[index] = body.Position.Z;
        _positionW[index] = body.Position.W;
        _radii[index] = body.Radius;
        _eligible[index] = body.IsAlive && !body.IsStatic;
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
