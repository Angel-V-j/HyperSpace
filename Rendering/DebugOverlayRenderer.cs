using System;
using System.Globalization;
using System.Text;
using HyperSpace.Geometry;
using HyperSpace.Projection;
using HyperSpace.Transformations;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace HyperSpace.Rendering;

/// <summary>
/// A small text overlay for inspecting the current sandbox state.
/// </summary>
public sealed class DebugOverlayRenderer : IDisposable
{
    private readonly SpriteBatch _spriteBatch;
    private readonly SpriteFont _font;
    private readonly StringBuilder _text = new(capacity: 1024);

    private double _sampleTime;
    private int _sampleFrames;
    private double _framesPerSecond;

    public DebugOverlayRenderer(GraphicsDevice graphicsDevice, SpriteFont font)
    {
        _spriteBatch = new SpriteBatch(graphicsDevice);
        _font = font;
    }

    public void UpdateTiming(GameTime gameTime)
    {
        _sampleTime += gameTime.ElapsedGameTime.TotalSeconds;
        _sampleFrames++;

        if (_sampleTime >= 0.5)
        {
            _framesPerSecond = _sampleFrames / _sampleTime;
            _sampleTime = 0.0;
            _sampleFrames = 0;
        }
    }

    public void Draw(
        Transform4D objectTransform,
        Camera4D camera4D,
        PerspectiveProjector4D projector,
        OrbitCamera3D camera3D,
        Wireframe3D wireframe,
        TransformationAnimator4D animator)
    {
        BuildText(objectTransform, camera4D, projector, camera3D, wireframe, animator);

        _spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.AlphaBlend,
            SamplerState.LinearClamp,
            DepthStencilState.None,
            RasterizerState.CullNone);

        var position = new Vector2(14.0f, 12.0f);
        _spriteBatch.DrawString(_font, _text, position + new Vector2(2.0f), Color.Black * 0.8f);
        _spriteBatch.DrawString(_font, _text, position, new Color(225, 235, 255));
        _spriteBatch.End();
    }

    public void Dispose()
    {
        _spriteBatch.Dispose();
    }

    private void BuildText(
        Transform4D objectTransform,
        Camera4D camera4D,
        PerspectiveProjector4D projector,
        OrbitCamera3D camera3D,
        Wireframe3D wireframe,
        TransformationAnimator4D animator)
    {
        _text.Clear();
        AppendFormat("FPS {0,5:0.0}   Visible {1}/16 vertices, {2}/32 edges\n",
            _framesPerSecond,
            wireframe.VisibleVertexCount,
            wireframe.VisibleEdgeCount);
        AppendFormat("Object4D pos ({0,6:0.00}, {1,6:0.00}, {2,6:0.00}, {3,6:0.00})  scale {4:0.000}\n",
            objectTransform.Position.X,
            objectTransform.Position.Y,
            objectTransform.Position.Z,
            objectTransform.Position.W,
            objectTransform.Scale);
        AppendRotation("Object", objectTransform.Rotation);
        AppendFormat("Camera4D pos ({0,6:0.00}, {1,6:0.00}, {2,6:0.00}, {3,6:0.00})\n",
            camera4D.Position.X,
            camera4D.Position.Y,
            camera4D.Position.Z,
            camera4D.Position.W);
        AppendRotation("Camera", camera4D.Orientation);
        AppendFormat("4D projection  focal {0:0.00}   near W {1:0.00}\n",
            projector.FocalDistance,
            projector.NearPlane);
        AppendFormat("3D view  yaw {0:0.0} deg   pitch {1:0.0} deg   distance {2:0.00}\n",
            Degrees(camera3D.Yaw),
            Degrees(camera3D.Pitch),
            camera3D.Distance);
        if (animator.IsActive)
        {
            var detail = animator.ActiveRotationPlane.HasValue
                ? $"step angle {animator.CurrentRotationDegrees:0.0} / 90 deg"
                : $"progress {animator.Progress * 100.0:0}%";
            AppendFormat("Animation  {0}   {1}\n", animator.ActiveLabel, detail);
        }
        else
        {
            _text.AppendLine("Animation  idle");
        }
    }

    private void AppendRotation(string label, Rotation4D rotation)
    {
        AppendFormat(
            "{0,-6} deg  XY {1,6:0.0}  XZ {2,6:0.0}  XW {3,6:0.0}  YZ {4,6:0.0}  YW {5,6:0.0}  ZW {6,6:0.0}\n",
            label,
            Degrees(rotation.XY),
            Degrees(rotation.XZ),
            Degrees(rotation.XW),
            Degrees(rotation.YZ),
            Degrees(rotation.YW),
            Degrees(rotation.ZW));
    }

    private void AppendFormat(string format, params object[] arguments)
    {
        _text.AppendFormat(CultureInfo.InvariantCulture, format, arguments);
    }

    private static double Degrees(double radians) => radians * 180.0 / Math.PI;
}
