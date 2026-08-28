using System;
using System.Collections.Generic;
using HyperSpace.Mathematics;

namespace HyperSpace.Geometry;

/// <summary>
/// A small 4D coordinate-frame grid centered on the world origin.
/// </summary>
public sealed class ReferenceGrid4D
{
    public const double DefaultExtent = 2.0;
    public const double DefaultSpacing = 1.0;

    private readonly Vector4D[] _vertices;
    private readonly Edge[] _edges;

    public ReferenceGrid4D(
        double extent = DefaultExtent,
        double spacing = DefaultSpacing)
    {
        if (!double.IsFinite(extent) || extent <= 0.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(extent),
                "Grid extent must be finite and greater than zero.");
        }

        if (!double.IsFinite(spacing) || spacing <= 0.0 || spacing > extent)
        {
            throw new ArgumentOutOfRangeException(
                nameof(spacing),
                "Grid spacing must be finite, positive, and no larger than the extent.");
        }

        var vertices = new List<Vector4D>();
        var edges = new List<Edge>();
        var layerCount = (int)Math.Floor(extent / spacing);
        var sampledExtent = layerCount * spacing;

        for (var layer = -layerCount; layer <= layerCount; layer++)
        {
            var w = layer * spacing;
            var isOriginLayer = layer == 0;

            AddLine(vertices, edges,
                new Vector4D(-sampledExtent, 0.0, 0.0, w),
                new Vector4D(sampledExtent, 0.0, 0.0, w),
                isOriginLayer ? EdgeKind.AxisX : EdgeKind.Grid);
            AddLine(vertices, edges,
                new Vector4D(0.0, -sampledExtent, 0.0, w),
                new Vector4D(0.0, sampledExtent, 0.0, w),
                isOriginLayer ? EdgeKind.AxisY : EdgeKind.Grid);
            AddLine(vertices, edges,
                new Vector4D(0.0, 0.0, -sampledExtent, w),
                new Vector4D(0.0, 0.0, sampledExtent, w),
                isOriginLayer ? EdgeKind.AxisZ : EdgeKind.Grid);
        }

        // The central W axis projects to one point because x=y=z=0. These six
        // offset W-parallel rails expose the real W structure without faking it.
        AddWLine(vertices, edges, sampledExtent, 0.0, 0.0, sampledExtent);
        AddWLine(vertices, edges, -sampledExtent, 0.0, 0.0, sampledExtent);
        AddWLine(vertices, edges, 0.0, sampledExtent, 0.0, sampledExtent);
        AddWLine(vertices, edges, 0.0, -sampledExtent, 0.0, sampledExtent);
        AddWLine(vertices, edges, 0.0, 0.0, sampledExtent, sampledExtent);
        AddWLine(vertices, edges, 0.0, 0.0, -sampledExtent, sampledExtent);

        _vertices = vertices.ToArray();
        _edges = edges.ToArray();
    }

    public IReadOnlyList<Vector4D> Vertices => _vertices;

    public IReadOnlyList<Edge> Edges => _edges;

    private static void AddWLine(
        List<Vector4D> vertices,
        List<Edge> edges,
        double x,
        double y,
        double z,
        double extent)
    {
        AddLine(
            vertices,
            edges,
            new Vector4D(x, y, z, -extent),
            new Vector4D(x, y, z, extent),
            EdgeKind.AxisW);
    }

    private static void AddLine(
        List<Vector4D> vertices,
        List<Edge> edges,
        Vector4D start,
        Vector4D end,
        EdgeKind kind)
    {
        var startIndex = vertices.Count;
        vertices.Add(start);
        vertices.Add(end);
        edges.Add(new Edge(startIndex, startIndex + 1, kind));
    }
}
