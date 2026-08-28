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
    private readonly TesseractCell4D[] _cells;

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
        _cells = CreateCells();
    }

    public IReadOnlyList<Vector4D> Vertices => _vertices;

    public IReadOnlyList<Edge> Edges => _edges;

    public IReadOnlyList<TesseractCell4D> Cells => _cells;

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
                    edges.Add(new Edge(
                        vertex,
                        neighbor,
                        EdgeKind.Default,
                        (CoordinateAxis4D)dimension));
                }
            }
        }

        return edges.ToArray();
    }

    private static TesseractCell4D[] CreateCells()
    {
        var cells = new List<TesseractCell4D>(capacity: 8);

        for (var fixedDimension = 0; fixedDimension < DimensionCount; fixedDimension++)
        {
            for (var fixedBit = 0; fixedBit <= 1; fixedBit++)
            {
                var vertexIndices = new List<int>(capacity: 8);

                for (var vertex = 0; vertex < VertexCount; vertex++)
                {
                    if (((vertex >> fixedDimension) & 1) == fixedBit)
                    {
                        vertexIndices.Add(vertex);
                    }
                }

                cells.Add(new TesseractCell4D(
                    (CoordinateAxis4D)fixedDimension,
                    fixedBit == 0 ? -1 : 1,
                    vertexIndices,
                    CreateCellFaces(fixedDimension, fixedBit)));
            }
        }

        return cells.ToArray();
    }

    private static QuadFace[] CreateCellFaces(int fixedDimension, int fixedBit)
    {
        var faces = new List<QuadFace>(capacity: 6);

        for (var faceDimension = 0; faceDimension < DimensionCount; faceDimension++)
        {
            if (faceDimension == fixedDimension)
            {
                continue;
            }

            for (var faceBit = 0; faceBit <= 1; faceBit++)
            {
                var freeDimensions = new int[2];
                var freeIndex = 0;

                for (var dimension = 0; dimension < DimensionCount; dimension++)
                {
                    if (dimension != fixedDimension && dimension != faceDimension)
                    {
                        freeDimensions[freeIndex++] = dimension;
                    }
                }

                var baseVertex =
                    (fixedBit << fixedDimension) |
                    (faceBit << faceDimension);
                var firstFreeBit = 1 << freeDimensions[0];
                var secondFreeBit = 1 << freeDimensions[1];

                faces.Add(new QuadFace(
                    baseVertex,
                    baseVertex | firstFreeBit,
                    baseVertex | firstFreeBit | secondFreeBit,
                    baseVertex | secondFreeBit));
            }
        }

        return faces.ToArray();
    }

    private static double CoordinateFromBit(int index, int bit, double halfExtent) =>
        (index & (1 << bit)) == 0 ? -halfExtent : halfExtent;
}
