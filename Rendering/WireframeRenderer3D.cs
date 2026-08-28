using System;
using System.Collections.Generic;
using HyperSpace.Geometry;
using HyperSpace.Mathematics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace HyperSpace.Rendering;

/// <summary>
/// Renders any projected 4D geometry and the common reference lines with BasicEffect.
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
        IGeometry4D geometry,
        OrbitCamera3D camera,
        int visibleVertexLimit = int.MaxValue)
    {
        DrawInternal(
            graphicsDevice,
            wireframe,
            geometry,
            camera,
            isReferenceGrid: false,
            visibleVertexLimit: visibleVertexLimit);
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
            geometry: null,
            camera,
            isReferenceGrid: true,
            showGrid,
            showAxes);
    }

    public void DrawSurfaces(
        GraphicsDevice graphicsDevice,
        Wireframe3D wireframe,
        IGeometry4D geometry,
        OrbitCamera3D camera)
    {
        _transparentTriangles.Clear();
        var view = camera.View;

        if (geometry.Cells.Count > 0)
        {
            for (var cellIndex = 0; cellIndex < geometry.Cells.Count; cellIndex++)
            {
                foreach (var face in geometry.Cells[cellIndex].Faces)
                {
                    AddFaceTriangles(
                        wireframe,
                        face,
                        geometry,
                        cellIndex,
                        view);
                }
            }
        }
        else
        {
            foreach (var face in geometry.Faces)
            {
                AddFaceTriangles(wireframe, face, geometry, cellIndex: null, view);
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
        IGeometry4D geometry,
        OrbitCamera3D camera,
        int visibleVertexLimit = int.MaxValue)
    {
        var effectiveLimit = Math.Clamp(visibleVertexLimit, 0, wireframe.Vertices.Count);
        const int trianglesPerMarker = 8;
        const int verticesPerTriangle = 3;
        EnsureTriangleVertexCapacity(
            effectiveLimit * trianglesPerMarker * verticesPerTriangle);
        var writtenVertexCount = 0;
        var (minimumW, maximumW) = FindVisibleColorWRange(
            wireframe,
            geometry,
            effectiveLimit);

        for (var vertexIndex = 0; vertexIndex < effectiveLimit; vertexIndex++)
        {
            var vertex = wireframe.Vertices[vertexIndex];
            if (!vertex.IsVisible || !TryConvert(vertex.Position, out var center))
            {
                continue;
            }

            var color = WDepthColor(
                geometry.VisualStyle,
                ColorW(geometry, vertex),
                minimumW,
                maximumW);
            var radius = geometry.VisualStyle switch
            {
                GeometryVisualStyle4D.Hypersphere => VertexMarkerRadius * 0.58f,
                GeometryVisualStyle4D.Spiral => VertexMarkerRadius * 0.40f,
                _ => VertexMarkerRadius
            };
            WriteOctahedron(center, color, radius, ref writtenVertexCount);
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

    public void DrawCurveDirectionMarkers(
        GraphicsDevice graphicsDevice,
        Wireframe3D wireframe,
        OrbitCamera3D camera,
        int visibleVertexLimit)
    {
        var effectiveLimit = Math.Clamp(visibleVertexLimit, 0, wireframe.Vertices.Count);
        if (effectiveLimit == 0)
        {
            return;
        }

        EnsureTriangleVertexCapacity(60);
        var writtenVertexCount = 0;
        var start = wireframe.Vertices[0];
        if (start.IsVisible && TryConvert(start.Position, out var startPosition))
        {
            // START is an octahedron.
            WriteOctahedron(
                startPosition,
                VisualizationPalette.CurveStart,
                VertexMarkerRadius * 1.45f,
                ref writtenVertexCount);
        }

        var endIndex = effectiveLimit - 1;
        var end = wireframe.Vertices[endIndex];
        if (endIndex > 0 && end.IsVisible && TryConvert(end.Position, out var endPosition))
        {
            // END/current playback tip is a geometrically distinct cube.
            WriteCube(
                endPosition,
                VisualizationPalette.CurveEnd,
                VertexMarkerRadius * 1.35f,
                ref writtenVertexCount);
        }

        if (writtenVertexCount == 0)
        {
            return;
        }

        graphicsDevice.BlendState = BlendState.Opaque;
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
        IGeometry4D? geometry,
        OrbitCamera3D camera,
        bool isReferenceGrid,
        bool showGrid = true,
        bool showAxes = true,
        int visibleVertexLimit = int.MaxValue)
    {
        var effectiveLimit = Math.Clamp(visibleVertexLimit, 0, wireframe.Vertices.Count);
        EnsureVertexCapacity(wireframe.Edges.Count * 2);

        var (minimumDepth, maximumDepth) = isReferenceGrid
            ? (0.0, 0.0)
            : FindVisibleDepthRange(wireframe, effectiveLimit);
        var (minimumW, maximumW) = isReferenceGrid
            ? (0.0, 0.0)
            : FindVisibleColorWRange(wireframe, geometry!, effectiveLimit);
        var writtenVertexCount = 0;

        foreach (var edge in wireframe.Edges)
        {
            if (edge.Start >= effectiveLimit || edge.End >= effectiveLimit)
            {
                continue;
            }

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
                    ObjectEdgeColor(
                        geometry!,
                        edge,
                        start,
                        start.CameraDepth4D,
                        minimumDepth,
                        maximumDepth,
                        minimumW,
                        maximumW));
                _lineVertices[writtenVertexCount++] = new VertexPositionColor(
                    endPosition,
                    ObjectEdgeColor(
                        geometry!,
                        edge,
                        end,
                        end.CameraDepth4D,
                        minimumDepth,
                        maximumDepth,
                        minimumW,
                        maximumW));
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
        Wireframe3D wireframe,
        int visibleVertexLimit)
    {
        var minimum = double.PositiveInfinity;
        var maximum = double.NegativeInfinity;

        for (var index = 0; index < visibleVertexLimit; index++)
        {
            var vertex = wireframe.Vertices[index];
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

    private static (double Minimum, double Maximum) FindVisibleColorWRange(
        Wireframe3D wireframe,
        IGeometry4D geometry,
        int visibleVertexLimit)
    {
        var minimum = double.PositiveInfinity;
        var maximum = double.NegativeInfinity;
        // Curve colors stay stable while a prefix is revealed. Hidden projected
        // points still carry finite world W metadata, so the full curve defines
        // one consistent W scale for the entire playback.
        var rangeLimit = geometry.VisualStyle == GeometryVisualStyle4D.Spiral
            ? wireframe.Vertices.Count
            : visibleVertexLimit;

        for (var index = 0; index < rangeLimit; index++)
        {
            var vertex = wireframe.Vertices[index];
            if (!vertex.IsVisible && geometry.VisualStyle != GeometryVisualStyle4D.Spiral)
            {
                continue;
            }

            var w = ColorW(geometry, vertex);
            minimum = Math.Min(minimum, w);
            maximum = Math.Max(maximum, w);
        }

        return double.IsFinite(minimum) ? (minimum, maximum) : (0.0, 0.0);
    }

    private static (double Minimum, double Maximum) FindVisibleSourceWRange(
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

            minimum = Math.Min(minimum, vertex.SourceW);
            maximum = Math.Max(maximum, vertex.SourceW);
        }

        return double.IsFinite(minimum) ? (minimum, maximum) : (0.0, 0.0);
    }

    private static Color ObjectEdgeColor(
        IGeometry4D geometry,
        Edge edge,
        ProjectedVertex3D vertex,
        double depth,
        double minimum,
        double maximum,
        double minimumW,
        double maximumW)
    {
        var range = maximum - minimum;
        var normalizedDepth = range > 1e-12
            ? (float)Math.Clamp((depth - minimum) / range, 0.0, 1.0)
            : 0.5f;
        var brightness = 1.0f - (0.38f * normalizedDepth);
        var baseColor = geometry.VisualStyle == GeometryVisualStyle4D.Tesseract
            ? VisualizationPalette.EdgeColor(edge.Axis)
            : WDepthColor(
                geometry.VisualStyle,
                ColorW(geometry, vertex),
                minimumW,
                maximumW);

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

    private void AddFaceTriangles(
        Wireframe3D wireframe,
        Face4D face,
        IGeometry4D geometry,
        int? cellIndex,
        Matrix view)
    {
        var points = new Vector3[face.VertexIndices.Count];
        var sourceWSum = 0.0;

        for (var index = 0; index < face.VertexIndices.Count; index++)
        {
            var projected = wireframe.Vertices[face.VertexIndices[index]];
            if (!projected.IsVisible || !TryConvert(projected.Position, out points[index]))
            {
                // 4D polygon clipping is deferred. Skipping the whole face is safe
                // and avoids triangles that cross the perspective singularity.
                return;
            }

            sourceWSum += projected.SourceW;
        }

        var (minimumW, maximumW) = FindVisibleSourceWRange(wireframe);
        var averageW = sourceWSum / face.VertexIndices.Count;
        var baseColor = cellIndex.HasValue
            ? VisualizationPalette.CellColor(cellIndex.Value, geometry.VisualStyle)
            : WDepthColor(geometry.VisualStyle, averageW, minimumW, maximumW);
        var color = WithAlpha(baseColor, VisualizationPalette.CellSurfaceAlpha);

        for (var index = 1; index < points.Length - 1; index++)
        {
            AddTransparentTriangle(points[0], points[index], points[index + 1], color, view);
        }
    }

    private static Color WDepthColor(
        GeometryVisualStyle4D style,
        double w,
        double minimum,
        double maximum)
    {
        float amount;
        if (style == GeometryVisualStyle4D.Spiral)
        {
            // Zero is always the gradient midpoint. Unlike min/max normalization,
            // this preserves the meaning of W sign when the curve is translated.
            var maximumMagnitude = Math.Max(Math.Abs(minimum), Math.Abs(maximum));
            amount = maximumMagnitude > 1e-12
                ? (float)Math.Clamp(0.5 + (w / (2.0 * maximumMagnitude)), 0.0, 1.0)
                : 0.5f;
        }
        else
        {
            var range = maximum - minimum;
            amount = range > 1e-12
                ? (float)Math.Clamp((w - minimum) / range, 0.0, 1.0)
                : 0.5f;
        }

        return VisualizationPalette.WDepthColor(style, amount);
    }

    private static double ColorW(IGeometry4D geometry, ProjectedVertex3D vertex) =>
        geometry.VisualStyle == GeometryVisualStyle4D.Spiral
            ? vertex.WorldW
            : vertex.SourceW;

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

    private void WriteOctahedron(Vector3 center, Color color, float radius, ref int index)
    {
        var top = center + (Vector3.Up * radius);
        var bottom = center + (Vector3.Down * radius);
        var left = center + (Vector3.Left * radius);
        var right = center + (Vector3.Right * radius);
        var forward = center + (Vector3.Forward * radius);
        var backward = center + (Vector3.Backward * radius);

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

    private void WriteCube(Vector3 center, Color color, float radius, ref int index)
    {
        var nnn = center + new Vector3(-radius, -radius, -radius);
        var nnp = center + new Vector3(-radius, -radius, radius);
        var npn = center + new Vector3(-radius, radius, -radius);
        var npp = center + new Vector3(-radius, radius, radius);
        var pnn = center + new Vector3(radius, -radius, -radius);
        var pnp = center + new Vector3(radius, -radius, radius);
        var ppn = center + new Vector3(radius, radius, -radius);
        var ppp = center + new Vector3(radius, radius, radius);

        WriteTriangle(nnn, npn, ppn, color, ref index);
        WriteTriangle(nnn, ppn, pnn, color, ref index);
        WriteTriangle(nnp, pnp, ppp, color, ref index);
        WriteTriangle(nnp, ppp, npp, color, ref index);
        WriteTriangle(nnn, nnp, npp, color, ref index);
        WriteTriangle(nnn, npp, npn, color, ref index);
        WriteTriangle(pnn, ppn, ppp, color, ref index);
        WriteTriangle(pnn, ppp, pnp, color, ref index);
        WriteTriangle(nnn, pnn, pnp, color, ref index);
        WriteTriangle(nnn, pnp, nnp, color, ref index);
        WriteTriangle(npn, npp, ppp, color, ref index);
        WriteTriangle(npn, ppp, ppn, color, ref index);
    }

    private void ApplyCamera(GraphicsDevice graphicsDevice, OrbitCamera3D camera)
    {
        _effect.View = camera.View;
        _effect.Projection = camera.CreateProjection(graphicsDevice.Viewport.AspectRatio);
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
