using System;
using System.Collections.Generic;
using System.Linq;
using HyperSpace.Diagnostics;
using HyperSpace.Mathematics;

namespace HyperSpace.Physics;

/// <summary>
/// A small deterministic fixed-step world shared by the particle and gravity labs.
/// </summary>
public sealed class PhysicsWorld4D
{
    public const double DefaultFixedDeltaTime = 1.0 / 60.0;
    public const int MaximumBodyCount = 20_000;
    public const int MaximumParticleBodyCount = 500;
    public const int MaximumExactGravityBodyCount = 1_000;
    private const int MaximumStepsPerUpdate = 8;

    private static readonly double[] SupportedTimeScales = [0.1, 0.25, 0.5, 1.0, 2.0, 3.0, 4.0, 6.0, 8.0, 16.0, 32.0];

    private readonly List<PhysicsBody4D> _bodies = [];
    private Vector4D[] _accelerationBuffer = [];
    private long[] _integrationCollisionCounts = [];
    private double _accumulator;
    private double _rateElapsed;
    private int _rateSteps;
    private int _rateAggregationCollisions;
    private int _nextBodyId = 1;
    private int _spawnSequence;
    private int _timeScaleIndex = 3;
    private int _aggregationStepCounter;
    private double _initialTotalEnergy = double.NaN;
    private EnergyDiagnostics4D _cachedEnergyDiagnostics;
    private long _energyDiagnosticsStep = long.MinValue;
    private int _energyDiagnosticsStateVersion = -1;

    public PhysicsWorld4D(double fixedDeltaTime = DefaultFixedDeltaTime)
    {
        if (!double.IsFinite(fixedDeltaTime) || fixedDeltaTime <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(fixedDeltaTime));
        }

