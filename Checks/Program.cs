using System;
using System.Collections.Generic;
using System.Linq;
using HyperSpace.Diagnostics;
using HyperSpace.Geometry;
using HyperSpace.Input;
using HyperSpace.Mathematics;
using HyperSpace.Physics;
using HyperSpace.Projection;
using HyperSpace.Rendering;
using HyperSpace.Scene;
using HyperSpace.Transformations;
using HyperSpace.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

if (args.Contains("--profile-nbody", StringComparer.OrdinalIgnoreCase))
{
    RunNBodyProfilingReport();
    return;
}

if (args.Contains("--profile-nbody-20k", StringComparer.OrdinalIgnoreCase))
{
    PerformanceBenchmarks.Run20k();
    return;
}

if (args.Contains("--benchmark-render", StringComparer.OrdinalIgnoreCase))
{
    using var benchmark = new RenderingBenchmarkGame();
    benchmark.Run();
    return;
}

if (args.Contains("--audit-energy", StringComparer.OrdinalIgnoreCase))
{
    EnergyChecks.RunAudit();
    return;
}

var checks = new (string Name, Action Run)[]
{
    ("Tesseract topology", CheckTesseractTopology),
    ("Tesseract cubic cells", CheckTesseractCells),
    ("Hypersphere sampling", CheckHypersphereSampling),
    ("Regular 4-simplex topology", CheckSimplexTopology),
    ("Irregular 4D polytope topology", CheckIrregularPolytope),
    ("4D spiral sampling", CheckSpiralSampling),
    ("Curve playback state", CheckCurvePlayback),
    ("Quaternion algebra", CheckQuaternionAlgebra),
    ("Quaternion Julia iteration", CheckQuaternionJuliaIteration),
    ("Incremental 4D fractal generation", CheckIncrementalFractalGeneration),
    ("Fractal presets and display state", CheckFractalPresetsAndDisplayState),
    ("4D physics integration", CheckPhysicsIntegration),
    ("4D gravity and W movement", CheckPhysicsGravityAndWMovement),
    ("W hyperplane collision", CheckHyperplaneCollision),
    ("Fixed timestep physics", CheckFixedTimestepPhysics),
    ("Deterministic particle spawning", CheckDeterministicParticleSpawning),
    ("Physics hyperplane visualization", CheckPhysicsHyperplaneVisualization),
    ("4D inverse-cube gravity", CheckFourDimensionalGravityLaw),
    ("Pairwise gravity symmetry", CheckPairwiseGravitySymmetry),
    ("Static central body and free motion", CheckStaticCentralBodyAndFreeMotion),
    ("Gravity Lab trajectory and determinism", CheckGravityLabTrajectoryAndDeterminism),
    ("Gravity Lab velocity regimes", CheckGravityLabVelocityRegimes),
    ("4D aggregation conservation", CheckAggregationConservation),
    ("Aggregation through W", CheckAggregationThroughW),
    ("N-body projected screen picking", CheckNBodyProjectedScreenPicking),
    ("N-body selection trail and aggregation", CheckNBodySelectionLifecycle),
    ("Performance profiler instrumentation", CheckPerformanceProfilerInstrumentation),
    ("N-body input validation", CheckNBodyInputValidation),
    ("Deterministic non-overlapping 4D cloud", CheckNBodyGeneration),
    ("N-body gravity quality modes", CheckNBodyGravityQualityModes),
    ("N-body gravity and aggregation toggles", CheckNBodyIndependentToggles),
    ("N-body lab reset and defaults", CheckNBodyLabLifecycle),
    ("N-body total energy stability", EnergyChecks.CheckEnergyStability),
    ("N-body scale benchmark", CheckNBodyScaleBenchmark),
    ("Common geometry projection", CheckCommonGeometryProjection),
    ("4D reference grid", CheckReferenceGrid),
    ("Six plane rotations", CheckPlaneRotations),
    ("Rotation inverse", CheckRotationInverse),
    ("4D camera space", CheckCameraSpace),
    ("4D perspective projection", CheckPerspectiveProjection),
    ("Projection safety", CheckProjectionSafety),
    ("Animated 4D transformations", CheckTransformationAnimation),
    ("Minimal UI button states", CheckUiButtonStates),
    ("Display layer state", CheckDisplayLayerState),
    ("Interactive input mapping", CheckInputMapping),
    ("Parallel physics determinism", PerformanceChecks.CheckParallelDeterminism),
    ("Parallel exact gravity accumulation order", PerformanceChecks.CheckExactGravity),
    ("Parallel radix collision ordering", PerformanceChecks.CheckCollisionPairSort),
    ("Prepared parallel projection equivalence", PerformanceChecks.CheckProjection),
    ("Bounded fixed-step catch-up retains debt", PerformanceChecks.CheckFixedStepDebt)
};

try
{
    foreach (var check in checks)
    {
        check.Run();
        Console.WriteLine($"PASS: {check.Name}");
    }
}
catch (Exception exception)
{
    Console.Error.WriteLine($"FAIL: {exception.Message}");
    Environment.ExitCode = 1;
}

static void CheckTesseractTopology()
{
    var tesseract = new Tesseract4D();
    Require(tesseract.Vertices.Count == 16, "A tesseract must have 16 vertices.");
    Require(tesseract.Edges.Count == 32, "A tesseract must have 32 edges.");

    var degrees = new int[tesseract.Vertices.Count];
    var edgeCountsByAxis = new int[4];

    foreach (var edge in tesseract.Edges)
    {
        var start = tesseract.Vertices[edge.Start];
        var end = tesseract.Vertices[edge.End];
        var changedCoordinates =
            Different(start.X, end.X) +
            Different(start.Y, end.Y) +
            Different(start.Z, end.Z) +
            Different(start.W, end.W);

        Require(changedCoordinates == 1, "Each edge must change exactly one coordinate.");
        var edgeAxis = edge.Axis ?? throw new InvalidOperationException(
            "Every tesseract edge must record its 4D direction.");
        Require(
            GetCoordinate(start, edgeAxis) != GetCoordinate(end, edgeAxis),
            "The semantic edge axis must be the coordinate that changes.");
        edgeCountsByAxis[(int)edgeAxis]++;
        degrees[edge.Start]++;
        degrees[edge.End]++;
    }

    Require(degrees.All(degree => degree == 4), "Every tesseract vertex must have degree four.");
    Require(edgeCountsByAxis.All(count => count == 8),
        "A tesseract must have eight edges in each X/Y/Z/W direction.");
}

static void CheckTesseractCells()
{
    var tesseract = new Tesseract4D();
    Require(tesseract.Cells.Count == 8, "A tesseract boundary must contain eight cubic cells.");

    var vertexMembership = new int[tesseract.Vertices.Count];
    var faceMembership = new Dictionary<string, int>();
    var labels = new HashSet<string>();

    foreach (var cell in tesseract.Cells)
    {
        var fixedAxis = cell.FixedAxis ?? throw new InvalidOperationException(
            "A tesseract cell must declare its fixed axis.");
        Require(labels.Add(cell.Label), "Each fixed-axis/sign cell label must be unique.");
        Require(cell.FixedSign is -1 or 1, "Cell sign must be either -1 or +1.");
        Require(cell.VertexIndices.Count == 8, "Each cubic cell must contain eight vertices.");
        Require(cell.VertexIndices.Distinct().Count() == 8,
            "A cubic cell cannot repeat a vertex.");
        Require(cell.Faces.Count == 6, "Each cubic cell must contain six square faces.");

        foreach (var vertexIndex in cell.VertexIndices)
        {
            var vertex = tesseract.Vertices[vertexIndex];
            RequireNear(
                GetCoordinate(vertex, fixedAxis),
                cell.FixedSign,
                1e-12,
                $"Cell {cell.Label} must fix its declared coordinate.");
            vertexMembership[vertexIndex]++;
        }

        foreach (var face in cell.Faces)
        {
            var indices = face.VertexIndices.ToArray();
            Require(indices.Distinct().Count() == 4, "A square face must contain four vertices.");
            Require(indices.All(cell.VertexIndices.Contains),
                "Every cell face vertex must belong to that cell.");

            for (var index = 0; index < indices.Length; index++)
            {
                var next = (index + 1) % indices.Length;
                Require(ChangedCoordinateCount(
                    tesseract.Vertices[indices[index]],
                    tesseract.Vertices[indices[next]]) == 1,
                    "Consecutive square-face vertices must share an edge.");
            }

            var key = string.Join(",", indices.OrderBy(index => index));
            faceMembership[key] = faceMembership.GetValueOrDefault(key) + 1;
        }
    }

    Require(labels.SetEquals(["X-", "X+", "Y-", "Y+", "Z-", "Z+", "W-", "W+"]),
        "The eight cells must be X-/X+/Y-/Y+/Z-/Z+/W-/W+.");
    Require(vertexMembership.All(count => count == 4),
        "Every tesseract vertex must belong to four cubic boundary cells.");
    Require(faceMembership.Count == 24,
        "A tesseract must contain 24 unique square faces.");
    Require(tesseract.Faces.Count == 24,
        "The common geometry view must expose the 24 unique square faces.");
    Require(faceMembership.Values.All(count => count == 2),
        "Every square face must be shared by exactly two cubic cells.");
}

static void CheckHypersphereSampling()
{
    const double radius = 1.25;
    var sphere = new Hypersphere4D(radius, wSegments: 4, polarSegments: 4, azimuthSegments: 8);

    Require(sphere.Vertices.Count == 80,
        "The documented default S3 sampling must contain 80 vertices.");
    Require(sphere.Edges.Count == 272,
        "The documented default S3 parameter mesh must contain 272 edges.");
    Require(sphere.Faces.Count == 96,
        "Three sampled 2-sphere shells must contain 96 polygonal faces.");
    Require(sphere.Cells.Count == 0,
        "Sampled S3 shells must not be mislabeled as volumetric boundary cells.");

    foreach (var vertex in sphere.Vertices)
    {
        RequireNear(vertex.LengthSquared, radius * radius, 1e-11,
            "Every hypersphere sample must satisfy x^2+y^2+z^2+w^2=r^2.");
    }

    Require(sphere.Vertices.Any(vertex => vertex.W > 0.0) &&
        sphere.Vertices.Any(vertex => vertex.W < 0.0),
        "The hypersphere sampling must cover positive and negative W.");
    RequireValidTopology(sphere);
}

static void CheckSimplexTopology()
{
    var simplex = new Simplex4D(radius: 1.35);
    Require(simplex.Vertices.Count == 5, "A 4-simplex must have five vertices.");
    Require(simplex.Edges.Count == 10, "A 4-simplex must have ten edges.");
    Require(simplex.Faces.Count == 10, "A 4-simplex must have ten triangular faces.");
    Require(simplex.Cells.Count == 5, "A 4-simplex must have five tetrahedral cells.");
    Require(simplex.Faces.All(face => face.VertexIndices.Count == 3),
        "Every 4-simplex face must be triangular.");
    Require(simplex.Cells.All(cell => cell.VertexIndices.Count == 4 && cell.Faces.Count == 4),
        "Every 4-simplex boundary cell must be a tetrahedron.");

    var centroid = simplex.Vertices.Aggregate(Vector4D.Zero, (sum, vertex) => sum + vertex) * 0.2;
    RequireVectorNear(centroid, Vector4D.Zero, 1e-12,
        "The regular 4-simplex must be centered at the origin.");
    var edgeLengths = simplex.Edges
        .Select(edge => (simplex.Vertices[edge.End] - simplex.Vertices[edge.Start]).Length)
        .ToArray();
    Require(edgeLengths.All(length => Math.Abs(length - edgeLengths[0]) < 1e-12),
        "All edges of the regular 4-simplex must have equal length.");

    RequireEachCellFaceSharedTwice(simplex);
    RequireValidTopology(simplex);
}

static void CheckIrregularPolytope()
{
    var polytope = new IrregularPolytope4D();
    var repeated = new IrregularPolytope4D();
    Require(polytope.Vertices.SequenceEqual(repeated.Vertices),
        "The irregular polytope must be deterministic across runs.");
    Require(polytope.Vertices.Count == 8, "A 4D cross-polytope must have eight vertices.");
    Require(polytope.Edges.Count == 24, "A 4D cross-polytope must have 24 edges.");
    Require(polytope.Faces.Count == 32, "A 4D cross-polytope must have 32 triangular faces.");
    Require(polytope.Cells.Count == 16, "A 4D cross-polytope must have 16 tetrahedral cells.");
    Require(polytope.Faces.All(face => face.VertexIndices.Count == 3),
        "Every irregular 16-cell face must remain triangular.");

    var centroid = polytope.Vertices.Aggregate(Vector4D.Zero, (sum, vertex) => sum + vertex) * 0.125;
    RequireVectorNear(centroid, Vector4D.Zero, 1e-12,
        "The irregular realization must remain centered for inspection.");
    var distinctEdgeLengths = polytope.Edges
        .Select(edge => Math.Round(
            (polytope.Vertices[edge.End] - polytope.Vertices[edge.Start]).Length,
            digits: 8))
        .Distinct()
        .Count();
    Require(distinctEdgeLengths >= 8,
        "The irregular realization must have meaningfully varied edge lengths.");
    Require(polytope.Vertices.Count != new Tesseract4D().Vertices.Count,
        "The irregular object must not be a deformed tesseract topology.");

    RequireEachCellFaceSharedTwice(polytope);
    RequireValidTopology(polytope);
}

