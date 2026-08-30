using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using HyperSpace.Diagnostics;
using HyperSpace.Geometry;
using HyperSpace.Mathematics;
using HyperSpace.Physics;
using HyperSpace.Projection;
using HyperSpace.Transformations;

internal static class PerformanceBenchmarks
{
    public static void Run20k()
    {
        var world = new PhysicsWorld4D();
        using var lab = new NBodyLab4D(world);
        lab.Settings.TryApplyBodyCount("20000", out _);
        lab.SetGravityMode(GravityMode4D.MeanFieldApproximate);
        if (!lab.GenerateSystem()) throw new InvalidOperationException(lab.LastGenerationMessage);
        for (var index = 0; index < 16; index++) world.StepOnce();

        const int samples = 120;
        var totals = new double[Enum.GetValues<PerformancePhase>().Length];
        var candidates = 0L;
        var merges = 0L;
        using var process = Process.GetCurrentProcess();
        var mainThreadId = OperatingSystem.IsWindows() ? NativeThreadId() : 0;
        var beforeThreads = ThreadTimes(process);
        var beforeCpu = process.TotalProcessorTime;
        var beforeAllocated = GC.GetTotalAllocatedBytes(precise: true);
        var beforeCollections = new[] { GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2) };
        var timer = Stopwatch.StartNew();
        for (var sample = 0; sample < samples; sample++)
        {
            world.Performance.BeginFrame(world.FixedDeltaTime, world.FixedDeltaTime, 1);
            world.StepOnce();
            world.Performance.CompleteFrame(0, 0);
            foreach (var phase in Enum.GetValues<PerformancePhase>())
                totals[(int)phase] += world.Performance.Metric(phase).CurrentMilliseconds;
            candidates += world.Performance.CollisionCandidatesThisFrame;
            merges += world.Performance.MergesThisFrame;
        }
        timer.Stop();
        process.Refresh();
        var cpuMs = (process.TotalProcessorTime - beforeCpu).TotalMilliseconds;
        var afterThreads = ThreadTimes(process);
        var mainMs = afterThreads.GetValueOrDefault(mainThreadId) - beforeThreads.GetValueOrDefault(mainThreadId);
        var nonMainMs = afterThreads.Where(pair => pair.Key != mainThreadId)
            .Sum(pair => Math.Max(0, pair.Value - beforeThreads.GetValueOrDefault(pair.Key)));
        var activeWorkers = afterThreads.Count(pair => pair.Key != mainThreadId &&
            pair.Value - beforeThreads.GetValueOrDefault(pair.Key) >= timer.Elapsed.TotalMilliseconds * 0.01);
        var allocated = GC.GetTotalAllocatedBytes(precise: true) - beforeAllocated;
        var collections = Enumerable.Range(0, 3).Select(index => GC.CollectionCount(index) - beforeCollections[index]);
        Console.WriteLine($"20K workers={ParallelWork.MaximumWorkerCount} logical={Environment.ProcessorCount} samples={samples} collisionInterval={world.AggregationCollisionInterval}");
        foreach (var phase in new[] { PerformancePhase.PhysicsTotal, PerformancePhase.Gravity,
                     PerformancePhase.Integration, PerformancePhase.CollisionDetection,
                     PerformancePhase.CollisionGrid, PerformancePhase.CollisionCandidates,
                     PerformancePhase.CollisionSort,
                     PerformancePhase.CollisionResolution, PerformancePhase.Aggregation })
            Console.WriteLine($"{phase}={totals[(int)phase] / samples:F4} ms/step");
        Console.WriteLine($"CPU wall={timer.Elapsed.TotalMilliseconds:F2}ms process={cpuMs:F2}ms total={100 * cpuMs / timer.Elapsed.TotalMilliseconds / Environment.ProcessorCount:F2}% coreEquivalent={cpuMs / timer.Elapsed.TotalMilliseconds:F2} main={Math.Clamp(100 * mainMs / timer.Elapsed.TotalMilliseconds, 0, 100):F1}% nonMainCpu={nonMainMs:F2}ms activeNonMainThreads={activeWorkers} (OS thread counters are coarse)");
        Console.WriteLine($"GC allocated={allocated / (double)samples:F0} bytes/step collections={string.Join('/', collections)}");
        Console.WriteLine($"STATE bodies={world.Bodies.Count} candidates={candidates} merges={merges} hash={StateHash(world.Bodies):X16} finite={world.Bodies.All(body => body.Position.IsFinite && body.Velocity.IsFinite)}");

        var pipeline = new WireframeProjectionPipeline4D();
        var points = world.Bodies.Select(body => body.Position).ToArray();
        var transform = new Transform4D();
        var camera = new Camera4D();
        var projector = new PerspectiveProjector4D();
        for (var sample = 0; sample < 10; sample++) pipeline.Project(points, Array.Empty<Edge>(), transform, camera, projector);
        timer.Restart();
        for (var sample = 0; sample < 60; sample++) pipeline.Project(points, Array.Empty<Edge>(), transform, camera, projector);
        timer.Stop();
        Console.WriteLine($"ProjectionCpuFallback={timer.Elapsed.TotalMilliseconds / 60:F4} ms (N-body render uses GPU instead)");
    }

    public static ulong StateHash(IReadOnlyList<PhysicsBody4D> bodies)
    {
        var hash = 14695981039346656037UL;
        foreach (var body in bodies)
        {
            Add(body.Id);
            Add(BitConverter.DoubleToInt64Bits(body.Position.X));
            Add(BitConverter.DoubleToInt64Bits(body.Position.Y));
            Add(BitConverter.DoubleToInt64Bits(body.Position.Z));
            Add(BitConverter.DoubleToInt64Bits(body.Position.W));
            Add(BitConverter.DoubleToInt64Bits(body.Velocity.X));
            Add(BitConverter.DoubleToInt64Bits(body.Velocity.Y));
            Add(BitConverter.DoubleToInt64Bits(body.Velocity.Z));
            Add(BitConverter.DoubleToInt64Bits(body.Velocity.W));
            Add(BitConverter.DoubleToInt64Bits(body.Mass));
            Add(BitConverter.DoubleToInt64Bits(body.Radius));
        }
        return hash;
        void Add(long value) => hash = unchecked((hash ^ (ulong)value) * 1099511628211UL);
    }

    private static Dictionary<int, double> ThreadTimes(Process process)
    {
        var times = new Dictionary<int, double>();
        if (!OperatingSystem.IsWindows()) return times;
        foreach (ProcessThread thread in process.Threads)
        {
            using (thread)
            {
                try { times[thread.Id] = thread.TotalProcessorTime.TotalMilliseconds; }
                catch (InvalidOperationException) { }
            }
        }
        return times;
    }

    // Windows-only diagnostic; this is not used by the simulation or rendering backend.
    [DllImport("kernel32.dll", EntryPoint = "GetCurrentThreadId")]
    private static extern int NativeThreadId();
}
