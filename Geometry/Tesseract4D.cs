using System;
using System.Collections.Generic;
using HyperSpace.Mathematics;

namespace HyperSpace.Geometry;

/// <summary>
/// An algorithmically generated four-dimensional hypercube.
/// </summary>
public sealed class Tesseract4D
{
    private const int DimensionCount = 4;
    private const int VertexCount = 1 << DimensionCount;

    private readonly Vector4D[] _vertices;
    private readonly Edge[] _edges;

    public Tesseract4D(double halfExtent = 1.0)
    {
        if (!double.IsFinite(halfExtent) || halfExtent <= 0.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(halfExtent),
                "Half extent must be finite and greater than zero.");
        }

        _vertices = CreateVertices(halfExtent);
        _edges = CreateEdges();
    }

    public IReadOnlyList<Vector4D> Vertices => _vertices;

    public IReadOnlyList<Edge> Edges => _edges;

    private static Vector4D[] CreateVertices(double halfExtent)
    {
        var vertices = new Vector4D[VertexCount];

        for (var index = 0; index < VertexCount; index++)
        {
            vertices[index] = new Vector4D(
                CoordinateFromBit(index, bit: 0, halfExtent),
                CoordinateFromBit(index, bit: 1, halfExtent),
                CoordinateFromBit(index, bit: 2, halfExtent),
                CoordinateFromBit(index, bit: 3, halfExtent));
        }

        return vertices;
    }

    private static Edge[] CreateEdges()
    {
        var edges = new List<Edge>(capacity: 32);

        for (var vertex = 0; vertex < VertexCount; vertex++)
        {
            for (var dimension = 0; dimension < DimensionCount; dimension++)
            {
                var neighbor = vertex ^ (1 << dimension);

                // Add each undirected edge once rather than once from each endpoint.
                if (vertex < neighbor)
                {
                    edges.Add(new Edge(vertex, neighbor));
                }
            }
        }

        return edges.ToArray();
    }

    private static double CoordinateFromBit(int index, int bit, double halfExtent) =>
        (index & (1 << bit)) == 0 ? -halfExtent : halfExtent;
}