static void CheckSpiralSampling()
{
    var parameters = SpiralParameters.Default;
    var generator = new Spiral4DGenerator();
    var spiral = generator.Generate(parameters);

    Require(spiral.Vertices.Count == 600,
        "The default 4D spiral must contain 600 samples.");
    Require(spiral.Edges.Count == 599,
        "A 600-point open polyline must contain 599 consecutive edges.");
    Require(spiral.Faces.Count == 0 && spiral.Cells.Count == 0,
        "A curve must not invent polygon faces or volumetric cells.");
    RequireVectorNear(
        spiral.Vertices[0],
        new Vector4D(parameters.R1, 0.0, parameters.R2, 0.0),
        1e-12,
        "P(0) must match the parametric definition.");

    var testT = Math.PI / 4.0;
    RequireVectorNear(
        Spiral4DGenerator.Evaluate(parameters, testT),
        new Vector4D(
            parameters.R1 * Math.Cos(testT),
            parameters.R1 * Math.Sin(testT),
            parameters.R2 * Math.Cos(parameters.K * testT),
            parameters.R2 * Math.Sin(parameters.K * testT)),
        1e-12,
        "Direct evaluation must use both circular parameter components.");

    foreach (var vertex in spiral.Vertices)
    {
        RequireNear(
            (vertex.X * vertex.X) + (vertex.Y * vertex.Y),
            parameters.R1 * parameters.R1,
            1e-12,
            "Every sample must remain on the XY circle of radius r1.");
        RequireNear(
            (vertex.Z * vertex.Z) + (vertex.W * vertex.W),
            parameters.R2 * parameters.R2,
            1e-12,
            "Every sample must remain on the ZW circle of radius r2.");
    }

    Require(spiral.Vertices.Count(vertex => Math.Abs(vertex.X) > 1e-6) > 400,
        "X must vary significantly along the curve.");
    Require(spiral.Vertices.Count(vertex => Math.Abs(vertex.Y) > 1e-6) > 400,
        "Y must vary significantly along the curve.");
    Require(spiral.Vertices.Count(vertex => Math.Abs(vertex.Z) > 1e-6) > 400,
        "Z must vary significantly along the curve.");
    Require(spiral.Vertices.Count(vertex => Math.Abs(vertex.W) > 1e-6) > 400,
        "W must vary significantly along the curve.");

    for (var index = 0; index < spiral.Edges.Count; index++)
    {
        Require(spiral.Edges[index] == new Edge(index, index + 1),
            "Spiral edges must connect only consecutive parameter samples.");
    }

    var custom = parameters with
    {
        R1 = 1.4,
        R2 = 0.7,
        K = 1.5,
        SampleCount = 101,
        TStart = -Math.PI,
        TEnd = Math.PI
    };
    var customSpiral = generator.Generate(custom);
    Require(customSpiral.Vertices.Count == 101 && customSpiral.Edges.Count == 100,
        "Custom spiral sampling parameters must change generated geometry.");
    RequireNear(customSpiral.Vertices[50].X, custom.R1, 1e-12,
        "The midpoint sample at t=0 must reflect custom r1.");
    RequireNear(customSpiral.Vertices[50].Z, custom.R2, 1e-12,
        "The midpoint sample at t=0 must reflect custom r2.");

    RequireThrows<ArgumentOutOfRangeException>(
        () => generator.Generate(parameters with { SampleCount = 1 }),
        "A curve needs at least two samples.");
    RequireValidTopology(spiral);
}

static void CheckCurvePlayback()
{
    var playback = new CurvePlayback4D(totalSampleCount: 600, durationSeconds: 4.0);
    Require(playback.VisibleSampleCount == 600 && !playback.IsPlaying,
        "A spiral must initially be fully visible.");

    playback.Reset();
    Require(playback.VisibleSampleCount == 1 && playback.Progress == 0.0,
        "Reset Curve must return to the first sample.");
    playback.Play();
    playback.Update(2.0);
    Require(playback.IsPlaying && playback.VisibleSampleCount == 300,
        "Half the draw duration must reveal approximately half the samples.");
    playback.Update(2.0);
    Require(!playback.IsPlaying && playback.VisibleSampleCount == 600,
        "Playback must stop exactly at the complete curve.");

    playback.Play();
    Require(playback.IsPlaying && playback.VisibleSampleCount == 1,
        "Play Curve at the end must restart from P0.");
    playback.SetTotalSampleCount(200, showComplete: true);
    Require(!playback.IsPlaying && playback.VisibleSampleCount == 200,
        "Regeneration must update playback to the new complete sample set.");

    var curveDisplay = new DisplayOptions(
        showCells: false,
        showEdges: true,
        showVertices: false,
        showDirection: true);
    Require(!curveDisplay.ShowCells && curveDisplay.ShowEdges &&
        !curveDisplay.ShowVertices && curveDisplay.ShowDirection,
        "Curve defaults must be Curve ON, Points OFF, Direction ON.");
}

static void CheckCommonGeometryProjection()
{
    IGeometry4D[] geometries =
    [
        new Tesseract4D(),
        new Hypersphere4D(),
        new Simplex4D(),
        new IrregularPolytope4D(),
        new Spiral4DGenerator().Generate(SpiralParameters.Default)
    ];
    var pipeline = new WireframeProjectionPipeline4D();
    var camera = new Camera4D();
    var projector = new PerspectiveProjector4D();

    foreach (var geometry in geometries)
    {
        var sceneObject = new SceneObject4D(geometry);
        var initial = pipeline.Project(geometry, sceneObject.Transform, camera, projector);
        Require(initial.VisibleVertexCount == geometry.Vertices.Count,
            $"Default camera must safely project every {geometry.Name} vertex.");
        Require(initial.VisibleEdgeCount == geometry.Edges.Count,
            $"Default camera must safely project every {geometry.Name} edge.");

        sceneObject.Transform.Rotate(RotationPlane4D.XW, 0.37);
        var rotated = pipeline.Project(geometry, sceneObject.Transform, camera, projector);
        Require(initial.Vertices.Zip(rotated.Vertices)
            .Any(pair => pair.First.Position != pair.Second.Position),
            $"The shared XW transform must visibly change {geometry.Name}.");
    }

    var spiral = new Spiral4DGenerator().Generate(SpiralParameters.Default);
    var spiralTransform = new Transform4D();
    spiralTransform.Rotate(RotationPlane4D.XW, Math.PI / 2.0);
    var projectedSpiral = pipeline.Project(spiral, spiralTransform, camera, projector);
    RequireNear(projectedSpiral.Vertices[0].SourceW, 0.0, 1e-12,
        "Projection metadata must preserve the curve's original local W.");
    RequireNear(projectedSpiral.Vertices[0].WorldW, spiral.Parameters.R1, 1e-12,
        "Projection metadata must expose transformed world W for curve coloring.");

    var firstObject = new SceneObject4D(geometries[0]);
    var secondObject = new SceneObject4D(geometries[1]);
    firstObject.DisplayOptions.ToggleEdges();
    firstObject.Transform.MoveWorld(new Vector4D(1.0, 0.0, 0.0, 0.0));
    Require(secondObject.DisplayOptions.ShowEdges,
        "Scene objects must keep independent display state.");
    Require(secondObject.Transform.Position == Vector4D.Zero,
        "Scene objects must keep independent 4D transforms.");
}

static void RequireValidTopology(IGeometry4D geometry)
{
    foreach (var edge in geometry.Edges)
    {
        Require(edge.Start >= 0 && edge.Start < geometry.Vertices.Count &&
            edge.End >= 0 && edge.End < geometry.Vertices.Count &&
            edge.Start != edge.End,
            $"{geometry.Name} contains an invalid edge index.");
    }

    foreach (var face in geometry.Faces)
    {
        Require(face.VertexIndices.All(index => index >= 0 && index < geometry.Vertices.Count),
            $"{geometry.Name} contains an invalid face index.");
    }
}

static void RequireEachCellFaceSharedTwice(IGeometry4D geometry)
{
    var memberships = geometry.Cells
        .SelectMany(cell => cell.Faces)
        .GroupBy(face => string.Join(",", face.VertexIndices.OrderBy(index => index)))
        .ToDictionary(group => group.Key, group => group.Count());
    Require(memberships.Count == geometry.Faces.Count,
        $"{geometry.Name} cell faces must match its unique face collection.");
    Require(memberships.Values.All(count => count == 2),
        $"Every {geometry.Name} boundary face must be shared by two 3D cells.");
}

static void CheckReferenceGrid()
{
    var grid = new ReferenceGrid4D();
    Require(grid.Vertices.Count == 42, "The default reference grid must have 42 vertices.");
    Require(grid.Edges.Count == 21, "The default reference grid must have 21 lines.");
    Require(grid.Edges.Count(edge => edge.Kind == EdgeKind.Grid) == 12,
        "Four non-origin W layers must contribute twelve minor grid lines.");
    Require(grid.Edges.Count(edge => edge.Kind == EdgeKind.AxisW) == 6,
        "The grid must have six offset W-parallel rails.");

    foreach (var edge in grid.Edges)
    {
        var start = grid.Vertices[edge.Start];
        var end = grid.Vertices[edge.End];
        var changedCoordinates =
            Different(start.X, end.X) +
            Different(start.Y, end.Y) +
            Different(start.Z, end.Z) +
            Different(start.W, end.W);

        Require(changedCoordinates == 1,
            "Every reference line must follow exactly one true 4D coordinate axis.");

        if (edge.Kind == EdgeKind.AxisW)
        {
            Require(start.W != end.W, "Every W rail must vary in W.");
            Require(start.X != 0.0 || start.Y != 0.0 || start.Z != 0.0,
                "W rails must be offset because the central W axis projects to one point.");
        }
    }

    var pipeline = new WireframeProjectionPipeline4D();
    var camera = new Camera4D();
    var transform = new Transform4D();
    var projector = new PerspectiveProjector4D();
    var projected = pipeline.Project(
        grid.Vertices,
        grid.Edges,
        transform,
        camera,
        projector);

    Require(projected.VisibleVertexCount == 42,
        "The default grid must project all vertices through the normal 4D pipeline.");
    Require(projected.VisibleEdgeCount == 21,
        "The default grid must project all lines through the normal 4D pipeline.");

    camera.MoveWorld(new Vector4D(0.5, 0.0, 0.0, 0.0));
    var afterCameraMove = pipeline.Project(
        grid.Vertices,
        grid.Edges,
        transform,
        camera,
        projector);

    Require(afterCameraMove.Vertices[0].Position != projected.Vertices[0].Position,
        "Moving Camera4D must change the grid through the same projection pipeline.");
}

static void CheckPlaneRotations()
{
    var source = new Vector4D(1.0, 2.0, 3.0, 4.0);

    foreach (var plane in Enum.GetValues<RotationPlane4D>())
    {
        var rotation = Rotation4D.Identity.WithAddedAngle(plane, 0.713);
        var rotated = rotation.Apply(source);
        RequireNear(rotated.Length, source.Length, 1e-12, $"{plane} must preserve vector length.");
    }

    var quarterTurn = Rotation4D.Identity.WithAddedAngle(RotationPlane4D.XW, Math.PI / 2.0);
    var xAxis = quarterTurn.Apply(new Vector4D(1.0, 0.0, 0.0, 0.0));
    RequireNear(xAxis.X, 0.0, 1e-12, "XW quarter turn must remove the X component.");
    RequireNear(xAxis.W, 1.0, 1e-12, "XW quarter turn must rotate +X toward +W.");
}

static void CheckRotationInverse()
{
    var rotation = new Rotation4D(0.1, -0.2, 0.3, -0.4, 0.5, -0.6);
    var source = new Vector4D(1.25, -2.5, 0.75, 3.0);
    var restored = rotation.ApplyInverse(rotation.Apply(source));

    RequireNear(restored.X, source.X, 1e-12, "Inverse rotation must restore X.");
    RequireNear(restored.Y, source.Y, 1e-12, "Inverse rotation must restore Y.");
    RequireNear(restored.Z, source.Z, 1e-12, "Inverse rotation must restore Z.");
    RequireNear(restored.W, source.W, 1e-12, "Inverse rotation must restore W.");
}

