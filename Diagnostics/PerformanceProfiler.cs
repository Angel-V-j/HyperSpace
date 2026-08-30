using System;
using System.Diagnostics;

namespace HyperSpace.Diagnostics;

/// <summary>
/// Coarse CPU phase timings for one Update/Draw frame.
/// Measurements are accumulated with Stopwatch timestamps and committed to a
/// fixed rolling window, so the hot path performs no per-frame allocations.
/// </summary>
public sealed class PerformanceProfiler
{
    public const int DefaultRollingWindowSize = 60;

    private readonly RollingMetric[] _metrics;
    private readonly long[] _pendingPhaseTicks;
    private bool _frameActive;
    private long _frameStartedAt;
    private long _previousFrameStartedAt;
    private int _pendingPhysicsSteps;
    private double _pendingSimulatedSeconds;
    private long _pendingCollisionCandidates;
    private int _pendingMerges;
    private int _pendingParallelWorkers;
    private double _realRateElapsedSeconds;
    private long _realRatePhysicsSteps;

    public PerformanceProfiler(int rollingWindowSize = DefaultRollingWindowSize)
    {
        if (rollingWindowSize < 1 || rollingWindowSize > 600)
        {
            throw new ArgumentOutOfRangeException(nameof(rollingWindowSize));
        }

        RollingWindowSize = rollingWindowSize;
        var phaseCount = Enum.GetValues<PerformancePhase>().Length;
        _metrics = new RollingMetric[phaseCount];
        _pendingPhaseTicks = new long[phaseCount];
        for (var index = 0; index < phaseCount; index++)
        {
            _metrics[index] = new RollingMetric(rollingWindowSize);
        }

        MainThreadId = Environment.CurrentManagedThreadId;
    }

    public int RollingWindowSize { get; }
    public int LogicalProcessorCount => Environment.ProcessorCount;
    public int MainThreadId { get; }
    public int? LastPhysicsThreadId { get; private set; }
    public bool PhysicsRunsOnMainThread => LastPhysicsThreadId == MainThreadId;
    public bool UsesParallelPhysics => ParallelWorkerCountThisFrame > 1;
    public int ParallelWorkerCountThisFrame { get; private set; }

    public double RealElapsedMilliseconds { get; private set; }
    public double SchedulerElapsedMilliseconds { get; private set; }
    public double FixedTimestepMilliseconds { get; private set; }
    public double AccumulatedSimulationMilliseconds { get; private set; }
    public double SimulatedSecondsThisFrame { get; private set; }
    public double TimeScale { get; private set; }
    public double SimulationStepsPerSecond { get; private set; }
    public double SchedulerStepsPerSecond { get; private set; }
    public int PhysicsStepsThisFrame { get; private set; }
    public long CollisionCandidatesThisFrame { get; private set; }
    public int MergesThisFrame { get; private set; }

    public void BeginFrame(
        double realElapsedSeconds,
        double fixedTimestepSeconds,
        double timeScale)
    {
        if (!double.IsFinite(realElapsedSeconds) || realElapsedSeconds < 0.0 ||
            !double.IsFinite(fixedTimestepSeconds) || fixedTimestepSeconds <= 0.0 ||
            !double.IsFinite(timeScale) || timeScale <= 0.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(realElapsedSeconds),
                "Profiling schedule values must be finite and non-negative/positive as applicable.");
        }

        // MonoGame may execute more than one Update before one Draw while catching up.
        // Keep accumulating into the same presented-frame sample until CompleteFrame.
        if (_frameActive)
        {
            SchedulerElapsedMilliseconds += realElapsedSeconds * 1000.0;
            FixedTimestepMilliseconds = fixedTimestepSeconds * 1000.0;
            TimeScale = timeScale;
            return;
        }

