using System;
using System.Linq;
using HyperSpace.Geometry;
using HyperSpace.Input;
using HyperSpace.Mathematics;
using HyperSpace.Projection;
using HyperSpace.Rendering;
using HyperSpace.Transformations;
using HyperSpace.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

var checks = new (string Name, Action Run)[]
{
    ("Tesseract topology", CheckTesseractTopology),
    ("4D reference grid", CheckReferenceGrid),
    ("Six plane rotations", CheckPlaneRotations),
    ("Rotation inverse", CheckRotationInverse),
    ("4D camera space", CheckCameraSpace),
    ("4D perspective projection", CheckPerspectiveProjection),
    ("Projection safety", CheckProjectionSafety),
    ("Animated 4D transformations", CheckTransformationAnimation),
    ("Minimal UI button states", CheckUiButtonStates),
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
        degrees[edge.Start]++;
        degrees[edge.End]++;
    }

    Require(degrees.All(degree => degree == 4), "Every tesseract vertex must have degree four.");
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

static void Require(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
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