static void CheckCameraSpace()
{
    var camera = new Camera4D();
    var cameraPoint = camera.WorldToCameraSpace(new Vector4D(1.0, 2.0, 3.0, 0.0));

    RequireNear(cameraPoint.X, 1.0, 1e-12, "Camera-space X mismatch.");
    RequireNear(cameraPoint.Y, 2.0, 1e-12, "Camera-space Y mismatch.");
    RequireNear(cameraPoint.Z, 3.0, 1e-12, "Camera-space Z mismatch.");
    RequireNear(cameraPoint.W, 4.0, 1e-12, "Default camera must see the origin at W depth four.");
}

static void CheckPerspectiveProjection()
{
    var projector = new PerspectiveProjector4D(focalDistance: 2.0, nearPlane: 0.1);
    var projected = projector.TryProject(new Vector4D(1.0, 2.0, 3.0, 4.0), out var point);

    Require(projected, "A point in front of the camera must project.");
    RequireNear(point.X, 0.5, 1e-12, "Projected X mismatch.");
    RequireNear(point.Y, 1.0, 1e-12, "Projected Y mismatch.");
    RequireNear(point.Z, 1.5, 1e-12, "Projected Z mismatch.");
}

static void CheckProjectionSafety()
{
    var projector = new PerspectiveProjector4D(focalDistance: 2.0, nearPlane: 0.1);

    Require(!projector.TryProject(new Vector4D(1.0, 1.0, 1.0, 0.1), out _),
        "A point on the near plane must be rejected.");
    Require(!projector.TryProject(new Vector4D(1.0, 1.0, 1.0, 0.0), out _),
        "A point at the perspective singularity must be rejected.");
    Require(!projector.TryProject(new Vector4D(1.0, 1.0, 1.0, -2.0), out _),
        "A point behind the camera must be rejected.");
    Require(!projector.TryProject(new Vector4D(double.NaN, 1.0, 1.0, 2.0), out _),
        "A non-finite point must be rejected.");

    var pipeline = new WireframeProjectionPipeline4D();
    var wireframe = pipeline.Project(
        new Tesseract4D(),
        new Transform4D(),
        new Camera4D(),
        projector);

    Require(wireframe.VisibleVertexCount == 16, "The default tesseract must be fully projectable.");
    Require(wireframe.VisibleEdgeCount == 32, "The default tesseract must expose all edges.");
    Require(wireframe.Vertices.Count(vertex => vertex.SourceW < 0.0) == 8,
        "The projected representation must retain eight source W- vertices.");
    Require(wireframe.Vertices.Count(vertex => vertex.SourceW > 0.0) == 8,
        "The projected representation must retain eight source W+ vertices.");

    var cameraInsideTesseract = new Camera4D();
    cameraInsideTesseract.MoveWorld(new Vector4D(0.0, 0.0, 0.0, 4.0));
    var partiallyVisible = pipeline.Project(
        new Tesseract4D(),
        new Transform4D(),
        cameraInsideTesseract,
        projector);

    Require(partiallyVisible.VisibleVertexCount == 8,
        "A camera at W = 0 must safely reject the eight vertices behind it.");
    Require(partiallyVisible.VisibleEdgeCount == 12,
        "Only the fully projectable cube face must remain when the camera is at W = 0.");

    cameraInsideTesseract.MoveWorld(new Vector4D(0.0, 0.0, 0.0, 2.0));
    var fullyBehind = pipeline.Project(
        new Tesseract4D(),
        new Transform4D(),
        cameraInsideTesseract,
        projector);

    Require(fullyBehind.VisibleVertexCount == 0,
        "A tesseract fully behind Camera4D must produce no visible vertices without failing.");
    Require(fullyBehind.VisibleEdgeCount == 0,
        "A tesseract fully behind Camera4D must produce no visible edges without failing.");
}

static void CheckTransformationAnimation()
{
    var source = new Vector4D(1.0, 2.0, 3.0, 4.0);
    var rotatedResults = new Vector4D[Enum.GetValues<RotationPlane4D>().Length];
    var resultIndex = 0;

    foreach (var plane in Enum.GetValues<RotationPlane4D>())
    {
        var transform = new Transform4D();
        var animator = new TransformationAnimator4D();

        Require(animator.TryStartRotation(plane), $"{plane} animation must start while idle.");
        Require(!animator.TryStartRotation(RotationPlane4D.XW),
            "A second request must be ignored while an animation is active.");

        animator.Update(0.25, transform);
        RequireNear(animator.CurrentRotationDegrees, 14.0625, 1e-12,
            $"{plane} debug angle must report the same eased progress used by the transform.");
        RequireNear(
            transform.Rotation.GetAngle(plane),
            TransformationAnimator4D.QuarterTurnRadians * 0.15625,
            1e-12,
            $"{plane} must use smooth-step progress at quarter duration.");

        animator.Update(0.25, transform);
        Require(animator.IsActive, $"{plane} animation must remain active at half duration.");
        RequireNear(animator.CurrentRotationDegrees, 45.0, 1e-12,
            $"{plane} animation must report its intermediate angle.");
        RequireNear(transform.Rotation.GetAngle(plane), Math.PI / 4.0, 1e-12,
            $"{plane} must reach 45 degrees halfway through the smooth animation.");

        animator.Update(0.5, transform);
        Require(!animator.IsActive, $"{plane} animation must finish after one second.");
        RequireNear(transform.Rotation.GetAngle(plane), Math.PI / 2.0, 1e-12,
            $"{plane} animation must add exactly 90 degrees.");
        rotatedResults[resultIndex++] = transform.Rotation.Apply(source);
    }

    for (var left = 0; left < rotatedResults.Length; left++)
    {
        for (var right = left + 1; right < rotatedResults.Length; right++)
        {
            Require(rotatedResults[left] != rotatedResults[right],
                "Each of the six plane rotations must produce a distinct result for a generic vector.");
        }
    }

    var repeatedTransform = new Transform4D();
    var repeatedAnimator = new TransformationAnimator4D();
    for (var turn = 0; turn < 3; turn++)
    {
        Require(repeatedAnimator.TryStartRotation(RotationPlane4D.XW),
            "A completed rotation must allow the next request.");
        repeatedAnimator.Update(1.0, repeatedTransform);
    }

    RequireNear(repeatedTransform.Rotation.XW, 3.0 * Math.PI / 2.0, 1e-12,
        "Three XW button requests must accumulate to +270 degrees.");

    var scaleTransform = new Transform4D();
    var scaleAnimator = new TransformationAnimator4D();
    Require(scaleAnimator.TryStartUniformScale(1.25), "Scale-up animation must start.");
    scaleAnimator.Update(0.5, scaleTransform);
    Require(scaleTransform.Scale > 1.0 && scaleTransform.Scale < 1.25,
        "Uniform scale must have a visible intermediate value.");
    scaleAnimator.Update(0.5, scaleTransform);
    RequireNear(scaleTransform.Scale, 1.25, 1e-12, "Scale-up must finish at x1.25.");
    RequireVectorNear(
        scaleTransform.TransformPoint(source),
        source * 1.25,
        1e-12,
        "Uniform scale must affect X, Y, Z and W.");

    Require(scaleAnimator.TryStartUniformScale(0.8), "Scale-down animation must start after scale-up.");
    scaleAnimator.Update(1.0, scaleTransform);
    RequireNear(scaleTransform.Scale, 1.0, 1e-12,
        "Scale-down x0.8 must exactly undo scale-up x1.25.");

    var translationOffsets = new[]
    {
        new Vector4D(0.75, 0.0, 0.0, 0.0),
        new Vector4D(-0.75, 0.0, 0.0, 0.0),
        new Vector4D(0.0, 0.75, 0.0, 0.0),
        new Vector4D(0.0, -0.75, 0.0, 0.0),
        new Vector4D(0.0, 0.0, 0.75, 0.0),
        new Vector4D(0.0, 0.0, -0.75, 0.0),
        new Vector4D(0.0, 0.0, 0.0, 0.75),
        new Vector4D(0.0, 0.0, 0.0, -0.75)
    };

    foreach (var offset in translationOffsets)
    {
        var transform = new Transform4D();
        var animator = new TransformationAnimator4D();
        Require(animator.TryStartTranslation(offset), "Translation animation must start.");
        animator.Update(0.5, transform);
        RequireVectorNear(transform.Position, offset * 0.5, 1e-12,
            "Translation must interpolate through a visible midpoint.");
        animator.Update(0.5, transform);
        RequireVectorNear(transform.Position, offset, 1e-12,
            "Translation must finish at the requested 4D offset.");
    }

    var resetTransform = new Transform4D();
    var resetAnimator = new TransformationAnimator4D();
    resetAnimator.TryStartRotation(RotationPlane4D.YW);
    resetAnimator.Update(0.25, resetTransform);
    resetAnimator.Cancel();
    resetTransform.Reset();
    resetAnimator.Update(2.0, resetTransform);
    Require(resetTransform.Position == Vector4D.Zero, "Reset must restore the object position.");
    Require(resetTransform.Rotation == Rotation4D.Identity, "Reset must restore identity rotation.");
    RequireNear(resetTransform.Scale, 1.0, 1e-12, "Reset must restore unit scale.");
}

static void CheckUiButtonStates()
{
    var button = new UiButton("XW +90", TransformationCommand.RotateXW);
    button.SetBounds(new Rectangle(10, 20, 100, 30));
    var outside = MouseAt(0, 0, 0);
    var insideReleased = MouseAt(30, 30, 0);
    var insidePressed = MouseAt(30, 30, 0, ButtonState.Pressed);

    Require(!button.Update(insideReleased, outside, isEnabled: true),
        "Hovering must not trigger a click.");
    Require(button.IsHovered && !button.IsPressed, "Button must expose hover state.");
    Require(!button.Update(insidePressed, insideReleased, isEnabled: true),
        "Pressing must wait for release before clicking.");
    Require(button.IsPressed, "Button must expose pressed state.");
    Require(button.Update(insideReleased, insidePressed, isEnabled: true),
        "Release over the pressed button must produce one click.");

    button.SetActive(true);
    Require(button.IsActive, "Button must expose active-animation state.");
    Require(!button.Update(insidePressed, insideReleased, isEnabled: false),
        "A disabled button must ignore rapid input during another animation.");
    Require(!button.IsHovered && !button.IsPressed,
        "Disabled buttons must not retain hover or pressed feedback.");
}

static void CheckDisplayLayerState()
{
    var options = new DisplayOptions();
    Require(options.ShowGrid && options.ShowAxes && options.ShowCells &&
        options.ShowEdges && options.ShowVertices,
        "Every educational display layer must be enabled by default.");

    options.ToggleGrid();
    options.ToggleAxes();
    options.ToggleCells();
    options.ToggleEdges();
    options.ToggleVertices();
    Require(!options.ShowGrid && !options.ShowAxes && !options.ShowCells &&
        !options.ShowEdges && !options.ShowVertices,
        "Every display layer must toggle independently.");
    Require(!options.ShowDirection,
        "Direction markers must remain opt-in for non-curve geometry.");
    options.ToggleDirection();
    Require(options.ShowDirection, "Direction marker state must toggle independently.");

    Require(VisualizationPalette.CellColorCount == 8,
        "The cell legend and renderer must share exactly eight colors.");
    Require(
        VisualizationPalette.CellSurfaceAlpha >= 0.15f &&
        VisualizationPalette.CellSurfaceAlpha <= 0.30f,
        "Cell alpha must remain in the intended translucent range.");
    Require(Enumerable.Range(0, VisualizationPalette.CellColorCount)
        .Select(VisualizationPalette.CellColor)
        .Distinct()
        .Count() == 8,
        "All eight cell colors must be distinct.");
    Require(new[]
    {
        VisualizationPalette.EdgeX,
        VisualizationPalette.EdgeY,
        VisualizationPalette.EdgeZ,
        VisualizationPalette.EdgeW
    }.Distinct().Count() == 4, "X/Y/Z/W edge colors must be distinct.");
    Require(VisualizationPalette.VertexNegativeW != VisualizationPalette.VertexPositiveW,
        "W- and W+ vertex markers must use different colors.");
}