        FixedDeltaTime = fixedDeltaTime;
    }

    public IReadOnlyList<PhysicsBody4D> Bodies => _bodies;
    public PhysicsBody4D? SelectedBody { get; private set; }
    public GravitySystem4D GravitySystem { get; } = new();
    public AggregationCollisionSystem4D AggregationSystem { get; } = new();
    public PerformanceProfiler Performance { get; } = new();

    public Vector4D Gravity { get; private set; } = new(0.0, -9.8, 0.0, 0.0);
    public Hyperplane4D CollisionPlane { get; } = Hyperplane4D.WZero;
    public double FixedDeltaTime { get; }
    public double TimeScale => SupportedTimeScales[_timeScaleIndex];
    public double AccumulatedSimulationTime => _accumulator;
    public bool CatchUpLimitedLastUpdate { get; private set; }

    public double Restitution { get; private set; } = 0.8;
    public bool IsEnabled { get; private set; } = true;
    public bool IsPaused { get; private set; } = true;
    public bool CollisionsEnabled { get; private set; } = true;
    public bool MutualGravityEnabled { get; private set; }
    public bool AggregationEnabled { get; private set; }
    public GravityMode4D RequestedGravityMode { get; private set; } = GravityMode4D.Exact;

    public GravityMode4D EffectiveGravityMode =>
        RequestedGravityMode == GravityMode4D.Exact && _bodies.Count <= MaximumExactGravityBodyCount
            ? GravityMode4D.Exact
            : GravityMode4D.MeanFieldApproximate;

    public int AggregationCollisionInterval { get; private set; } = 1;
    public int StateVersion { get; private set; }
    public long CompletedStepCount { get; private set; }
    public long CollisionCount { get; private set; }
    public long AggregationCollisionCount { get; private set; }
    public int LastAggregationCollisionCount { get; private set; }
    public double LastPhysicsStepMilliseconds { get; private set; }
    public double SimulationStepsPerSecond { get; private set; }
    public double AggregationCollisionsPerSecond { get; private set; }
    public double TotalKineticEnergy => _bodies.Sum(body => body.KineticEnergy);
    public double TotalMass => _bodies.Sum(body => body.Mass);
    public Vector4D TotalMomentum => _bodies.Aggregate(
        Vector4D.Zero,
        (sum, body) => sum + (body.Velocity * body.Mass));
    public double AverageSpeed => _bodies.Count == 0
        ? 0.0
        : _bodies.Average(body => body.Velocity.Length);
    public double MaximumMass => _bodies.Count == 0 ? 0.0 : _bodies.Max(body => body.Mass);
    public double MaximumAbsoluteW => _bodies.Count == 0
        ? 0.0
        : _bodies.Max(body => Math.Abs(body.Position.W));
    public EnergyDiagnostics4D EnergyDiagnostics => GetEnergyDiagnostics();

    public event Action? FixedStepCompleted;

    public int Update(double realElapsedSeconds)
    {
        if (!double.IsFinite(realElapsedSeconds) || realElapsedSeconds < 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(realElapsedSeconds));
        }

        if (!IsEnabled || IsPaused)
        {
            return 0;
        }

        _accumulator += realElapsedSeconds * TimeScale;
        CatchUpLimitedLastUpdate = false;
        var updateStartedAt = PerformanceProfiler.Timestamp();
        var executedSteps = 0;
        var collisionsBefore = AggregationCollisionCount;
        while (_accumulator >= FixedDeltaTime && executedSteps < MaximumStepsPerUpdate)
        {
            ExecuteFixedStep();
            _accumulator -= FixedDeltaTime;
            executedSteps++;
            // Preserve every fixed step, but return control to input/rendering when
            // this update has consumed one frame's CPU budget. Debt remains visible
            // in the accumulator instead of silently dropping simulated time.
            if (PerformanceProfiler.ElapsedMillisecondsSince(updateStartedAt) >=
                DefaultFixedDeltaTime * 1000.0)
            {
                break;
            }
        }

        CatchUpLimitedLastUpdate = _accumulator >= FixedDeltaTime;

        UpdateRates(
            realElapsedSeconds,
            executedSteps,
            (int)(AggregationCollisionCount - collisionsBefore));
        return executedSteps;
    }

    public bool StepOnce()
    {
        if (!IsEnabled)
        {
            return false;
        }

        ExecuteFixedStep();
        return true;
    }

    public void Play()
    {
        IsEnabled = true;
        IsPaused = false;
    }

    public void Pause()
    {
        IsPaused = true;
        _accumulator = 0.0;
    }

    public void ToggleEnabled()
    {
        IsEnabled = !IsEnabled;
        _accumulator = 0.0;
    }

    public void ToggleCollisions() => CollisionsEnabled = !CollisionsEnabled;
    public void SetCollisionsEnabled(bool enabled) => CollisionsEnabled = enabled;
    public void ToggleMutualGravity() => SetMutualGravityEnabled(!MutualGravityEnabled);

    public void SetMutualGravityEnabled(bool enabled)
    {
        MutualGravityEnabled = enabled;
        RefreshAccelerations();
        ResetEnergyReference();
    }

    public void SetAggregationEnabled(bool enabled) => AggregationEnabled = enabled;

    public void SetGravityMode(GravityMode4D mode)
    {
        RequestedGravityMode = mode;
        RefreshAccelerations();
        ResetEnergyReference();
    }

    public void SetAggregationCollisionInterval(int interval)
    {
        if (interval < 1 || interval > 60)
        {
            throw new ArgumentOutOfRangeException(nameof(interval));
        }

        AggregationCollisionInterval = interval;
        _aggregationStepCounter = 0;
    }

    public void SetAggregationRadiusScale(double value) => AggregationSystem.SetRadiusScale(value);

    public void SetGravitationalConstant(double value)
    {
        GravitySystem.SetGravitationalConstant(value);
        RefreshAccelerations();
        ResetEnergyReference();
    }

    public void SetGravitySoftening(double value)
    {
        GravitySystem.SetSoftening(value);
        RefreshAccelerations();
        ResetEnergyReference();
    }

    public void SetGravity(Vector4D gravity)
    {
        if (!gravity.IsFinite)
        {
            throw new ArgumentOutOfRangeException(nameof(gravity));
        }

        Gravity = gravity;
        RefreshAccelerations();
        ResetEnergyReference();
    }

    public void SetRestitution(double restitution)
    {
        if (!double.IsFinite(restitution) || restitution < 0.0 || restitution > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(restitution));
        }

        Restitution = restitution;
    }

    public void AdjustTimeScale(int direction)
    {
        _timeScaleIndex = Math.Clamp(
            _timeScaleIndex + Math.Sign(direction),
            0,
            SupportedTimeScales.Length - 1);
        _accumulator = 0.0;
    }

    public PhysicsBody4D AddBody(
        Vector4D position,
        Vector4D velocity,
        double mass = 1.0,
        bool isStatic = false,
        double radius = 0.05)
    {
        if (_bodies.Count >= MaximumBodyCount)
        {
            throw new InvalidOperationException($"Physics is capped at {MaximumBodyCount:N0} bodies.");
        }

        var body = new PhysicsBody4D(_nextBodyId++, position, velocity, mass, isStatic, radius)
        {
            Acceleration = isStatic ? Vector4D.Zero : Gravity
            //Acceleration = Gravity
        };
        _bodies.Add(body);
        SelectedBody = body;
        RefreshAccelerations();
        return body;
    }

    public bool SelectBody(PhysicsBody4D body)
    {
        ArgumentNullException.ThrowIfNull(body);
        if (!body.IsAlive || !_bodies.Contains(body))
        {
            return false;
        }

        SelectedBody = body;
        return true;
    }

    /// <summary>
    /// Replaces a whole generated system and performs one acceleration refresh,
    /// avoiding repeated O(N^2) work while adding many bodies.
    /// </summary>
    public void ReplaceBodies(IReadOnlyList<PhysicsBodyInitialState4D> states)
    {
        ArgumentNullException.ThrowIfNull(states);
        if (states.Count > MaximumBodyCount)
        {
            throw new ArgumentOutOfRangeException(nameof(states));
        }

        _bodies.Clear();
        _nextBodyId = 1;
        foreach (var state in states)
        {
            _bodies.Add(new PhysicsBody4D(
                _nextBodyId++,
                state.Position,
                state.Velocity,
                state.Mass,
                state.IsStatic,
                state.Radius));
        }

        SelectedBody = _bodies.Count == 0 ? null : _bodies[^1];
        GravitySystem.ConfigureMeanField(_bodies);
        ResetCounters();
        StateVersion++;
        RefreshAccelerations();
        InvalidateEnergyReference();
    }

    public int SpawnParticles(int count, Vector4D initialVelocity)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        if (!initialVelocity.IsFinite)
        {
            throw new ArgumentOutOfRangeException(nameof(initialVelocity));
        }

        var spawnCount = Math.Min(count, MaximumParticleBodyCount - Math.Min(_bodies.Count, MaximumParticleBodyCount));
        for (var index = 0; index < spawnCount; index++)
        {
            AddBody(DeterministicSpawnPosition(_spawnSequence++), initialVelocity);
        }

        return spawnCount;
    }

    public void Clear()
    {
        _bodies.Clear();
        GravitySystem.ConfigureMeanField(_bodies);
        SelectedBody = null;
        _nextBodyId = 1;
        _spawnSequence = 0;
        ResetCounters();
        StateVersion++;
        InvalidateEnergyReference();
    }

    private void ExecuteFixedStep()
    {
        var totalStartedAt = PerformanceProfiler.Timestamp();
        var profiledTotalStartedAt = Performance.BeginPhase();

        var integrationStartedAt = Performance.BeginPhase();
        var integrationWorkerCount = ParallelWork.WorkerCountFor(_bodies.Count, 2_048);
        Performance.RecordParallelWork(integrationWorkerCount);
        if (_integrationCollisionCounts.Length < integrationWorkerCount)
        {
            Array.Resize(ref _integrationCollisionCounts, integrationWorkerCount);
        }
        Array.Clear(_integrationCollisionCounts, 0, integrationWorkerCount);
        ParallelWork.ForRanges(
            _bodies.Count,
            minimumItemsPerWorker: 2_048,
            (workerIndex, start, end) =>
            {
                var workerCollisions = 0L;
                for (var index = start; index < end; index++)
                {
                    var body = _bodies[index];
                    // Kick-drift. Acceleration is valid at the current position;
                    // every state/configuration mutation refreshes it.
                    body.BeginLeapfrogStep(FixedDeltaTime);
                    if (!body.IsStatic && body.IsAlive &&
                        CollisionsEnabled &&
                        CollisionPlane.ResolveCollision(body, Restitution))
                    {
                        workerCollisions++;
                    }
                }
                _integrationCollisionCounts[workerIndex] = workerCollisions;
            });
        for (var workerIndex = 0; workerIndex < integrationWorkerCount; workerIndex++)
        {
            CollisionCount += _integrationCollisionCounts[workerIndex];
        }
        Performance.EndPhase(PerformancePhase.Integration, integrationStartedAt);

        LastAggregationCollisionCount = 0;
        _aggregationStepCounter++;
        if (AggregationEnabled && _aggregationStepCounter >= AggregationCollisionInterval)
        {
            _aggregationStepCounter = 0;
            LastAggregationCollisionCount = AggregationSystem.Resolve(
                _bodies,
                SelectedBody,
                out var selectedSurvivor,
                Performance);
            AggregationCollisionCount += LastAggregationCollisionCount;
            if (LastAggregationCollisionCount > 0)
            {
                var cleanupStartedAt = Performance.BeginPhase();
                _bodies.RemoveAll(body => !body.IsAlive);
                SelectedBody = selectedSurvivor is { IsAlive: true }
                    ? selectedSurvivor
                    : _bodies.Count == 0
                        ? null
                        : _bodies.MaxBy(body => body.Mass);
                Performance.EndPhase(PerformancePhase.Aggregation, cleanupStartedAt);
            }
        }


        // Re-evaluate the field at the drifted (and possibly merged) positions,
        // then finish the second half-kick. This is velocity Verlet/leapfrog and
        // requires the same one gravity evaluation per fixed step as before.
        RefreshAccelerations();
        var finalKickStartedAt = Performance.BeginPhase();
        ParallelWork.ForRanges(
            _bodies.Count,
            minimumItemsPerWorker: 2_048,
            (_, start, end) =>
            {
                for (var index = start; index < end; index++)
                {
                    _bodies[index].CompleteLeapfrogStep(FixedDeltaTime);
                }
            });
        Performance.EndPhase(PerformancePhase.Integration, finalKickStartedAt);

        CompletedStepCount++;
        Performance.RecordPhysicsStep(FixedDeltaTime);
        FixedStepCompleted?.Invoke();
        Performance.EndPhase(PerformancePhase.PhysicsTotal, profiledTotalStartedAt);
        LastPhysicsStepMilliseconds = PerformanceProfiler.ElapsedMillisecondsSince(totalStartedAt);
    }

    private void RefreshAccelerations()
    {
        if (_accelerationBuffer.Length < _bodies.Count)
        {
            Array.Resize(ref _accelerationBuffer, _bodies.Count);
        }

        ParallelWork.ForRanges(
            _bodies.Count,
            minimumItemsPerWorker: 2_048,
            (_, start, end) =>
            {
                for (var index = start; index < end; index++)
                {
                    _accelerationBuffer[index] = _bodies[index].IsStatic ? Vector4D.Zero : Gravity;
                }
            });

        if (MutualGravityEnabled)
        {
            var gravityStartedAt = Performance.BeginPhase();
            if (EffectiveGravityMode == GravityMode4D.Exact)
            {
                GravitySystem.AccumulatePairwise(_bodies, _accelerationBuffer);
            }
            else
            {
                GravitySystem.AccumulateMeanField(_bodies, _accelerationBuffer);
            }
            Performance.RecordParallelWork(GravitySystem.LastWorkerCount);
            Performance.EndPhase(PerformancePhase.Gravity, gravityStartedAt);
        }

        ParallelWork.ForRanges(
            _bodies.Count,
            minimumItemsPerWorker: 2_048,
            (_, start, end) =>
            {
                for (var index = start; index < end; index++)
                {
                    _bodies[index].Acceleration = _accelerationBuffer[index];
                }
            });
    }

    public void ResetEnergyReference()
    {
        _initialTotalEnergy = CalculateCurrentEnergy().TotalEnergy;
        InvalidateEnergyCache();
    }

    private EnergyDiagnostics4D GetEnergyDiagnostics()
    {
        // Exact potential is O(N^2), so update the overlay diagnostic at 4 Hz
        // of simulated time rather than duplicating gravity work every frame.
        const int diagnosticIntervalSteps = 15;
        if (_energyDiagnosticsStateVersion == StateVersion &&
            _energyDiagnosticsStep != long.MinValue &&
            CompletedStepCount - _energyDiagnosticsStep < diagnosticIntervalSteps)
        {
            return _cachedEnergyDiagnostics;
        }

        var current = CalculateCurrentEnergy();
        var hasReference = double.IsFinite(_initialTotalEnergy);
        var drift = hasReference && Math.Abs(_initialTotalEnergy) > 1e-12
            ? 100.0 * (current.TotalEnergy - _initialTotalEnergy) / Math.Abs(_initialTotalEnergy)
            : 0.0;
        _cachedEnergyDiagnostics = new EnergyDiagnostics4D(
            current.KineticEnergy,
            current.PotentialEnergy,
            current.TotalEnergy,
            hasReference ? _initialTotalEnergy : current.TotalEnergy,
            drift,
            hasReference,
            true);
        _energyDiagnosticsStep = CompletedStepCount;
        _energyDiagnosticsStateVersion = StateVersion;
        return _cachedEnergyDiagnostics;
    }

    private (double KineticEnergy, double PotentialEnergy, double TotalEnergy) CalculateCurrentEnergy()
    {
        var kinetic = TotalKineticEnergy;
        var potential = 0.0;
        if (MutualGravityEnabled)
        {
            potential = EffectiveGravityMode == GravityMode4D.Exact
                ? GravitySystem.CalculateExactPotentialEnergy(_bodies)
                : GravitySystem.CalculateMeanFieldPotentialEnergy(_bodies);
        }

        foreach (var body in _bodies)
        {
            if (body.IsAlive && !body.IsStatic)
            {
                potential -= body.Mass * Vector4D.Dot(Gravity, body.Position);
            }
        }
        return (kinetic, potential, kinetic + potential);
    }

    private void InvalidateEnergyReference()
    {
        _initialTotalEnergy = double.NaN;
        InvalidateEnergyCache();
    }

    private void InvalidateEnergyCache()
    {
        _energyDiagnosticsStep = long.MinValue;
        _energyDiagnosticsStateVersion = -1;
    }

    private void UpdateRates(double elapsed, int steps, int collisions)
    {
        _rateElapsed += elapsed;
        _rateSteps += steps;
        _rateAggregationCollisions += collisions;
        if (_rateElapsed < 0.5)
        {
            return;
        }

        SimulationStepsPerSecond = _rateSteps / _rateElapsed;
        AggregationCollisionsPerSecond = _rateAggregationCollisions / _rateElapsed;
        _rateElapsed = 0.0;
        _rateSteps = 0;
        _rateAggregationCollisions = 0;
    }

    private void ResetCounters()
    {
        _accumulator = 0.0;
        _aggregationStepCounter = 0;
        _rateElapsed = 0.0;
        _rateSteps = 0;
        _rateAggregationCollisions = 0;
        CompletedStepCount = 0;
        CollisionCount = 0;
        AggregationCollisionCount = 0;
        LastAggregationCollisionCount = 0;
        LastPhysicsStepMilliseconds = 0.0;
        SimulationStepsPerSecond = 0.0;
        AggregationCollisionsPerSecond = 0.0;
    }

    private static Vector4D DeterministicSpawnPosition(int sequence)
    {
        var x = ((sequence % 5) - 2) * 0.22;
        var y = 1.5 + (((sequence / 5) % 3) * 0.18);
        var z = (((sequence * 3) % 7) - 3) * 0.16;
        var w = 2.0 + (((sequence * 5) % 4) * 0.15);
        return new Vector4D(x, y, z, w);
    }
}
