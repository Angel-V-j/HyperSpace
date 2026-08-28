using System;
using HyperSpace.Mathematics;
using HyperSpace.Projection;
using HyperSpace.Rendering;
using HyperSpace.Transformations;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace HyperSpace.Input;

/// <summary>
/// Maps direct sandbox controls to the current object and camera state.
/// </summary>
public sealed class SandboxInputController
{
    private const double RotationSpeed = Math.PI / 2.0;
    private const double CameraMoveSpeed = 2.0;
    private const double FocalDistanceSpeed = 1.0;
    private const float MouseOrbitSensitivity = 0.008f;
    private const float MouseWheelZoomStep = 0.6f;
    private const double ObjectDragUnitsPerPixel = 0.01;

    private KeyboardState _previousKeyboard;
    private MouseState _previousMouse;
    private bool _hasPreviousMouse;

    public bool ExitRequested { get; private set; }

    public bool ResetRequested { get; private set; }

    public void Update(
        GameTime gameTime,
        bool isActive,
        KeyboardState keyboard,
        MouseState mouse,
        GamePadState gamePad,
        Transform4D objectTransform,
        Camera4D camera4D,
        PerspectiveProjector4D projector,
        OrbitCamera3D camera3D,
        bool allowMouseInput = true)
    {
        ResetRequested = false;
        ExitRequested = isActive &&
            (keyboard.IsKeyDown(Keys.Escape) ||
             gamePad.Buttons.Back == ButtonState.Pressed);

        if (!isActive)
        {
            _previousKeyboard = keyboard;
            _previousMouse = mouse;
            _hasPreviousMouse = false;
            return;
        }

        var elapsedSeconds = gameTime.ElapsedGameTime.TotalSeconds;
        var rotationDelta = RotationSpeed * elapsedSeconds;
        var rotateCamera = keyboard.IsKeyDown(Keys.LeftShift) ||
            keyboard.IsKeyDown(Keys.RightShift);

        ApplyRotationPair(keyboard, Keys.T, Keys.Y, RotationPlane4D.XY, rotationDelta,
            rotateCamera, objectTransform, camera4D);
        ApplyRotationPair(keyboard, Keys.U, Keys.I, RotationPlane4D.XZ, rotationDelta,
            rotateCamera, objectTransform, camera4D);
        ApplyRotationPair(keyboard, Keys.O, Keys.P, RotationPlane4D.XW, rotationDelta,
            rotateCamera, objectTransform, camera4D);
        ApplyRotationPair(keyboard, Keys.G, Keys.H, RotationPlane4D.YZ, rotationDelta,
            rotateCamera, objectTransform, camera4D);
        ApplyRotationPair(keyboard, Keys.J, Keys.K, RotationPlane4D.YW, rotationDelta,
            rotateCamera, objectTransform, camera4D);
        ApplyRotationPair(keyboard, Keys.L, Keys.OemSemicolon, RotationPlane4D.ZW, rotationDelta,
            rotateCamera, objectTransform, camera4D);

        var moveDistance = CameraMoveSpeed * elapsedSeconds;
        camera4D.MoveWorld(new Vector4D(
            KeyAxis(keyboard, Keys.A, Keys.Q) * moveDistance,
            KeyAxis(keyboard, Keys.S, Keys.W) * moveDistance,
            KeyAxis(keyboard, Keys.D, Keys.E) * moveDistance,
            KeyAxis(keyboard, Keys.F, Keys.R) * moveDistance));

        projector.AdjustFocalDistance(
            KeyAxis(keyboard, Keys.OemOpenBrackets, Keys.OemCloseBrackets) *
            FocalDistanceSpeed * elapsedSeconds);

        var resetRequested = IsNewKeyPress(keyboard, Keys.Space);
        if (resetRequested)
        {
            ResetRequested = true;
            objectTransform.Reset();
            camera4D.Reset();
            projector.Reset();
            camera3D.Reset();
        }

        if (!resetRequested && allowMouseInput)
        {
            UpdateMouse(mouse, objectTransform, camera3D);
        }

        _previousKeyboard = keyboard;
        _previousMouse = mouse;
        _hasPreviousMouse = allowMouseInput;
    }

    private void UpdateMouse(
        MouseState mouse,
        Transform4D objectTransform,
        OrbitCamera3D camera3D)
    {
        if (_hasPreviousMouse)
        {
            var deltaX = mouse.X - _previousMouse.X;
            var deltaY = mouse.Y - _previousMouse.Y;

            if (mouse.LeftButton == ButtonState.Pressed &&
                _previousMouse.LeftButton == ButtonState.Pressed)
            {
                camera3D.Orbit(
                    -deltaX * MouseOrbitSensitivity,
                    -deltaY * MouseOrbitSensitivity);
            }

            if (mouse.RightButton == ButtonState.Pressed &&
                _previousMouse.RightButton == ButtonState.Pressed)
            {
                objectTransform.MoveWorld(new Vector4D(
                    deltaX * ObjectDragUnitsPerPixel,
                    -deltaY * ObjectDragUnitsPerPixel,
                    0.0,
                    0.0));
            }

            if (mouse.MiddleButton == ButtonState.Pressed &&
                _previousMouse.MiddleButton == ButtonState.Pressed)
            {
                objectTransform.MoveWorld(new Vector4D(
                    0.0,
                    0.0,
                    deltaX * ObjectDragUnitsPerPixel,
                    -deltaY * ObjectDragUnitsPerPixel));
            }

            var wheelSteps = (mouse.ScrollWheelValue - _previousMouse.ScrollWheelValue) / 120.0f;
            camera3D.Zoom(-wheelSteps * MouseWheelZoomStep);
        }
    }

    private bool IsNewKeyPress(KeyboardState keyboard, Keys key) =>
        keyboard.IsKeyDown(key) && _previousKeyboard.IsKeyUp(key);

    private static void ApplyRotationPair(
        KeyboardState keyboard,
        Keys negativeKey,
        Keys positiveKey,
        RotationPlane4D plane,
        double rotationDelta,
        bool rotateCamera,
        Transform4D objectTransform,
        Camera4D camera4D)
    {
        var direction = KeyAxis(keyboard, negativeKey, positiveKey);
        if (direction == 0)
        {
            return;
        }

        var angle = direction * rotationDelta;
        if (rotateCamera)
        {
            camera4D.Rotate(plane, angle);
        }
        else
        {
            objectTransform.Rotate(plane, angle);
        }
    }

    private static int KeyAxis(KeyboardState keyboard, Keys negativeKey, Keys positiveKey) =>
        (keyboard.IsKeyDown(positiveKey) ? 1 : 0) -
        (keyboard.IsKeyDown(negativeKey) ? 1 : 0);
}