static void CheckInputMapping()
{
    var controller = new SandboxInputController();
    var objectTransform = new Transform4D();
    var camera4D = new Camera4D();
    var projector = new PerspectiveProjector4D();
    var camera3D = new OrbitCamera3D();
    var oneSecond = new GameTime(TimeSpan.FromSeconds(1.0), TimeSpan.FromSeconds(1.0));
    var releasedMouse = MouseAt(0, 0, 0, ButtonState.Released);

    controller.Update(
        oneSecond,
        isActive: true,
        new KeyboardState(Keys.Y, Keys.I, Keys.P, Keys.H, Keys.K, Keys.OemSemicolon),
        releasedMouse,
        default,
        objectTransform,
        camera4D,
        projector,
        camera3D);

    RequireNear(objectTransform.Rotation.XY, Math.PI / 2.0, 1e-12,
        "Y must rotate the object in XY.");
    RequireNear(objectTransform.Rotation.XZ, Math.PI / 2.0, 1e-12,
        "I must rotate the object in XZ.");
    RequireNear(objectTransform.Rotation.XW, Math.PI / 2.0, 1e-12,
        "P must rotate the object in XW.");
    RequireNear(objectTransform.Rotation.YZ, Math.PI / 2.0, 1e-12,
        "H must rotate the object in YZ.");
    RequireNear(objectTransform.Rotation.YW, Math.PI / 2.0, 1e-12,
        "K must rotate the object in YW.");
    RequireNear(objectTransform.Rotation.ZW, Math.PI / 2.0, 1e-12,
        "Semicolon must rotate the object in ZW.");

    controller.Update(
        oneSecond,
        isActive: true,
        new KeyboardState(Keys.LeftShift, Keys.P, Keys.K, Keys.OemSemicolon),
        releasedMouse,
        default,
        objectTransform,
        camera4D,
        projector,
        camera3D);

    RequireNear(camera4D.Orientation.XW, Math.PI / 2.0, 1e-12,
        "Shift+P must rotate Camera4D in XW.");
    RequireNear(camera4D.Orientation.YW, Math.PI / 2.0, 1e-12,
        "Shift+K must rotate Camera4D in YW.");
    RequireNear(camera4D.Orientation.ZW, Math.PI / 2.0, 1e-12,
        "Shift+Semicolon must rotate Camera4D in ZW.");

    var rotationBeforeCameraMovement = objectTransform.Rotation;

    controller.Update(
        oneSecond,
        isActive: true,
        new KeyboardState(Keys.Q, Keys.W, Keys.E, Keys.R),
        releasedMouse,
        default,
        objectTransform,
        camera4D,
        projector,
        camera3D);

    RequireNear(camera4D.Position.X, 2.0, 1e-12, "Q must move Camera4D along +X.");
    RequireNear(camera4D.Position.Y, 2.0, 1e-12, "W must move Camera4D along +Y.");
    RequireNear(camera4D.Position.Z, 2.0, 1e-12, "E must move Camera4D along +Z.");
    RequireNear(camera4D.Position.W, -2.0, 1e-12, "R must move Camera4D along +W.");
    Require(objectTransform.Rotation == rotationBeforeCameraMovement,
        "Camera movement keys must not also rotate the object.");

    controller.Update(
        oneSecond,
        isActive: true,
        new KeyboardState(Keys.A, Keys.S, Keys.D, Keys.F),
        releasedMouse,
        default,
        objectTransform,
        camera4D,
        projector,
        camera3D);

    Require(camera4D.Position == Camera4D.DefaultPosition,
        "A/S/D/F must move Camera4D in the negative X/Y/Z/W directions.");

    controller.Update(
        oneSecond,
        isActive: true,
        new KeyboardState(),
        MouseAt(100, 100, 0, rightButton: ButtonState.Pressed),
        default,
        objectTransform,
        camera4D,
        projector,
        camera3D);
    controller.Update(
        oneSecond,
        isActive: true,
        new KeyboardState(),
        MouseAt(140, 120, 0, rightButton: ButtonState.Pressed),
        default,
        objectTransform,
        camera4D,
        projector,
        camera3D);

    RequireNear(objectTransform.Position.X, 0.4, 1e-12,
        "Right-button horizontal drag must move the object along X.");
    RequireNear(objectTransform.Position.Y, -0.2, 1e-12,
        "Right-button downward drag must move the object along -Y.");

    controller.Update(
        oneSecond,
        isActive: true,
        new KeyboardState(),
        MouseAt(140, 120, 0, middleButton: ButtonState.Pressed),
        default,
        objectTransform,
        camera4D,
        projector,
        camera3D);
    controller.Update(
        oneSecond,
        isActive: true,
        new KeyboardState(),
        MouseAt(170, 90, 0, middleButton: ButtonState.Pressed),
        default,
        objectTransform,
        camera4D,
        projector,
        camera3D);

    RequireNear(objectTransform.Position.Z, 0.3, 1e-12,
        "Middle-button horizontal drag must move the object along Z.");
    RequireNear(objectTransform.Position.W, 0.3, 1e-12,
        "Middle-button upward drag must move the object along +W.");

    var initialYaw = camera3D.Yaw;
    var initialPitch = camera3D.Pitch;
    controller.Update(
        oneSecond,
        isActive: true,
        new KeyboardState(),
        MouseAt(100, 100, 0, ButtonState.Pressed),
        default,
        objectTransform,
        camera4D,
        projector,
        camera3D);
    controller.Update(
        oneSecond,
        isActive: true,
        new KeyboardState(),
        MouseAt(150, 120, 0, ButtonState.Pressed),
        default,
        objectTransform,
        camera4D,
        projector,
        camera3D);

    Require(camera3D.Yaw != initialYaw && camera3D.Pitch != initialPitch,
        "A left-button mouse drag must orbit the 3D view.");

    var initialDistance = camera3D.Distance;
    controller.Update(
        oneSecond,
        isActive: true,
        new KeyboardState(),
        MouseAt(150, 120, 120, ButtonState.Released),
        default,
        objectTransform,
        camera4D,
        projector,
        camera3D);
    Require(camera3D.Distance < initialDistance, "A positive mouse wheel step must zoom in.");

    var positionBeforePanelInput = objectTransform.Position;
    controller.Update(
        oneSecond,
        isActive: true,
        new KeyboardState(),
        MouseAt(500, 100, 120, rightButton: ButtonState.Pressed),
        default,
        objectTransform,
        camera4D,
        projector,
        camera3D,
        allowMouseInput: false);
    controller.Update(
        oneSecond,
        isActive: true,
        new KeyboardState(),
        MouseAt(600, 100, 120, rightButton: ButtonState.Pressed),
        default,
        objectTransform,
        camera4D,
        projector,
        camera3D,
        allowMouseInput: false);
    controller.Update(
        oneSecond,
        isActive: true,
        new KeyboardState(),
        MouseAt(700, 100, 120, rightButton: ButtonState.Pressed),
        default,
        objectTransform,
        camera4D,
        projector,
        camera3D);
    Require(objectTransform.Position == positionBeforePanelInput,
        "Mouse input over the UI panel must not move the object or create a jump on exit.");

    objectTransform.MultiplyScale(2.0);

    controller.Update(
        oneSecond,
        isActive: true,
        new KeyboardState(Keys.Space),
        releasedMouse,
        default,
        objectTransform,
        camera4D,
        projector,
        camera3D);

    Require(objectTransform.Rotation == Rotation4D.Identity, "Space must reset object rotation.");
    Require(objectTransform.Position == Vector4D.Zero, "Space must reset object position.");
    RequireNear(objectTransform.Scale, 1.0, 1e-12, "Space must reset object scale.");
    Require(controller.ResetRequested, "The game loop must be notified so Space can cancel an animation.");
    Require(camera4D.Position == Camera4D.DefaultPosition, "Space must reset Camera4D position.");
    Require(camera4D.Orientation == Rotation4D.Identity, "Space must reset Camera4D orientation.");
    RequireNear(projector.FocalDistance, PerspectiveProjector4D.DefaultFocalDistance, 1e-12,
        "Space must reset focal distance.");
    var defaultCamera3D = new OrbitCamera3D();
    RequireNear(camera3D.Yaw, defaultCamera3D.Yaw, 1e-12,
        "Space must reset 3D view yaw.");
    RequireNear(camera3D.Pitch, defaultCamera3D.Pitch, 1e-12,
        "Space must reset 3D view pitch.");
    RequireNear(camera3D.Distance, defaultCamera3D.Distance, 1e-12,
        "Space must reset 3D view zoom.");
}

static void CheckQuaternionAlgebra()
{
    var one = new Quaternion4D(1.0, 0.0, 0.0, 0.0);
    var i = new Quaternion4D(0.0, 1.0, 0.0, 0.0);
    var j = new Quaternion4D(0.0, 0.0, 1.0, 0.0);
    var k = new Quaternion4D(0.0, 0.0, 0.0, 1.0);

    RequireQuaternionNear(i * i, -1.0 * one, 1e-12, "i^2 must equal -1.");
    RequireQuaternionNear(j * j, -1.0 * one, 1e-12, "j^2 must equal -1.");
    RequireQuaternionNear(k * k, -1.0 * one, 1e-12, "k^2 must equal -1.");
    RequireQuaternionNear(i * j, k, 1e-12, "ij must equal k.");
    RequireQuaternionNear(j * i, -1.0 * k, 1e-12, "ji must equal -k.");
    Require(i * j != j * i, "Quaternion multiplication must be non-commutative.");

    var q = new Quaternion4D(1.5, -2.0, 0.25, 3.0);
    RequireQuaternionNear(q.Square(), q * q, 1e-12,
        "The optimized square formula must match quaternion multiplication.");
    RequireNear(q.SquaredMagnitude, 15.3125, 1e-12,
        "Squared magnitude must sum all four squared components.");
    RequireQuaternionNear(q + one, new Quaternion4D(2.5, -2.0, 0.25, 3.0), 1e-12,
        "Quaternion addition must be component-wise.");
}

static void CheckQuaternionJuliaIteration()
{
    var parameters = JuliaParameters.Default with
    {
        Constant = Quaternion4D.Zero,
        MaxIterations = 8,
        EscapeRadius = 2.0
    };
    var origin = QuaternionJuliaGenerator4D.Evaluate(Vector4D.Zero, parameters);
    Require(origin.IsBounded && origin.Iterations == parameters.MaxIterations,
        "q0=0 with C=0 must remain bounded.");

    var outside = QuaternionJuliaGenerator4D.Evaluate(
        new Vector4D(2.0, 0.0, 0.0, 0.0),
        parameters);
    Require(!outside.IsBounded && outside.Iterations == 1,
        "q0=2 must escape after the first q^2 iteration for radius 2.");

    var nonFinite = QuaternionJuliaGenerator4D.Evaluate(
        new Vector4D(double.MaxValue, 0.0, 0.0, 0.0),
        parameters);
    Require(!nonFinite.IsBounded,
        "Overflowing samples must be classified as escaped instead of propagating NaN.");
}

static void CheckIncrementalFractalGeneration()
{
    var parameters = JuliaParameters.Default with
    {
        Resolution = 4,
        MaxIterations = 8,
        MinimumCoordinate = -1.0,
        MaximumCoordinate = 1.0
    };
    var generator = new QuaternionJuliaGenerator4D();
    var generation = generator.Start(parameters);

    generation.ProcessBatch(17);
    Require(generation.ProcessedSampleCount == 17 && !generation.IsCompleted,
        "A batch must advance only the requested number of grid samples.");
    Require(generation.Progress > 0.0 && generation.Progress < 1.0,
        "Incremental generation must expose intermediate progress.");

    while (!generation.IsCompleted)
    {
        generation.ProcessBatch(31);
    }

    var fractal = generation.CreateResult();
    Require(fractal.Samples.Count == 256,
        "A resolution-4 4D grid must contain 4^4 = 256 samples.");
    Require(fractal.Vertices.Count == fractal.Samples.Count && fractal.Edges.Count == 0,
        "The first fractal representation must be a point cloud without invented edges.");
    Require(fractal.Vertices.Contains(new Vector4D(-1.0, -1.0, -1.0, -1.0)) &&
        fractal.Vertices.Contains(new Vector4D(1.0, 1.0, 1.0, 1.0)),
        "The 4D grid must include both endpoints on all four axes.");

    var pipeline = new WireframeProjectionPipeline4D();
    var projected = pipeline.Project(
        fractal,
        new Transform4D(),
        new Camera4D(),
        new PerspectiveProjector4D());
    Require(projected.Vertices.Count == fractal.Vertices.Count,
        "Every fractal sample must pass through the common 4D projection pipeline.");

    var cancelled = generator.Start(parameters);
    cancelled.ProcessBatch(5);
    cancelled.Cancel();
    cancelled.ProcessBatch(100);
    Require(cancelled.IsCancelled && cancelled.ProcessedSampleCount == 5,
        "Cancel must stop later batches without discarding the current displayed geometry.");
    RequireThrows<InvalidOperationException>(() => _ = cancelled.CreateResult(),
        "A cancelled partial generation must not be published as complete geometry.");
}

