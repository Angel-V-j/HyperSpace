using System;
using System.Collections.Generic;
using HyperSpace.Mathematics;

namespace HyperSpace.Geometry;

/// <summary>
/// A finite 3D lattice sampled inside the 4D hyperplane W=0.
/// It visualizes three independent tangent directions X/Y/Z, not a fake 2D plane.
/// </summary>
public sealed class HyperplaneGrid4D
{
    private readonly Vector4D[] _vertices;
    private readonly Edge[] _edges;

    public HyperplaneGrid4D(double extent = 2.5, int coordinatesPerAxis = 5)
    {
        if (!double.IsFinite(extent) || extent <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(extent));
        }

        if (coordinatesPerAxis < 2 || coordinatesPerAxis > 15)
        {
            throw new ArgumentOutOfRangeException(nameof(coordinatesPerAxis));
        }

        var vertices = new List<Vector4D>(3 * coordinatesPerAxis * coordinatesPerAxis * 2);
        var edges = new List<Edge>(3 * coordinatesPerAxis * coordinatesPerAxis);
        var values = new double[coordinatesPerAxis];
        var spacing = (2.0 * extent) / (coordinatesPerAxis - 1);
        for (var index = 0; index < coordinatesPerAxis; index++)
        {
            values[index] = -extent + (index * spacing);
        }

        foreach (var first in values)
        {
            foreach (var second in values)
            {
                AddLine(
                    vertices,
                    edges,
                    new Vector4D(-extent, first, second, 0.0),
                    new Vector4D(extent, first, second, 0.0));
                AddLine(
                    vertices,
                    edges,
                    new Vector4D(first, -extent, second, 0.0),
                    new Vector4D(first, extent, second, 0.0));
                AddLine(
                    vertices,
                    edges,
                    new Vector4D(first, second, -extent, 0.0),
                    new Vector4D(first, second, extent, 0.0));
            }
        }

        _vertices = vertices.ToArray();
        _edges = edges.ToArray();
    }

    public IReadOnlyList<Vector4D> Vertices => _vertices;

    public IReadOnlyList<Edge> Edges => _edges;

    private static void AddLine(
        List<Vector4D> vertices,
        List<Edge> edges,
        Vector4D start,
        Vector4D end)
    {
        var startIndex = vertices.Count;
        vertices.Add(start);
        vertices.Add(end);
        edges.Add(new Edge(startIndex, startIndex + 1, EdgeKind.Grid));
    }
}
