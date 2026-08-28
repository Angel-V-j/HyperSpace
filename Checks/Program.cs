using System;
using System.Collections.Generic;
using System.Linq;
using HyperSpace.Geometry;
using HyperSpace.Input;
using HyperSpace.Mathematics;
using HyperSpace.Projection;
using HyperSpace.Rendering;
using HyperSpace.Scene;
using HyperSpace.Transformations;
using HyperSpace.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

var checks = new (string Name, Action Run)[]
{
    ("Tesseract topology", CheckTesseractTopology),
    ("Tesseract cubic cells", CheckTesseractCells),
    ("Hypersphere sampling", CheckHypersphereSampling),
    ("Regular 4-simplex topology", CheckSimplexTopology),
    ("Irregular 4D polytope topology", CheckIrregularPolytope),
    ("4D spiral sampling", CheckSpiralSampling),
    ("Curve playback state", CheckCurvePlayback),
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
    ("Interactive input mapping", CheckInputMapping)
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
