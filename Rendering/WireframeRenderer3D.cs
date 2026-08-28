using System;
using HyperSpace.Geometry;
using HyperSpace.Mathematics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace HyperSpace.Rendering;

/// <summary>
/// Renders the projected 3D representation as MonoGame line primitives.
/// </summary>
public sealed class WireframeRenderer3D : IDisposable
{
    private static readonly Color NearDepthColor = new(255, 190, 84);
    private static readonly Color FarDepthColor = new(55, 125, 255);
    private static readonly Color GridColor = new(115, 125, 155, 40);
    private static readonly Color AxisXColor = new(165, 95, 95, 82);
    private static readonly Color AxisYColor = new(95, 155, 110, 82);
    private static readonly Color AxisZColor = new(90, 115, 175, 82);
    private static readonly Color AxisWColor = new(145, 100, 175, 62);

    private readonly BasicEffect _effect;
    private VertexPositionColor[] _lineVertices = [];

    public WireframeRenderer3D(GraphicsDevice graphicsDevice)
    {
        _effect = new BasicEffect(graphicsDevice)
        {
            VertexColorEnabled = true,
            World = Matrix.Identity
        };
    }

    public void Draw(
        GraphicsDevice graphicsDevice,
        Wireframe3D wireframe,
        OrbitCamera3D camera)
    {
        DrawInternal(graphicsDevice, wireframe, camera, isReferenceGrid: false);
    }

    public void DrawReferenceGrid(
        GraphicsDevice graphicsDevice,
        Wireframe3D wireframe,
        OrbitCamera3D camera)
    {
        DrawInternal(graphicsDevice, wireframe, camera, isReferenceGrid: true);
    }

    private void DrawInternal(
        GraphicsDevice graphicsDevice,
        Wireframe3D wireframe,
        OrbitCamera3D camera,
        bool isReferenceGrid)
    {
        EnsureVertexCapacity(wireframe.Edges.Count * 2);

        var (minimumDepth, maximumDepth) = isReferenceGrid
            ? (0.0, 0.0)
            : FindVisibleDepthRange(wireframe);
        var writtenVertexCount = 0;

        foreach (var edge in wireframe.Edges)
        {
            var start = wireframe.Vertices[edge.Start];
            var end = wireframe.Vertices[edge.End];

            if (!start.IsVisible || !end.IsVisible ||
                !TryConvert(start.Position, out var startPosition) ||
                !TryConvert(end.Position, out var endPosition))
            {
                // Full 4D edge clipping is intentionally deferred. An edge with
                // an invalid endpoint is skipped rather than producing infinities.
                continue;
            }

            if (isReferenceGrid)
            {
                var color = ReferenceGridColor(edge.Kind);
                _lineVertices[writtenVertexCount++] = new VertexPositionColor(startPosition, color);
                _lineVertices[writtenVertexCount++] = new VertexPositionColor(endPosition, color);
            }
            else
            {
                _lineVertices[writtenVertexCount++] = new VertexPositionColor(
                    startPosition,
                    DepthColor(start.CameraDepth4D, minimumDepth, maximumDepth));
                _lineVertices[writtenVertexCount++] = new VertexPositionColor(
                    endPosition,
                    DepthColor(end.CameraDepth4D, minimumDepth, maximumDepth));
            }
        }

        if (writtenVertexCount == 0)
        {
            return;
        }

        graphicsDevice.BlendState = isReferenceGrid
            ? BlendState.NonPremultiplied
            : BlendState.Opaque;
        graphicsDevice.DepthStencilState = isReferenceGrid
            ? DepthStencilState.DepthRead
            : DepthStencilState.Default;
        graphicsDevice.RasterizerState = RasterizerState.CullNone;

        _effect.View = camera.View;
        _effect.Projection = camera.CreateProjection(graphicsDevice.Viewport.AspectRatio);

        foreach (var pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            graphicsDevice.DrawUserPrimitives(
                PrimitiveType.LineList,
                _lineVertices,
                vertexOffset: 0,
                primitiveCount: writtenVertexCount / 2);
        }
    }

    public void Dispose()
    {
        _effect.Dispose();
    }

    private void EnsureVertexCapacity(int requiredCount)
    {
        if (_lineVertices.Length < requiredCount)
        {
            _lineVertices = new VertexPositionColor[requiredCount];
        }
    }

    private static (double Minimum, double Maximum) FindVisibleDepthRange(
        Wireframe3D wireframe)
    {
        var minimum = double.PositiveInfinity;
        var maximum = double.NegativeInfinity;

        foreach (var vertex in wireframe.Vertices)
        {
            if (!vertex.IsVisible)
            {
                continue;
            }

            minimum = Math.Min(minimum, vertex.CameraDepth4D);
            maximum = Math.Max(maximum, vertex.CameraDepth4D);
        }

        return double.IsFinite(minimum)
            ? (minimum, maximum)
            : (0.0, 0.0);
    }

    private static Color DepthColor(double depth, double minimum, double maximum)
    {
        var range = maximum - minimum;
        var normalizedDepth = range > 1e-12
            ? (float)Math.Clamp((depth - minimum) / range, 0.0, 1.0)
            : 0.5f;

        return Color.Lerp(NearDepthColor, FarDepthColor, normalizedDepth);
    }

    private static Color ReferenceGridColor(EdgeKind kind) =>
        kind switch
        {
            EdgeKind.AxisX => AxisXColor,
            EdgeKind.AxisY => AxisYColor,
            EdgeKind.AxisZ => AxisZColor,
            EdgeKind.AxisW => AxisWColor,
            _ => GridColor
        };

    private static bool TryConvert(Vector3D source, out Vector3 destination)
    {
        var x = (float)source.X;
        var y = (float)source.Y;
        var z = (float)source.Z;

        if (!float.IsFinite(x) || !float.IsFinite(y) || !float.IsFinite(z))
        {
            destination = Vector3.Zero;
            return false;
        }

        destination = new Vector3(x, y, z);
        return true;
    }
}