static void CheckFractalPresetsAndDisplayState()
{
    var generator = new QuaternionJuliaGenerator4D();
    var boundedCounts = new List<int>();
    foreach (var constant in new[]
    {
        JuliaParameters.Preset1,
        JuliaParameters.Preset2,
        JuliaParameters.Preset3
    })
    {
        var parameters = JuliaParameters.Default with
        {
            Constant = constant,
            Resolution = 8,
            MaxIterations = 24
        };
        var generation = generator.Start(parameters);
        while (!generation.IsCompleted)
        {
            generation.ProcessBatch(257);
        }

        var result = generation.CreateResult();
        boundedCounts.Add(result.BoundedPointCount);
    }

    Require(boundedCounts.All(count => count > 0 && count < 4096),
        $"Each preset must contain bounded and escaped samples; bounded counts were " +
        string.Join(", ", boundedCounts) + ".");
    Require(boundedCounts.Distinct().Count() >= 2,
        "The presets must not all produce the same sampled structure.");

    var settings = new FractalVisualizationSettings();
    Require(settings.ColorMode == FractalColorMode.EscapeIterations,
        "Iteration coloring must be the informative default.");
    settings.SetColorMode(FractalColorMode.WCoordinate);
    settings.ToggleWSlice();
    settings.AdjustSliceW(10.0, -1.5, 1.5);
    settings.CyclePointSize();
    settings.CyclePointSize();
    Require(settings.ColorMode == FractalColorMode.WCoordinate &&
        settings.ShowWSlice &&
        settings.SliceW == 1.5 &&
        settings.PointSize == 3,
        "Fractal display controls must update and clamp their state.");
    settings.Reset();
    Require(settings.ColorMode == FractalColorMode.EscapeIterations &&
        !settings.ShowWSlice &&
        settings.SliceW == 0.0 &&
        settings.PointSize == 1,
        "Fractal display reset must restore all defaults.");
}

static void CheckPhysicsIntegration()
{
    var body = new PhysicsBody4D(
        id: 1,
        position: Vector4D.Zero,
        velocity: new Vector4D(1.0, 2.0, 3.0, 4.0));
    body.Integrate(1.0);

    RequireVectorNear(body.Position, new Vector4D(1.0, 2.0, 3.0, 4.0), 1e-12,
        "Zero-acceleration integration must move in all four velocity components.");
    RequireVectorNear(body.Velocity, new Vector4D(1.0, 2.0, 3.0, 4.0), 1e-12,
        "Zero acceleration must preserve the complete 4D velocity.");
    RequireNear(body.Velocity.LengthSquared, 30.0, 1e-12,
        "4D speed squared must include vx, vy, vz, and vw.");
    RequireNear(body.KineticEnergy, 15.0, 1e-12,
        "Kinetic energy must be 0.5*m*|v|^2.");
}

static void CheckPhysicsGravityAndWMovement()
{
    var yWorld = new PhysicsWorld4D(fixedDeltaTime: 1.0);
    var falling = yWorld.AddBody(Vector4D.Zero, Vector4D.Zero);
    yWorld.SetGravity(new Vector4D(0.0, -9.8, 0.0, 0.0));
    Require(yWorld.StepOnce(), "An enabled world must execute one manual fixed step.");
    RequireNear(falling.Velocity.Y, -9.8, 1e-12,
        "Y gravity must update Y velocity after one second.");
    RequireNear(falling.Position.Y, -4.9, 1e-12,
        "Leapfrog must reproduce constant-acceleration Y motion.");

    var wWorld = new PhysicsWorld4D(fixedDeltaTime: 0.5);
    wWorld.ToggleCollisions();
    var wBody = wWorld.AddBody(
        new Vector4D(0.0, 0.0, 0.0, 4.0),
        new Vector4D(0.0, 0.0, 0.0, 5.0));
    wWorld.SetGravity(new Vector4D(0.0, 0.0, 0.0, -9.8));
    wWorld.StepOnce();
    RequireNear(wBody.Velocity.W, 0.1, 1e-12,
        "W gravity must change only the true fourth velocity component.");
    RequireNear(wBody.Position.W, 5.275, 1e-12,
        "Leapfrog must reproduce constant-acceleration W motion.");
    RequireNear(wBody.Position.X, 0.0, 1e-12,
        "Pure W movement must not leak into X through projection or integration.");
}

static void CheckHyperplaneCollision()
{
    var plane = Hyperplane4D.WZero;
    var elastic = new PhysicsBody4D(
        1,
        new Vector4D(0.0, 0.0, 0.0, 0.1),
        new Vector4D(1.0, 0.0, 0.0, -2.0));
    var energyBefore = elastic.KineticEnergy;
    elastic.Integrate(0.1);
    Require(plane.ResolveCollision(elastic, restitution: 1.0),
        "Crossing from W>0 to W<0 must hit the W=0 hyperplane.");
    RequireNear(elastic.Position.W, 0.0, 1e-12,
        "Penetration correction must return the particle to W=0.");
    RequireNear(elastic.Velocity.W, 2.0, 1e-12,
        "A perfectly elastic W collision must reverse W velocity.");
    RequireNear(elastic.Velocity.X, 1.0, 1e-12,
        "Hyperplane response must preserve tangential X velocity.");
    RequireNear(elastic.KineticEnergy, energyBefore, 1e-12,
        "Restitution 1 must preserve kinetic energy for a plane reflection.");

    var damped = new PhysicsBody4D(
        2,
        new Vector4D(0.0, 0.0, 0.0, -0.1),
        new Vector4D(0.0, 0.0, 0.0, -2.0));
    Require(plane.ResolveCollision(damped, restitution: 0.8),
        "A penetrated particle must be resolved.");
    RequireNear(damped.Velocity.W, 1.6, 1e-12,
        "Restitution 0.8 must retain 80 percent of normal speed.");
    RequireNear(damped.KineticEnergy, 1.28, 1e-12,
        "Restitution below one must reduce kinetic energy.");

    var stopped = new PhysicsBody4D(
        3,
        new Vector4D(0.0, 0.0, 0.0, -0.1),
        new Vector4D(0.0, 0.0, 0.0, -2.0));
    plane.ResolveCollision(stopped, restitution: 0.0);
    RequireNear(stopped.Velocity.W, 0.0, 1e-12,
        "Restitution zero must remove inward normal velocity.");
}

static void CheckFixedTimestepPhysics()
{
    var world = new PhysicsWorld4D();
    world.SetGravity(Vector4D.Zero);
    var body = world.AddBody(Vector4D.Zero, new Vector4D(1.0, 0.0, 0.0, 0.0));

    Require(world.Update(1.0) == 0,
        "The debug-friendly default paused state must not advance automatically.");
    world.Play();
    Require(world.Update(world.FixedDeltaTime + 1e-12) == 1,
        "One fixed interval must execute exactly one physics step.");
    RequireNear(body.Position.X, world.FixedDeltaTime, 1e-12,
        "Rendering elapsed time must be quantized to the fixed physics interval.");

    world.AdjustTimeScale(+1);
    RequireNear(world.TimeScale, 2.0, 1e-12, "The next supported time scale must be 2x.");
    Require(world.Update(world.FixedDeltaTime + 1e-12) == 2,
        "A 2x time scale must execute two fixed steps for one render interval.");
    world.Pause();
    var pausedPosition = body.Position;
    Require(world.Update(1.0) == 0 && body.Position == pausedPosition,
        "Pause must stop automatic simulation without changing rendering time.");
    Require(world.StepOnce(), "STEP must execute while the enabled world is paused.");
    RequireNear(body.Position.X, pausedPosition.X + world.FixedDeltaTime, 1e-12,
        "STEP must execute exactly one fixed interval.");
}

static void CheckDeterministicParticleSpawning()
{
    var world = new PhysicsWorld4D();
    var velocity = new Vector4D(0.0, 0.0, 0.0, 5.0);
    Require(world.SpawnParticles(10, velocity) == 10,
        "SPAWN 10 must create ten particles below the safety cap.");
    var firstRun = world.Bodies.Select(body => body.Position).ToArray();
    Require(world.Bodies.All(body => body.Velocity == velocity),
        "Configured pure-W initial velocity must not receive random XYZ jitter.");

    world.Clear();
    world.SpawnParticles(10, velocity);
    Require(firstRun.SequenceEqual(world.Bodies.Select(body => body.Position)),
        "Clearing and spawning again must reproduce the same deterministic positions.");
}

static void CheckPhysicsHyperplaneVisualization()
{
    var grid = new HyperplaneGrid4D(extent: 2.0, coordinatesPerAxis: 5);
    Require(grid.Vertices.Count == 150 && grid.Edges.Count == 75,
        "A 5x5 lattice in each X/Y/Z direction must contain 75 finite line segments.");
    Require(grid.Vertices.All(vertex => vertex.W == 0.0),
        "Every collision-boundary visualization sample must lie on the true W=0 hyperplane.");

    foreach (var edge in grid.Edges)
    {
        var start = grid.Vertices[edge.Start];
        var end = grid.Vertices[edge.End];
        Require(ChangedCoordinateCount(start, end) == 1,
            "Each hyperplane lattice segment must follow one X/Y/Z tangent direction.");
    }

    var pipeline = new WireframeProjectionPipeline4D();
    var projected = pipeline.Project(
        grid.Vertices,
        grid.Edges,
        new Transform4D(),
        new Camera4D(),
        new PerspectiveProjector4D());
    Require(projected.Vertices.Count == grid.Vertices.Count &&
        projected.VisibleEdgeCount == grid.Edges.Count,
        "The W=0 lattice must use the common safe 4D projection pipeline.");
}

static void CheckFourDimensionalGravityLaw()
{
    var first = Vector4D.Zero;
    var second = new Vector4D(1.0, 2.0, 3.0, 4.0);
    RequireNear((second - first).LengthSquared, 30.0, 1e-12,
        "4D squared distance must include X, Y, Z, and W.");

    var directionTest = GravitySystem4D.CalculateAcceleration(
        first,
        second,
        sourceMass: 3.0,
        gravitationalConstant: 2.0,
        softening: 0.1);
    Require(directionTest.X > 0.0 && directionTest.Y > 0.0 &&
        directionTest.Z > 0.0 && directionTest.W > 0.0,
        "Gravitational acceleration must point from the target toward the source.");

    var pureW = GravitySystem4D.CalculateAcceleration(
        Vector4D.Zero,
        new Vector4D(0.0, 0.0, 0.0, 10.0),
        sourceMass: 5.0,
        gravitationalConstant: 1.0,
        softening: 0.1);
    RequireNear(pureW.X, 0.0, 1e-12, "A pure-W separation must not create X acceleration.");
    RequireNear(pureW.Y, 0.0, 1e-12, "A pure-W separation must not create Y acceleration.");
    RequireNear(pureW.Z, 0.0, 1e-12, "A pure-W separation must not create Z acceleration.");
    Require(pureW.W > 0.0, "A source at positive W must attract entirely toward positive W.");

    var numerical = GravitySystem4D.CalculateAcceleration(
        Vector4D.Zero,
        new Vector4D(4.0, 0.0, 0.0, 0.0),
        sourceMass: 1000.0,
        gravitationalConstant: 0.05,
        softening: 1e-6);
    RequireNear(numerical.Length, 0.05 * 1000.0 / Math.Pow(4.0, 3.0), 1e-10,
        "Far from negligible softening, acceleration magnitude must be G*M/R^3.");

    var coincident = GravitySystem4D.CalculateAcceleration(
        Vector4D.Zero,
        Vector4D.Zero,
        sourceMass: 1000.0,
        gravitationalConstant: 0.05,
        softening: 0.25);
    Require(coincident.IsFinite && coincident == Vector4D.Zero,
        "Softening must make coincident positions finite without an arbitrary force direction.");
}

static void CheckPairwiseGravitySymmetry()
{
    var world = new PhysicsWorld4D(fixedDeltaTime: 0.05);
    world.SetGravity(Vector4D.Zero);
    world.SetCollisionsEnabled(false);
    world.SetGravitationalConstant(0.4);
    world.SetGravitySoftening(0.2);
    var first = world.AddBody(
        new Vector4D(-1.0, 0.5, 0.0, -0.25),
        new Vector4D(0.1, 0.2, 0.3, 0.4),
        mass: 2.0);
    var second = world.AddBody(
        new Vector4D(2.0, -0.5, 1.0, 0.75),
        new Vector4D(-0.2, 0.1, -0.1, 0.05),
        mass: 3.0);
    world.SetMutualGravityEnabled(true);

    var firstForce = first.Acceleration * first.Mass;
    var secondForce = second.Acceleration * second.Mass;
    RequireVectorNear(firstForce + secondForce, Vector4D.Zero, 1e-12,
        "Pairwise internal gravitational forces must be equal and opposite.");

    var momentumBefore = (first.Velocity * first.Mass) + (second.Velocity * second.Mass);
    world.StepOnce();
    var momentumAfter = (first.Velocity * first.Mass) + (second.Velocity * second.Mass);
    RequireVectorNear(momentumAfter, momentumBefore, 1e-12,
        "One isolated pairwise step must preserve total 4D momentum to roundoff.");
}

