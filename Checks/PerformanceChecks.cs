using System;
using System.Collections.Generic;
using System.Linq;
using HyperSpace.Diagnostics;
using HyperSpace.Geometry;
using HyperSpace.Mathematics;
using HyperSpace.Physics;
using HyperSpace.Projection;
using HyperSpace.Transformations;

internal static class PerformanceChecks
{
    public static void CheckCollisionPairSort()
    {
        var random = new Random(173);
        var original = Enumerable.Range(0, 150_000)
            .Select(_ => (FirstIndex: random.Next(20_000), SecondIndex: random.Next(20_000))).ToArray();
        var expected = original.ToArray();
        Array.Sort(expected);
        var limit = ParallelWork.MaximumWorkerCount;
        try
        {
            foreach (var workers in new[] { 1, Environment.ProcessorCount })
            {
                ParallelWork.MaximumWorkerCount = workers;
                var actual = new List<(int FirstIndex, int SecondIndex)>(original);
                new CollisionPairSorter().Sort(actual, 20_000);
                if (!expected.SequenceEqual(actual))
                    throw new InvalidOperationException("Radix sorting changed lexicographic candidate ordering.");
            }
        }
        finally { ParallelWork.MaximumWorkerCount = limit; }
    }

    public static void CheckExactGravity()
    {
        var bodies = Enumerable.Range(0, 500).Select(index => new PhysicsBody4D(index,
            new Vector4D(index * 0.1, index % 7, index % 11, index % 13), Vector4D.Zero,
            1 + index % 9, isStatic: index % 17 == 0)).ToArray();
        var serial = new Vector4D[bodies.Length];
        var parallel = new Vector4D[bodies.Length];
        Array.Fill(serial, new Vector4D(0.1, -0.2, 0.3, -0.4));
        serial.CopyTo(parallel, 0);
        var gravity = new GravitySystem4D();
        var limit = ParallelWork.MaximumWorkerCount;
        try
        {
            ParallelWork.MaximumWorkerCount = 1;
            gravity.AccumulatePairwise(bodies, serial);
            ParallelWork.MaximumWorkerCount = Environment.ProcessorCount;
            gravity.AccumulatePairwise(bodies, parallel);
            if (!serial.SequenceEqual(parallel))
                throw new InvalidOperationException("Target-parallel exact gravity changed floating-point accumulation order.");
        }
        finally { ParallelWork.MaximumWorkerCount = limit; }
    }

    public static void CheckParallelDeterminism()
    {
        var previousLimit = ParallelWork.MaximumWorkerCount;
        try
        {
            var serial = Run(1);
            var parallel = Run(Environment.ProcessorCount);
            if (serial.Hash != parallel.Hash || serial.Count != parallel.Count ||
                !serial.Pairs.SequenceEqual(parallel.Pairs) || !serial.Merges.SequenceEqual(parallel.Merges))
                throw new InvalidOperationException("Parallel execution changed candidates, merges, or the bitwise 20k-body state.");
        }
        finally { ParallelWork.MaximumWorkerCount = previousLimit; }

        static (ulong Hash, int Count, int[] Pairs, int[] Merges) Run(int workers)
        {
            ParallelWork.MaximumWorkerCount = workers;
            var world = new PhysicsWorld4D();
            using var lab = new NBodyLab4D(world);
            lab.Settings.TryApplyBodyCount("20000", out _);
            if (!lab.GenerateSystem()) throw new InvalidOperationException(lab.LastGenerationMessage);
            var pairs = new int[16];
            var merges = new int[16];
            for (var step = 0; step < pairs.Length; step++)
            {
                world.StepOnce();
                pairs[step] = world.AggregationSystem.LastCandidatePairCount;
                merges[step] = world.LastAggregationCollisionCount;
            }
            if (world.Bodies.Any(body => !body.Position.IsFinite || !body.Velocity.IsFinite))
                throw new InvalidOperationException("Parallel physics produced non-finite state.");
            return (PerformanceBenchmarks.StateHash(world.Bodies), world.Bodies.Count, pairs, merges);
        }
    }

    public static void CheckProjection()
    {
        var transform = new Transform4D();
        var camera = new Camera4D();
        var projector = new PerspectiveProjector4D();
        foreach (var plane in Enum.GetValues<RotationPlane4D>())
        {
            transform.Rotate(plane, 0.31 + (int)plane * 0.07);
            camera.Rotate(plane, -0.17 - (int)plane * 0.03);
        }
        transform.MoveWorld(new Vector4D(0.4, -0.8, 0.1, 0.3));
        transform.MultiplyScale(1.3);
        var random = new Random(1337);
        var points = Enumerable.Range(0, 4096).Select(_ => new Vector4D(
            random.NextDouble() * 20 - 10, random.NextDouble() * 20 - 10,
            random.NextDouble() * 20 - 10, random.NextDouble() * 20 - 10)).ToArray();
        var result = new WireframeProjectionPipeline4D().Project(points, Array.Empty<Edge>(), transform, camera, projector);
        for (var index = 0; index < points.Length; index++)
        {
            var world = transform.TransformPoint(points[index]);
            var cameraPoint = camera.WorldToCameraSpace(world);
            var visible = projector.TryProject(cameraPoint, out var expected);
            var actual = result.Vertices[index];
            if (visible != actual.IsVisible || (visible &&
                (Math.Abs(expected.X - actual.Position.X) > 1e-10 ||
                 Math.Abs(expected.Y - actual.Position.Y) > 1e-10 ||
                 Math.Abs(expected.Z - actual.Position.Z) > 1e-10)))
                throw new InvalidOperationException("Prepared parallel projection differs from the original six-plane formula.");
        }
    }

    public static void CheckFixedStepDebt()
    {
        var world = new PhysicsWorld4D();
        world.SetGravity(Vector4D.Zero);
        world.SetCollisionsEnabled(false);
        var body = world.AddBody(Vector4D.Zero, new Vector4D(1, 0, 0, 0));
        world.Play();
        var steps = world.Update(1);
        if (steps > 8 || !world.CatchUpLimitedLastUpdate ||
            Math.Abs(world.AccumulatedSimulationTime - (1 - steps * world.FixedDeltaTime)) > 1e-12)
            throw new InvalidOperationException("Catch-up must be bounded without discarding fixed-step debt.");
        for (var update = 0; update < 100 && world.AccumulatedSimulationTime >= world.FixedDeltaTime; update++)
            world.Update(0);
        if (Math.Abs(body.Position.X + world.AccumulatedSimulationTime - 1) > 1e-12)
            throw new InvalidOperationException("Draining accumulated time changed the fixed-step trajectory.");
    }
}
