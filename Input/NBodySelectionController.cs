using System.Collections.Generic;
using HyperSpace.Geometry;
using HyperSpace.Physics;
using HyperSpace.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace HyperSpace.Input;

/// <summary>
/// Distinguishes an N-body selection click from the existing orbit-camera drag.
/// Screen-space hit testing remains in NBodyScreenPicker.
/// </summary>
internal sealed class NBodySelectionController
{
    private const int ClickMovementThresholdPixels = 5;

    private MouseState _previousMouse;
    private bool _hasPreviousMouse;
    private bool _pickArmed;
    private Point _pickStart;

    public PhysicsBody4D? Update(
        MouseState mouse,
        bool isGameActive,
        bool isNBodyView,
        bool pointerOverPanel,
        Viewport sceneViewport,
        Wireframe3D particleWireframe,
        IReadOnlyList<PhysicsBody4D> bodies,
        OrbitCamera3D camera,
        double pointScale)
    {
        var previousLeftPressed = _hasPreviousMouse &&
            _previousMouse.LeftButton == ButtonState.Pressed;
        var currentLeftPressed = mouse.LeftButton == ButtonState.Pressed;
        var acceptsSceneInput = isGameActive && isNBodyView && !pointerOverPanel;
        PhysicsBody4D? selectedBody = null;

        if (!previousLeftPressed && currentLeftPressed)
        {
            _pickArmed = acceptsSceneInput;
            _pickStart = mouse.Position;
        }
        else if (_pickArmed && currentLeftPressed &&
            MovementSquared(_pickStart, mouse.Position) >
                ClickMovementThresholdPixels * ClickMovementThresholdPixels)
        {
            // A left drag remains the existing 3D orbit gesture, not a selection click.
            _pickArmed = false;
        }
        else if (previousLeftPressed && !currentLeftPressed)
        {
            if (_pickArmed && acceptsSceneInput)
            {
                selectedBody = NBodyScreenPicker.Pick(
                    mouse.Position,
                    sceneViewport,
                    particleWireframe,
                    bodies,
                    camera,
                    pointScale);
            }

            _pickArmed = false;
        }

        if (!isNBodyView)
        {
            _pickArmed = false;
        }

        _previousMouse = mouse;
        _hasPreviousMouse = isGameActive;
        return selectedBody;
    }

    private static int MovementSquared(Point start, Point end)
    {
        var deltaX = end.X - start.X;
        var deltaY = end.Y - start.Y;
        return (deltaX * deltaX) + (deltaY * deltaY);
    }
}