static void CheckStaticCentralBodyAndFreeMotion()
{
    var gravityWorld = new PhysicsWorld4D(fixedDeltaTime: 0.1);
    gravityWorld.SetGravity(Vector4D.Zero);
    gravityWorld.SetCollisionsEnabled(false);
    gravityWorld.SetMutualGravityEnabled(true);
    var central = gravityWorld.AddBody(Vector4D.Zero, Vector4D.Zero, mass: 1000.0, isStatic: true);
    var orbiter = gravityWorld.AddBody(
        new Vector4D(4.0, 0.0, 0.0, 0.0),
        new Vector4D(0.0, 1.0, 0.0, 0.5));
    gravityWorld.StepOnce();
    Require(central.Position == Vector4D.Zero && central.Velocity == Vector4D.Zero,
        "A static central mass must create a field without integrating position or velocity.");
    Require(orbiter.Acceleration.X < 0.0,
        "An orbiter at positive X must accelerate toward a central body at the origin.");

    var freeWorld = new PhysicsWorld4D(fixedDeltaTime: 0.5);
    freeWorld.SetGravity(Vector4D.Zero);
    freeWorld.SetCollisionsEnabled(false);
    freeWorld.SetGravitationalConstant(0.0);
    freeWorld.SetMutualGravityEnabled(true);
    var freeBody = freeWorld.AddBody(
        new Vector4D(1.0, 2.0, 3.0, 4.0),
        new Vector4D(-1.0, 2.0, -3.0, 4.0));
    freeWorld.StepOnce();
    RequireVectorNear(freeBody.Position, new Vector4D(0.5, 3.0, 1.5, 6.0), 1e-12,
        "With external gravity zero and G=0, a body must move at constant 4D velocity.");
    RequireVectorNear(freeBody.Velocity, new Vector4D(-1.0, 2.0, -3.0, 4.0), 1e-12,
        "G=0 must not change velocity.");
}

static void CheckGravityLabTrajectoryAndDeterminism()
{
    var firstWorld = new PhysicsWorld4D(fixedDeltaTime: 0.05);
    var firstLab = new GravityLab4D(firstWorld);
    firstLab.ResetExperiment();
    Require(firstLab.HasExperiment && firstWorld.Bodies.Count == 2,
        "Reset must create exactly one central body and one orbiter.");
    Require(firstLab.CentralBody!.IsStatic && !firstLab.Orbiter!.IsStatic,
        "The central preset must be static and the orbiter dynamic.");
    Require(firstLab.Trail.Points.Count == 1,
        "A reset trajectory must retain the original 4D initial position.");

    for (var step = 0; step < 20; step++)
    {
        firstWorld.StepOnce();
    }

    Require(firstLab.Trail.Points.Count == 21,
        "Every fixed physics step must append one original Vector4D trail point.");
    Require(firstLab.Trail.Points.All(point => point.IsFinite),
        "The softened initial experiment must keep its trajectory finite.");
    var firstFinalPosition = firstLab.Orbiter!.Position;

    var secondWorld = new PhysicsWorld4D(fixedDeltaTime: 0.05);
    var secondLab = new GravityLab4D(secondWorld);
    secondLab.ResetExperiment();
    for (var step = 0; step < 20; step++)
    {
        secondWorld.StepOnce();
    }

    RequireVectorNear(secondLab.Orbiter!.Position, firstFinalPosition, 1e-12,
        "Identical Gravity Lab initial conditions must produce deterministic motion.");
    Require(secondLab.Trail.Points.SequenceEqual(firstLab.Trail.Points),
        "The complete stored 4D trajectory must be deterministic.");

    var storedTrajectory = secondLab.Trail.Points.ToArray();
    var trailEdges = Enumerable.Range(1, storedTrajectory.Length - 1)
        .Select(index => new Edge(index - 1, index, EdgeKind.Grid))
        .ToArray();
    var pipeline = new WireframeProjectionPipeline4D();
    var camera = new Camera4D();
    var originalProjection = pipeline.Project(
        storedTrajectory,
        trailEdges,
        new Transform4D(),
        camera,
        new PerspectiveProjector4D());
    camera.Rotate(RotationPlane4D.XW, 0.35);
    var rotatedProjection = pipeline.Project(
        storedTrajectory,
        trailEdges,
        new Transform4D(),
        camera,
        new PerspectiveProjector4D());
    Require(secondLab.Trail.Points.SequenceEqual(storedTrajectory),
        "Camera rotation must not mutate stored physics trajectory points.");
    Require(originalProjection.Vertices.Zip(rotatedProjection.Vertices)
        .Any(pair => pair.First.Position != pair.Second.Position),
        "A 4D camera rotation must reproject the original trail to a different 3D representation.");

    var initialPosition = secondLab.OrbiterInitialPosition;
    var centralMass = secondLab.CentralMass;
    secondLab.SetVelocityPreset(GravityLab4D.HighVelocity);
    Require(secondLab.OrbiterInitialPosition == initialPosition && secondLab.CentralMass == centralMass,
        "Velocity presets must change only pending initial velocity.");
    secondLab.UseXYWVelocity();
    Require(secondLab.OrbiterInitialVelocity.W != 0.0,
        "The XY+W experiment must contain a real non-zero W velocity component.");

    var boundedTrail = new Trajectory4D();
    boundedTrail.SetCapacity(Trajectory4D.MinimumCapacity);
    for (var index = 0; index < Trajectory4D.MinimumCapacity + 25; index++)
    {
        boundedTrail.Append(new Vector4D(index, 0.0, 0.0, 0.0));
    }

    Require(boundedTrail.Points.Count == Trajectory4D.MinimumCapacity &&
        boundedTrail.Points[0].X == 25.0,
        "A full trail must discard only its oldest original 4D positions.");
}

static void CheckGravityLabVelocityRegimes()
{
    var low = SimulateGravityLab(GravityLab4D.LowVelocity, wVelocity: 0.0, stepCount: 3600);
    var medium = SimulateGravityLab(GravityLab4D.MediumVelocity, wVelocity: 0.0, stepCount: 3600);
    var high = SimulateGravityLab(GravityLab4D.HighVelocity, wVelocity: 0.0, stepCount: 3600);
    var xyw = SimulateGravityLab(GravityLab4D.MediumVelocity, wVelocity: 0.75, stepCount: 3600);

    Require(low.IsFinite && medium.IsFinite && high.IsFinite && xyw.IsFinite,
        "All documented 60-second velocity experiments must remain numerically finite.");
    Require(Math.Abs(xyw.FinalW) > 0.1,
        "The XY+W experiment must retain observable real W displacement.");
    Require(low.MaximumDistance != medium.MaximumDistance &&
        medium.MaximumDistance != high.MaximumDistance,
        "Different fixed initial velocities must produce measurably different trajectories.");

    Console.WriteLine(
        $"INFO: 60s gravity regimes  " +
        $"LOW r[min,max,final]=[{low.MinimumDistance:0.000},{low.MaximumDistance:0.000},{low.FinalDistance:0.000}]  " +
        $"MED=[{medium.MinimumDistance:0.000},{medium.MaximumDistance:0.000},{medium.FinalDistance:0.000}]  " +
        $"HIGH=[{high.MinimumDistance:0.000},{high.MaximumDistance:0.000},{high.FinalDistance:0.000}]  " +
        $"XYW finalW={xyw.FinalW:0.000}");
}

static void CheckAggregationConservation()
{
    var first = new PhysicsBody4D(
        1,
        new Vector4D(0.0, 0.0, 0.0, 0.0),
        new Vector4D(2.0, 0.0, 0.0, 0.0),
        mass: 0.25,
        radius: 0.2);
    var second = new PhysicsBody4D(
        2,
        new Vector4D(0.1, 0.0, 0.0, 0.0),
        new Vector4D(-1.0, 0.0, 0.0, 0.0),
        mass: 1.0,
        radius: 0.2);
    var system = new AggregationCollisionSystem4D();

    Require(system.Resolve([first, second]) == 1,
        "One overlapping pair must produce exactly one deterministic merge.");
    Require(!first.IsAlive && second.IsAlive,
        "The larger body must survive and the smaller body must disappear.");
    RequireNear(second.Mass, 1.25, 1e-12, "Merged mass must be additive.");
    RequireNear(second.Velocity.X, -0.4, 1e-12,
        "Merged velocity must conserve momentum: (0.25*2 + 1*(-1))/1.25 = -0.4.");
    RequireNear(second.Position.X, 0.08, 1e-12,
        "Merged position must be the four-dimensional center of mass.");
    RequireNear(
        second.Radius,
        AggregationCollisionSystem4D.RadiusFromMass(1.25, system.RadiusScale),
        1e-12,
        "Merged radius must follow r=k*m^(1/4).");

    var largeFirst = new PhysicsBody4D(
        3,
        new Vector4D(-3.0, 0.0, 0.0, 0.0),
        new Vector4D(2.0, 0.0, 0.0, 0.0),
        mass: 1.0,
        radius: 2.0);
    var largeSecond = new PhysicsBody4D(
        4,
        Vector4D.Zero,
        new Vector4D(-1.0, 0.0, 0.0, 0.0),
        mass: 4.0,
        radius: 2.0);
    Require(system.Resolve([largeFirst, largeSecond]) == 1, "The second worked example must merge.");
    RequireNear(largeSecond.Mass, 5.0, 1e-12, "Worked example mass mismatch.");
    RequireNear(largeSecond.Velocity.X, -0.4, 1e-12, "Worked example momentum mismatch.");
    RequireNear(largeSecond.Position.X, -0.6, 1e-12, "Worked example COM mismatch.");
}

static void CheckAggregationThroughW()
{
    var first = new PhysicsBody4D(
        1,
        Vector4D.Zero,
        Vector4D.Zero,
        radius: 0.08);
    var second = new PhysicsBody4D(
        2,
        new Vector4D(0.0, 0.0, 0.0, 0.12),
        Vector4D.Zero,
        radius: 0.08);
    var system = new AggregationCollisionSystem4D();
    Require(system.Resolve([first, second]) == 1,
        "Bodies separated only in W must collide using full 4D distance.");

    var distantFirst = new PhysicsBody4D(3, Vector4D.Zero, Vector4D.Zero, radius: 0.1);
    var distantSecond = new PhysicsBody4D(
        4,
        new Vector4D(0.0, 0.0, 0.0, 2.0),
        Vector4D.Zero,
        radius: 0.1);
    Require(system.Resolve([distantFirst, distantSecond]) == 0 &&
        distantFirst.IsAlive && distantSecond.IsAlive,
        "Bodies separated by W=2 must not be mistaken for coincident projected 3D points.");
}

static void CheckNBodyProjectedScreenPicking()
{
    var world = new PhysicsWorld4D();
    var nearBody = world.AddBody(
        new Vector4D(1.0, 0.0, 0.0, 0.0),
        Vector4D.Zero,
        radius: 0.2);
    world.AddBody(
        new Vector4D(2.0, 0.0, 0.0, 4.0),
        Vector4D.Zero,
        radius: 0.2);

    var camera4D = new Camera4D();
    var projector4D = new PerspectiveProjector4D();
    var camera3D = new OrbitCamera3D();
    var viewport = new Viewport(0, 0, 940, 900);
    var pipeline = new WireframeProjectionPipeline4D();
    var projected = pipeline.Project(
        world.Bodies.Select(body => body.Position).ToArray(),
        Array.Empty<Edge>(),
        new Transform4D(),
        camera4D,
        projector4D);
    var click = ScreenPoint(projected.Vertices[0], viewport, camera3D);

    Require(NBodyScreenPicker.Pick(
            click,
            viewport,
            projected,
            world.Bodies,
            camera3D,
            pointScale: 1.0) == nearBody,
        "Coincident screen projections must choose the nearer 4D camera-depth candidate.");

    camera4D.MoveWorld(new Vector4D(0.2, -0.1, 0.15, 0.0));
    camera4D.Rotate(RotationPlane4D.XW, 0.25);
    projected = pipeline.Project(
        world.Bodies.Select(body => body.Position).ToArray(),
        Array.Empty<Edge>(),
        new Transform4D(),
        camera4D,
        projector4D);
    click = ScreenPoint(projected.Vertices[0], viewport, camera3D);
    Require(NBodyScreenPicker.Pick(
            click,
            viewport,
            projected,
            world.Bodies,
            camera3D,
            pointScale: 1.0) == nearBody,
        "Picking must use the current 4D camera projection after XW rotation and translation.");
}