        Array.Clear(_pendingPhaseTicks);
        _pendingPhysicsSteps = 0;
        _pendingSimulatedSeconds = 0.0;
        _pendingCollisionCandidates = 0;
        _pendingMerges = 0;
        _pendingParallelWorkers = 0;
        var now = Stopwatch.GetTimestamp();
        RealElapsedMilliseconds = _previousFrameStartedAt == 0L
            ? realElapsedSeconds * 1000.0
            : TicksToMilliseconds(Math.Max(0L, now - _previousFrameStartedAt));
        SchedulerElapsedMilliseconds = realElapsedSeconds * 1000.0;
        FixedTimestepMilliseconds = fixedTimestepSeconds * 1000.0;
        TimeScale = timeScale;
        _previousFrameStartedAt = now;
        _frameStartedAt = now;
        _frameActive = true;
    }

    /// <summary>
    /// Starts a coarse phase. Zero means that no frame is currently being sampled.
    /// </summary>
    public long BeginPhase() => _frameActive ? Stopwatch.GetTimestamp() : 0L;

    public void EndPhase(PerformancePhase phase, long startedAt)
    {
        if (!_frameActive || startedAt == 0L)
        {
            return;
        }

        var elapsed = Stopwatch.GetTimestamp() - startedAt;
        if (elapsed > 0L)
        {
            _pendingPhaseTicks[(int)phase] += elapsed;
        }
    }

    public void RecordPhysicsStep(double simulatedSeconds)
    {
        if (!_frameActive)
        {
            return;
        }

        _pendingPhysicsSteps++;
        _pendingSimulatedSeconds += simulatedSeconds;
        LastPhysicsThreadId = Environment.CurrentManagedThreadId;
    }

    public void AddCollisionCandidates(long count)
    {
        if (_frameActive && count > 0)
        {
            _pendingCollisionCandidates += count;
        }
    }

    public void AddMerges(int count)
    {
        if (_frameActive && count > 0)
        {
            _pendingMerges += count;
        }
    }

    public void RecordParallelWork(int workerCount)
    {
        if (_frameActive)
        {
            _pendingParallelWorkers = Math.Max(_pendingParallelWorkers, workerCount);
        }
    }

    public void CompleteFrame(
        double accumulatedSimulationSeconds,
        double simulationStepsPerSecond)
    {
        if (!_frameActive)
        {
            return;
        }

        var frameElapsed = Stopwatch.GetTimestamp() - _frameStartedAt;
        if (frameElapsed > 0L)
        {
            _pendingPhaseTicks[(int)PerformancePhase.FrameTotal] = frameElapsed;
        }

        for (var index = 0; index < _metrics.Length; index++)
        {
            _metrics[index].Add(TicksToMilliseconds(_pendingPhaseTicks[index]));
        }

        AccumulatedSimulationMilliseconds = Math.Max(0.0, accumulatedSimulationSeconds) * 1000.0;
        SimulatedSecondsThisFrame = _pendingSimulatedSeconds;
        SchedulerStepsPerSecond = Math.Max(0.0, simulationStepsPerSecond);
        PhysicsStepsThisFrame = _pendingPhysicsSteps;
        CollisionCandidatesThisFrame = _pendingCollisionCandidates;
        MergesThisFrame = _pendingMerges;
        ParallelWorkerCountThisFrame = _pendingParallelWorkers;
        _realRateElapsedSeconds += RealElapsedMilliseconds / 1000.0;
        _realRatePhysicsSteps += _pendingPhysicsSteps;
        if (_realRateElapsedSeconds >= 0.5)
        {
            SimulationStepsPerSecond = _realRatePhysicsSteps / _realRateElapsedSeconds;
            _realRateElapsedSeconds = 0.0;
            _realRatePhysicsSteps = 0;
        }
        _frameActive = false;
    }

    public PerformanceMetric Metric(PerformancePhase phase) =>
        _metrics[(int)phase].Snapshot;

    public static long Timestamp() => Stopwatch.GetTimestamp();

    public static double ElapsedMillisecondsSince(long startedAt) =>
        TicksToMilliseconds(Math.Max(0L, Stopwatch.GetTimestamp() - startedAt));

    public void Reset()
    {
        foreach (var metric in _metrics)
        {
            metric.Reset();
        }

        Array.Clear(_pendingPhaseTicks);
        _frameActive = false;
        _previousFrameStartedAt = 0L;
        _realRateElapsedSeconds = 0.0;
        _realRatePhysicsSteps = 0;
        PhysicsStepsThisFrame = 0;
        CollisionCandidatesThisFrame = 0;
        MergesThisFrame = 0;
        ParallelWorkerCountThisFrame = 0;
        RealElapsedMilliseconds = 0.0;
        SchedulerElapsedMilliseconds = 0.0;
        FixedTimestepMilliseconds = 0.0;
        AccumulatedSimulationMilliseconds = 0.0;
        SimulatedSecondsThisFrame = 0.0;
        SimulationStepsPerSecond = 0.0;
        SchedulerStepsPerSecond = 0.0;
    }

    private static double TicksToMilliseconds(long ticks) =>
        ticks * 1000.0 / Stopwatch.Frequency;

    private sealed class RollingMetric
    {
        private readonly double[] _samples;
        private int _nextIndex;
        private int _count;
        private double _sum;

        public RollingMetric(int capacity)
        {
            _samples = new double[capacity];
        }

        public PerformanceMetric Snapshot { get; private set; }

        public void Add(double milliseconds)
        {
            if (_count == _samples.Length)
            {
                _sum -= _samples[_nextIndex];
            }
            else
            {
                _count++;
            }

            _samples[_nextIndex] = milliseconds;
            _sum += milliseconds;
            _nextIndex = (_nextIndex + 1) % _samples.Length;
            Snapshot = new PerformanceMetric(
                milliseconds,
                _count == 0 ? 0.0 : _sum / _count,
                _count);
        }

        public void Reset()
        {
            Array.Clear(_samples);
            _nextIndex = 0;
            _count = 0;
            _sum = 0.0;
            Snapshot = default;
        }
    }
}

public enum PerformancePhase
{
    PhysicsTotal,
    Gravity,
    CollisionDetection,
    CollisionGrid,
    CollisionCandidates,
    CollisionSort,
    CollisionResolution,
    Aggregation,
    Integration,
    TrailUpdate,
    RenderingPreparation,
    NBodyRenderCpu,
    UiUpdate,
    UpdateTotal,
    RenderTotal,
    FrameTotal
}

public readonly record struct PerformanceMetric(
    double CurrentMilliseconds,
    double AverageMilliseconds,
    int SampleCount);
