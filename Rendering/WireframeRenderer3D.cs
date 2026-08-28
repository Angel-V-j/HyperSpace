using System;
using System.Collections.Generic;
using HyperSpace.Geometry;
using HyperSpace.Mathematics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace HyperSpace.Rendering;

/// <summary>
/// Renders the projected tesseract layers and reference lines with BasicEffect.
/// </summary>
public sealed class WireframeRenderer3D : IDisposable
{
    private const float VertexMarkerRadius = 0.045f;

    private readonly BasicEffect _effect;
    private VertexPositionColor[] _lineVertices = [];
    private VertexPositionColor[] _triangleVertices = [];
    private readonly List<TransparentTriangle> _transparentTriangles = new(capacity: 96);

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
        OrbitCamera3D camera,
        bool showGrid,
        bool showAxes)
    {
        DrawInternal(
            graphicsDevice,
            wireframe,
            camera,
            isReferenceGrid: true,
            showGrid,
            showAxes);
    }

    public void DrawCells(
        GraphicsDevice graphicsDevice,
        Wireframe3D wireframe,
        IReadOnlyList<TesseractCell4D> cells,
        OrbitCamera3D camera)
    {
        _transparentTriangles.Clear();
        var view = camera.View;

        for (var cellIndex = 0; cellIndex < cells.Count; cellIndex++)
        {
            var color = WithAlpha(
                VisualizationPalette.CellColor(cellIndex),
                VisualizationPalette.CellSurfaceAlpha);

            foreach (var face in cells[cellIndex].Faces)
            {
                if (!TryGetFace(wireframe, face, out var a, out var b, out var c, out var d))
                {
                    continue;
                }

                AddTransparentTriangle(a, b, c, color, view);
                AddTransparentTriangle(a, c, d, color, view);
            }
        }

        if (_transparentTriangles.Count == 0)
        {
            return;
        }

        // Alpha blending is order-dependent. Centroid sorting is a practical
        // approximation for this tiny mesh; shared/intersecting faces cannot be
        // globally ordered perfectly without a more complex transparency method.
        _transparentTriangles.Sort(static (left, right) =>
            left.ViewDepth.CompareTo(right.ViewDepth));
        EnsureTriangleVertexCapacity(_transparentTriangles.Count * 3);

        var writtenVertexCount = 0;
        foreach (var triangle in _transparentTriangles)
        {
            _triangleVertices[writtenVertexCount++] = new VertexPositionColor(triangle.A, triangle.Color);
            _triangleVertices[writtenVertexCount++] = new VertexPositionColor(triangle.B, triangle.Color);
            _triangleVertices[writtenVertexCount++] = new VertexPositionColor(triangle.C, triangle.Color);
        }

        graphicsDevice.BlendState = BlendState.NonPremultiplied;
        graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
        graphicsDevice.RasterizerState = RasterizerState.CullNone;
        ApplyCamera(graphicsDevice, camera);

        foreach (var pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            graphicsDevice.DrawUserPrimitives(
                PrimitiveType.TriangleList,
                _triangleVertices,
                vertexOffset: 0,
                primitiveCount: writtenVertexCount / 3);
        }
    }

    public void DrawVertices(
        GraphicsDevice graphicsDevice,
        Wireframe3D wireframe,
        OrbitCamera3D camera)
    {
        const int trianglesPerMarker = 8;
        const int verticesPerTriangle = 3;
        EnsureTriangleVertexCapacity(
            wireframe.VisibleVertexCount * trianglesPerMarker * verticesPerTriangle);
        var writtenVertexCount = 0;

        foreach (var vertex in wireframe.Vertices)
        {
            if (!vertex.IsVisible || !TryConvert(vertex.Position, out var center))
            {
                continue;
            }

            var color = vertex.SourceW < 0.0
                ? VisualizationPalette.VertexNegativeW
                : VisualizationPalette.VertexPositiveW;
            WriteOctahedron(center, color, ref writtenVertexCount);
        }

        if (writtenVertexCount == 0)
        {
            return;
        }

        graphicsDevice.BlendState = BlendState.Opaque;
        // Markers are structural annotations drawn last. Disabling depth here
        // keeps all sixteen readable instead of hiding them behind translucent cells.
        graphicsDevice.DepthStencilState = DepthStencilState.None;
        graphicsDevice.RasterizerState = RasterizerState.CullNone;
        ApplyCamera(graphicsDevice, camera);

        foreach (var pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            graphicsDevice.DrawUserPrimitives(
                PrimitiveType.TriangleList,
                _triangleVertices,
                vertexOffset: 0,
                primitiveCount: writtenVertexCount / 3);
        }
    }

    private void DrawInternal(
        GraphicsDevice graphicsDevice,
        Wireframe3D wireframe,
        OrbitCamera3D camera,
        bool isReferenceGrid,
        bool showGrid = true,
        bool showAxes = true)
    {
        EnsureVertexCapacity(wireframe.Edges.Count * 2);

        var (minimumDepth, maximumDepth) = isReferenceGrid
            ? (0.0, 0.0)
            : FindVisibleDepthRange(wireframe);
        var writtenVertexCount = 0;

        foreach (var edge in wireframe.Edges)
        {
            if (isReferenceGrid && !ShouldDrawReferenceEdge(edge.Kind, showGrid, showAxes))
            {
                continue;
            }

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
                    DirectionAndDepthColor(
                        edge.Axis,
                        start.CameraDepth4D,
                        minimumDepth,
                        maximumDepth));
                _lineVertices[writtenVertexCount++] = new VertexPositionColor(
                    endPosition,
                    DirectionAndDepthColor(
                        edge.Axis,
                        end.CameraDepth4D,
                        minimumDepth,
                        maximumDepth));
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

        ApplyCamera(graphicsDevice, camera);

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

    private void EnsureTriangleVertexCapacity(int requiredCount)
    {
        if (_triangleVertices.Length < requiredCount)
        {
            _triangleVertices = new VertexPositionColor[requiredCount];
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

    private static Color DirectionAndDepthColor(
        CoordinateAxis4D? axis,
        double depth,
        double minimum,
        double maximum)
    {
        var range = maximum - minimum;
        var normalizedDepth = range > 1e-12
            ? (float)Math.Clamp((depth - minimum) / range, 0.0, 1.0)
            : 0.5f;
        var brightness = 1.0f - (0.38f * normalizedDepth);
        var baseColor = VisualizationPalette.EdgeColor(axis);

        return new Color(
            (byte)(baseColor.R * brightness),
            (byte)(baseColor.G * brightness),
            (byte)(baseColor.B * brightness),
            byte.MaxValue);
    }

    private static Color ReferenceGridColor(EdgeKind kind) =>
        kind switch
        {
            EdgeKind.AxisX => VisualizationPalette.AxisX,
            EdgeKind.AxisY => VisualizationPalette.AxisY,
            EdgeKind.AxisZ => VisualizationPalette.AxisZ,
            EdgeKind.AxisW => VisualizationPalette.AxisW,
            _ => VisualizationPalette.Grid
        };

    private static bool ShouldDrawReferenceEdge(
        EdgeKind kind,
        bool showGrid,
        bool showAxes) =>
        kind == EdgeKind.Grid ? showGrid : showAxes;

    private static Color WithAlpha(Color color, float alpha) =>
        new(color.R, color.G, color.B, (byte)Math.Round(byte.MaxValue * alpha));

    private void AddTransparentTriangle(
        Vector3 a,
        Vector3 b,
        Vector3 c,
        Color color,
        Matrix view)
    {
        var centroid = (a + b + c) / 3.0f;
        var viewDepth = Vector3.Transform(centroid, view).Z;
        _transparentTriangles.Add(new TransparentTriangle(a, b, c, color, viewDepth));
    }

    private void WriteOctahedron(Vector3 center, Color color, ref int index)
    {
        var top = center + (Vector3.Up * VertexMarkerRadius);
        var bottom = center + (Vector3.Down * VertexMarkerRadius);
        var left = center + (Vector3.Left * VertexMarkerRadius);
        var right = center + (Vector3.Right * VertexMarkerRadius);
        var forward = center + (Vector3.Forward * VertexMarkerRadius);
        var backward = center + (Vector3.Backward * VertexMarkerRadius);

        WriteTriangle(top, forward, right, color, ref index);
        WriteTriangle(top, right, backward, color, ref index);
        WriteTriangle(top, backward, left, color, ref index);
        WriteTriangle(top, left, forward, color, ref index);
        WriteTriangle(bottom, right, forward, color, ref index);
        WriteTriangle(bottom, backward, right, color, ref index);
        WriteTriangle(bottom, left, backward, color, ref index);
        WriteTriangle(bottom, forward, left, color, ref index);
    }

    private void WriteTriangle(
        Vector3 a,
        Vector3 b,
        Vector3 c,
        Color color,
        ref int index)
    {
        _triangleVertices[index++] = new VertexPositionColor(a, color);
        _triangleVertices[index++] = new VertexPositionColor(b, color);
        _triangleVertices[index++] = new VertexPositionColor(c, color);
    }

    private void ApplyCamera(GraphicsDevice graphicsDevice, OrbitCamera3D camera)
    {
        _effect.View = camera.View;
        _effect.Projection = camera.CreateProjection(graphicsDevice.Viewport.AspectRatio);
    }

    private static bool TryGetFace(
        Wireframe3D wireframe,
        QuadFace face,
        out Vector3 a,
        out Vector3 b,
        out Vector3 c,
        out Vector3 d)
    {
        var projectedA = wireframe.Vertices[face.A];
        var projectedB = wireframe.Vertices[face.B];
        var projectedC = wireframe.Vertices[face.C];
        var projectedD = wireframe.Vertices[face.D];

        if (projectedA.IsVisible &&
            projectedB.IsVisible &&
            projectedC.IsVisible &&
            projectedD.IsVisible &&
            TryConvert(projectedA.Position, out a) &&
            TryConvert(projectedB.Position, out b) &&
            TryConvert(projectedC.Position, out c) &&
            TryConvert(projectedD.Position, out d))
        {
            return true;
        }

        a = b = c = d = Vector3.Zero;
        return false;
    }

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

    private readonly record struct TransparentTriangle(
        Vector3 A,
        Vector3 B,
        Vector3 C,
        Color Color,
        float ViewDepth);
}