static void CheckNBodySelectionLifecycle()
{
    var world = new PhysicsWorld4D();
    var lab = new NBodyLab4D(world);
    lab.Settings.TryApplyBodyCount("2", out _);
    Require(lab.GenerateSystem(), "A two-body selection fixture must generate.");
    var first = world.Bodies[0];
    var second = world.Bodies[1];

    lab.SetTrailMode(NBodyTrailMode4D.SelectedBody);
    Require(lab.SelectBody(first) && ReferenceEquals(world.SelectedBody, first),
        "Selecting a generated body must update the world's selected body.");
    Require(lab.SelectedTrail.Points.Count == 1 &&
        lab.SelectedTrail.Points[0] == first.Position,
        "Selecting a body must immediately reset the selected trail to that body.");
    Require(world.StepOnce() && lab.SelectedTrail.Points.Count == 2,
        "The selected trail must survive normal simulation steps.");

    Require(lab.SelectBody(second) && ReferenceEquals(world.SelectedBody, second),
        "Selecting another body must change the trail target.");
    Require(lab.SelectedTrail.Points.Count == 1 &&
        lab.SelectedTrail.Points[0] == second.Position,
        "Changing selection must discard the previous body's trail.");

    var aggregationWorld = new PhysicsWorld4D();
    aggregationWorld.SetMutualGravityEnabled(false);
    aggregationWorld.SetGravity(Vector4D.Zero);
    aggregationWorld.SetAggregationEnabled(true);
    var absorbed = aggregationWorld.AddBody(
        Vector4D.Zero,
        Vector4D.Zero,
        mass: 1.0,
        radius: 0.2);
    var survivor = aggregationWorld.AddBody(
        new Vector4D(0.1, 0.0, 0.0, 0.0),
        Vector4D.Zero,
        mass: 2.0,
        radius: 0.2);
    Require(aggregationWorld.SelectBody(absorbed),
        "The lighter aggregation fixture body must be selectable.");
    Require(aggregationWorld.StepOnce() && !absorbed.IsAlive && survivor.IsAlive,
        "The selected lighter body must be absorbed by the deterministic survivor.");
    Require(ReferenceEquals(aggregationWorld.SelectedBody, survivor) &&
        aggregationWorld.Bodies.Contains(survivor),
        "Selection must transfer to the merge survivor without leaving a dangling reference.");
}

static Point ScreenPoint(
    ProjectedVertex3D projected,
    Viewport viewport,
    OrbitCamera3D camera)
{
    Require(projected.IsVisible, "The picking fixture must be visible after 4D projection.");
    var center = new Vector3(
        (float)projected.Position.X,
        (float)projected.Position.Y,
        (float)projected.Position.Z);
    var screen = viewport.Project(
        center,
        camera.CreateProjection(viewport.AspectRatio),
        camera.View,
        Matrix.Identity);
    return new Point((int)Math.Round(screen.X), (int)Math.Round(screen.Y));
}

static void CheckNBodyInputValidation()
{
    var settings = new NBodyGenerationSettings4D();
    Require(settings.TryApplyBodyCount("1", out var lowClamped) &&
        lowClamped && settings.BodyCount == 2,
        "Body count text must clamp below the supported minimum.");
    Require(settings.TryApplyBodyCount("99999", out var highClamped) &&
        highClamped && settings.BodyCount == 20_000,
        "Body count text must clamp above the supported maximum.");
    Require(!settings.TryApplyBodyCount("not-a-number", out _) && settings.BodyCount == 20_000,
        "Invalid count text must retain the last valid value.");
    Require(settings.TryApplySeed("-42") && settings.Seed == -42,
        "Seed input must accept the complete signed Int32 range.");
    Require(!settings.TryApplySeed("4.2") && settings.Seed == -42,
        "Invalid seed text must retain the last valid seed.");
}

static void CheckNBodyGeneration()
{
    var firstSettings = new NBodyGenerationSettings4D();
    firstSettings.TryApplyBodyCount("500", out _);
    var secondSettings = new NBodyGenerationSettings4D();
    secondSettings.TryApplyBodyCount("500", out _);
    var generator = new NBodyGenerator4D();
    var first = generator.Generate(firstSettings);
    var second = generator.Generate(secondSettings);

    Require(first.Bodies.SequenceEqual(second.Bodies),
        "Identical N-body settings and seed must produce exactly identical body states.");
    Require(first.Bodies.All(body =>
        Math.Abs(body.Position.X) <= firstSettings.PositionHalfRanges.X &&
        Math.Abs(body.Position.Y) <= firstSettings.PositionHalfRanges.Y &&
        Math.Abs(body.Position.Z) <= firstSettings.PositionHalfRanges.Z &&
        Math.Abs(body.Position.W) <= firstSettings.PositionHalfRanges.W),
        "Every generated coordinate must stay inside its independent 4D range.");
    Require(first.Bodies.All(body =>
        body.Velocity.Length >= firstSettings.MinimumSpeed - 1e-10 &&
        body.Velocity.Length <= firstSettings.MaximumSpeed + 1e-10),
        "Generated 4D speed magnitudes must stay inside the requested range.");
    Require(first.Bodies.All(body =>
        body.Mass >= firstSettings.MinimumMass && body.Mass <= firstSettings.MaximumMass),
        "Generated masses must stay inside the requested uniform range.");
    Require(first.Bodies.Any(body => body.Position.W < 0.0) &&
        first.Bodies.Any(body => body.Position.W > 0.0) &&
        first.Bodies.Any(body => Math.Abs(body.Velocity.W) > 1e-6),
        "The random cloud must occupy and move through the true fourth spatial coordinate.");

    for (var firstIndex = 0; firstIndex < first.Bodies.Count - 1; firstIndex++)
    {
        for (var secondIndex = firstIndex + 1; secondIndex < first.Bodies.Count; secondIndex++)
        {
            var a = first.Bodies[firstIndex];
            var b = first.Bodies[secondIndex];
            var minimumDistance = a.Radius + b.Radius;
            Require((b.Position - a.Position).LengthSquared >= minimumDistance * minimumDistance,
                "Generated bodies must not initially overlap in true 4D distance.");
        }
    }
}

static void CheckNBodyGravityQualityModes()
{
    var world = new PhysicsWorld4D();
    world.SetGravity(Vector4D.Zero);
    world.SetCollisionsEnabled(false);
    world.SetGravityMode(GravityMode4D.Exact);
    world.ReplaceBodies(CreateSeparatedStates(1_000));
    Require(world.EffectiveGravityMode == GravityMode4D.Exact,
        "Exact gravity must remain available at the documented 1000-body threshold.");
    world.ReplaceBodies(CreateSeparatedStates(1_001));
    Require(world.RequestedGravityMode == GravityMode4D.Exact &&
        world.EffectiveGravityMode == GravityMode4D.MeanFieldApproximate,
        "An exact request above the safety threshold must transparently fall back to mean field.");
    world.SetGravityMode(GravityMode4D.MeanFieldApproximate);
    Require(world.EffectiveGravityMode == GravityMode4D.MeanFieldApproximate,
        "The user must be able to request the inexpensive approximation explicitly.");
}

static void CheckNBodyIndependentToggles()
{
    var world = new PhysicsWorld4D(fixedDeltaTime: 0.1);
    world.SetGravity(Vector4D.Zero);
    world.SetCollisionsEnabled(false);
    world.SetMutualGravityEnabled(false);
    world.SetAggregationEnabled(true);
    world.ReplaceBodies([
        new PhysicsBodyInitialState4D(Vector4D.Zero, Vector4D.Zero, 1.0, 0.2),
        new PhysicsBodyInitialState4D(new Vector4D(0.1, 0.0, 0.0, 0.0), Vector4D.Zero, 1.0, 0.2)
    ]);
    world.StepOnce();
    Require(world.Bodies.Count == 1,
        "Aggregation and integration must continue while mutual gravity is OFF.");

    world.ReplaceBodies(CreateSeparatedStates(2));
    world.SetAggregationEnabled(false);
    world.SetMutualGravityEnabled(true);
    world.SetGravityMode(GravityMode4D.Exact);
    world.StepOnce();
    Require(world.Bodies.Count == 2 && world.Bodies.Any(body => body.Velocity.Length > 0.0),
        "Gravity must continue while aggregation is OFF.");
}

static void CheckNBodyScaleBenchmark()
{
    int[] counts = [2, 10, 100, 1_000, 20_000];
    foreach (var count in counts)
    {
        var settings = new NBodyGenerationSettings4D();
        settings.TryApplyBodyCount(count.ToString(), out _);
        var generator = new NBodyGenerator4D();
        var generation = generator.Generate(settings);
        var world = new PhysicsWorld4D();
        world.SetGravity(Vector4D.Zero);
        world.SetCollisionsEnabled(false);
        world.SetGravityMode(count <= 1_000 ? GravityMode4D.Exact : GravityMode4D.MeanFieldApproximate);
        world.SetAggregationRadiusScale(settings.RadiusScale);
        world.SetAggregationCollisionInterval(NBodyLab4D.RecommendedCollisionInterval(count));
        world.SetAggregationEnabled(true);
        world.ReplaceBodies(generation.Bodies);
        world.SetMutualGravityEnabled(true);
        var measuredSteps = world.AggregationCollisionInterval;
        var timer = System.Diagnostics.Stopwatch.StartNew();
        for (var step = 0; step < measuredSteps; step++)
        {
            world.StepOnce();
        }
        timer.Stop();
        Require(world.Bodies.All(body => body.Position.IsFinite && body.Velocity.IsFinite),
            $"The {count:N0}-body benchmark must remain finite.");
        Console.WriteLine(
            $"BENCH: N={count,6:N0} generate={generation.ElapsedMilliseconds,8:0.0} ms " +
            $"stepAvg={timer.Elapsed.TotalMilliseconds / measuredSteps,8:0.0} ms mode={world.EffectiveGravityMode} " +
            $"collision/{world.AggregationCollisionInterval}");
    }
}

static void CheckPerformanceProfilerInstrumentation()
{
    var profiled = new PhysicsWorld4D(fixedDeltaTime: 0.01);
    var baseline = new PhysicsWorld4D(fixedDeltaTime: 0.01);
    var states = CreateSeparatedStates(32);
    ConfigureProfiledComparisonWorld(profiled, states);
    ConfigureProfiledComparisonWorld(baseline, states);

    profiled.Performance.BeginFrame(0.01, profiled.FixedDeltaTime, profiled.TimeScale);
    profiled.StepOnce();
    profiled.Performance.CompleteFrame(
        profiled.AccumulatedSimulationTime,
        simulationStepsPerSecond: 100.0);
    baseline.StepOnce();

    Require(profiled.Bodies.Select(body => body.Position)
            .SequenceEqual(baseline.Bodies.Select(body => body.Position)) &&
        profiled.Bodies.Select(body => body.Velocity)
            .SequenceEqual(baseline.Bodies.Select(body => body.Velocity)),
        "Enabling a profiling frame must not alter deterministic physics state.");
    Require(profiled.Performance.PhysicsStepsThisFrame == 1 &&
        profiled.Performance.PhysicsRunsOnMainThread &&
        !profiled.Performance.UsesParallelPhysics,
        "The profiler must report the current single-threaded fixed step.");
    Require(profiled.Performance.Metric(PerformancePhase.PhysicsTotal).CurrentMilliseconds > 0.0 &&
        profiled.Performance.Metric(PerformancePhase.Gravity).CurrentMilliseconds > 0.0 &&
        profiled.Performance.Metric(PerformancePhase.Integration).CurrentMilliseconds > 0.0,
        "Whole physics, gravity, and integration phases must be measured.");

    var aggregationWorld = new PhysicsWorld4D(fixedDeltaTime: 0.01);
    aggregationWorld.SetGravity(Vector4D.Zero);
    aggregationWorld.SetCollisionsEnabled(false);
    aggregationWorld.SetMutualGravityEnabled(false);
    aggregationWorld.SetAggregationEnabled(true);
    aggregationWorld.ReplaceBodies([
        new PhysicsBodyInitialState4D(Vector4D.Zero, Vector4D.Zero, 1.0, 0.2),
        new PhysicsBodyInitialState4D(new Vector4D(0.1, 0.0, 0.0, 0.0), Vector4D.Zero, 2.0, 0.2)
    ]);
    aggregationWorld.Performance.BeginFrame(
        0.01,
        aggregationWorld.FixedDeltaTime,
        aggregationWorld.TimeScale);
    aggregationWorld.StepOnce();
    aggregationWorld.Performance.CompleteFrame(0.0, 100.0);
    Require(aggregationWorld.Performance.CollisionCandidatesThisFrame > 0 &&
        aggregationWorld.Performance.MergesThisFrame == 1 &&
        aggregationWorld.Bodies.Count == 1,
        "Candidate and merge counters must describe aggregation without changing its result.");

    var rolling = new PerformanceProfiler(rollingWindowSize: 3);
    for (var index = 0; index < 5; index++)
    {
        rolling.BeginFrame(0.016, 0.01, 1.0);
        var startedAt = rolling.BeginPhase();
        System.Threading.Thread.SpinWait(200);
        rolling.EndPhase(PerformancePhase.UiUpdate, startedAt);
        rolling.CompleteFrame(0.005, 60.0);
    }
    Require(rolling.Metric(PerformancePhase.UiUpdate).SampleCount == 3,
        "Rolling profiler history must remain bounded by its configured capacity.");
}

static void ConfigureProfiledComparisonWorld(
    PhysicsWorld4D world,
    IReadOnlyList<PhysicsBodyInitialState4D> states)
{
    world.SetGravity(Vector4D.Zero);
    world.SetCollisionsEnabled(false);
    world.SetAggregationEnabled(false);
    world.SetGravityMode(GravityMode4D.Exact);
    world.ReplaceBodies(states);
    world.SetMutualGravityEnabled(true);
}

static void RunNBodyProfilingReport()
{
    Console.WriteLine("N-BODY CPU PHASE PROFILE (3 bounded samples per scenario)");
    Console.WriteLine("Render/GPU timings require the running MonoGame application and are not faked here.");
    int[] counts = [500, 2_000, 20_000];
    GravityMode4D[] requestedModes = [GravityMode4D.Exact, GravityMode4D.MeanFieldApproximate];
    bool[] aggregationStates = [false, true];

    foreach (var count in counts)
    {
        foreach (var requestedMode in requestedModes)
        {
            foreach (var aggregationEnabled in aggregationStates)
            {
                ProfileNBodyScenario(count, requestedMode, aggregationEnabled);
            }
        }
    }
}

static void ProfileNBodyScenario(
    int bodyCount,
    GravityMode4D requestedMode,
    bool aggregationEnabled)
{
    var world = new PhysicsWorld4D();
    var lab = new NBodyLab4D(world);
    lab.Settings.TryApplyBodyCount(bodyCount.ToString(), out _);
    lab.SetGravityMode(requestedMode);
    if (!aggregationEnabled)
    {
        lab.ToggleAggregation();
    }
    Require(lab.GenerateSystem(), $"Could not generate profiling scenario N={bodyCount:N0}.");

    // Warm the same code paths, then recreate the deterministic initial state.
    var stepsPerSample = NBodyLab4D.RecommendedCollisionInterval(bodyCount);
    for (var step = 0; step < stepsPerSample; step++)
    {
        world.StepOnce();
    }
    lab.ResetSystem();
    world.Performance.Reset();

    const int sampleCount = 3;
    for (var sample = 0; sample < sampleCount; sample++)
    {
        world.Performance.BeginFrame(
            world.FixedDeltaTime * stepsPerSample,
            world.FixedDeltaTime,
            world.TimeScale);
        for (var step = 0; step < stepsPerSample; step++)
        {
            world.StepOnce();
        }
        world.Performance.CompleteFrame(
            world.AccumulatedSimulationTime,
            simulationStepsPerSecond: 0.0);
    }

    var divisor = stepsPerSample;
    double AveragePerStep(PerformancePhase phase) =>
        world.Performance.Metric(phase).AverageMilliseconds / divisor;
    var physics = AveragePerStep(PerformancePhase.PhysicsTotal);
    var particleProjectionPreparation = MeasureParticleProjectionPreparation(world.Bodies);
    var cpuStepsPerSecond = physics <= 0.0 ? 0.0 : 1000.0 / physics;
    Console.WriteLine(
        $"PROFILE: N={bodyCount,6:N0} requested={requestedMode,-20} " +
        $"effective={world.EffectiveGravityMode,-20} aggregation={(aggregationEnabled ? "ON " : "OFF")} " +
        $"physics={physics,8:0.000} gravity={AveragePerStep(PerformancePhase.Gravity),8:0.000} " +
        $"collision={AveragePerStep(PerformancePhase.CollisionDetection),8:0.000} " +
        $"grid={AveragePerStep(PerformancePhase.CollisionGrid),7:0.000} " +
        $"candidatesMs={AveragePerStep(PerformancePhase.CollisionCandidates),7:0.000} " +
        $"sort={AveragePerStep(PerformancePhase.CollisionSort),7:0.000} " +
        $"resolve={AveragePerStep(PerformancePhase.CollisionResolution),7:0.000} " +
        $"merge={AveragePerStep(PerformancePhase.Aggregation),8:0.000} " +
        $"integrate={AveragePerStep(PerformancePhase.Integration),8:0.000} " +
        $"trail={AveragePerStep(PerformancePhase.TrailUpdate),7:0.000} ms " +
        $"particlePrep={particleProjectionPreparation,7:0.000} ms " +
        $"candidates={world.Performance.CollisionCandidatesThisFrame,9:N0} " +
        $"merges={world.Performance.MergesThisFrame,5:N0} cpuStep/s={cpuStepsPerSecond,8:0.0}");
}

static double MeasureParticleProjectionPreparation(IReadOnlyList<PhysicsBody4D> bodies)
{
    var positions = new List<Vector4D>(bodies.Count);
    var pipeline = new WireframeProjectionPipeline4D();
    var transform = new Transform4D();
    var camera = new Camera4D();
    var projector = new PerspectiveProjector4D();
    var performance = new PerformanceProfiler(rollingWindowSize: 3);
    Wireframe3D? projected = null;
    foreach (var body in bodies)
    {
        positions.Add(body.Position);
    }
    _ = pipeline.Project(
        positions,
        Array.Empty<Edge>(),
        transform,
        camera,
        projector);
    for (var sample = 0; sample < 3; sample++)
    {
        performance.BeginFrame(1.0 / 60.0, 1.0 / 60.0, 1.0);
        var startedAt = performance.BeginPhase();
        positions.Clear();
        foreach (var body in bodies)
        {
            positions.Add(body.Position);
        }
        projected = pipeline.Project(
            positions,
            Array.Empty<Edge>(),
            transform,
            camera,
            projector);
        performance.EndPhase(PerformancePhase.RenderingPreparation, startedAt);
        performance.CompleteFrame(0.0, 0.0);
    }

    Require(projected?.Vertices.Count == bodies.Count,
        "Profiled particle projection must retain one projected vertex per 4D body.");
    return performance.Metric(PerformancePhase.RenderingPreparation).AverageMilliseconds;
}

static void CheckNBodyLabLifecycle()
{
    var world = new PhysicsWorld4D();
    var lab = new NBodyLab4D(world);
    lab.Settings.TryApplyBodyCount("10", out _);
    Require(lab.GenerateSystem(), "The default N-body cloud must generate successfully.");
    var initial = world.Bodies.Select(body =>
        new PhysicsBodyInitialState4D(body.Position, body.Velocity, body.Mass, body.Radius)).ToArray();
    Require(world.IsPaused && world.MutualGravityEnabled && world.AggregationEnabled,
        "A generated experiment must start paused with default gravity and aggregation ON.");
    RequireNear(world.GravitySystem.GravitationalConstant, 0.060, 1e-12,
        "N-body default G mismatch.");
    RequireNear(world.GravitySystem.Softening, 0.25, 1e-12,
        "N-body default softening mismatch.");
    Require(lab.TrailMode == NBodyTrailMode4D.Off,
        "Per-body trail storage must be OFF by default.");

    lab.SetGravityMode(GravityMode4D.MeanFieldApproximate);
    lab.ToggleGravity();
    lab.ToggleAggregation();
    lab.ResetSystem();
    var reset = world.Bodies.Select(body =>
        new PhysicsBodyInitialState4D(body.Position, body.Velocity, body.Mass, body.Radius)).ToArray();
    Require(initial.SequenceEqual(reset),
        "RESET with the same seed and generation settings must recreate identical conditions.");
    Require(!world.MutualGravityEnabled && !world.AggregationEnabled &&
        world.RequestedGravityMode == GravityMode4D.MeanFieldApproximate,
        "RESET must preserve the selected simulation toggles and quality mode.");
}

static IReadOnlyList<PhysicsBodyInitialState4D> CreateSeparatedStates(int count)
{
    var states = new PhysicsBodyInitialState4D[count];
    for (var index = 0; index < count; index++)
    {
        states[index] = new PhysicsBodyInitialState4D(
            new Vector4D(index * 0.5, (index % 7) * 0.7, (index % 11) * 0.9, (index % 13) * 1.1),
            Vector4D.Zero,
            1.0,
            0.01);
    }

    return states;
}

static (double MinimumDistance, double MaximumDistance, double FinalDistance, double FinalW, bool IsFinite)
    SimulateGravityLab(double yVelocity, double wVelocity, int stepCount)
{
    var world = new PhysicsWorld4D();
    var lab = new GravityLab4D(world);
    lab.SetVelocityPreset(yVelocity);
    if (wVelocity != 0.0)
    {
        lab.AdjustOrbiterInitialVelocity(new Vector4D(0.0, 0.0, 0.0, wVelocity));
    }

    lab.ResetExperiment();
    var minimumDistance = lab.Diagnostics.Distance;
    var maximumDistance = minimumDistance;
    var isFinite = true;
    for (var step = 0; step < stepCount; step++)
    {
        world.StepOnce();
        var distance = lab.Diagnostics.Distance;
        minimumDistance = Math.Min(minimumDistance, distance);
        maximumDistance = Math.Max(maximumDistance, distance);
        isFinite &= lab.Orbiter!.Position.IsFinite && lab.Orbiter.Velocity.IsFinite &&
            double.IsFinite(distance);
    }

    return (
        minimumDistance,
        maximumDistance,
        lab.Diagnostics.Distance,
        lab.Orbiter!.Position.W,
        isFinite);
}

static MouseState MouseAt(
    int x,
    int y,
    int wheel,
    ButtonState leftButton = ButtonState.Released,
    ButtonState middleButton = ButtonState.Released,
    ButtonState rightButton = ButtonState.Released) =>
    new(
        x,
        y,
        wheel,
        leftButton,
        middleButton,
        rightButton,
        ButtonState.Released,
        ButtonState.Released);

static int Different(double left, double right) => left == right ? 0 : 1;

static int ChangedCoordinateCount(Vector4D left, Vector4D right) =>
    Different(left.X, right.X) +
    Different(left.Y, right.Y) +
    Different(left.Z, right.Z) +
    Different(left.W, right.W);

static double GetCoordinate(Vector4D vector, CoordinateAxis4D axis) =>
    axis switch
    {
        CoordinateAxis4D.X => vector.X,
        CoordinateAxis4D.Y => vector.Y,
        CoordinateAxis4D.Z => vector.Z,
        CoordinateAxis4D.W => vector.W,
        _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, null)
    };

static void Require(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static void RequireQuaternionNear(
    Quaternion4D actual,
    Quaternion4D expected,
    double tolerance,
    string message)
{
    RequireNear(actual.A, expected.A, tolerance, $"{message} A mismatch.");
    RequireNear(actual.B, expected.B, tolerance, $"{message} B mismatch.");
    RequireNear(actual.C, expected.C, tolerance, $"{message} C mismatch.");
    RequireNear(actual.D, expected.D, tolerance, $"{message} D mismatch.");
}

static void RequireThrows<TException>(Action action, string message)
    where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        return;
    }

    throw new InvalidOperationException(message);
}

static void RequireNear(double actual, double expected, double tolerance, string message)
{
    if (Math.Abs(actual - expected) > tolerance)
    {
        throw new InvalidOperationException($"{message} Expected {expected}, received {actual}.");
    }
}

static void RequireVectorNear(
    Vector4D actual,
    Vector4D expected,
    double tolerance,
    string message)
{
    RequireNear(actual.X, expected.X, tolerance, $"{message} X mismatch.");
    RequireNear(actual.Y, expected.Y, tolerance, $"{message} Y mismatch.");
    RequireNear(actual.Z, expected.Z, tolerance, $"{message} Z mismatch.");
    RequireNear(actual.W, expected.W, tolerance, $"{message} W mismatch.");
}
