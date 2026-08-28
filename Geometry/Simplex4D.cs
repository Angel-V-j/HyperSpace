using System;
using System.Collections.Generic;
using System.Linq;
using HyperSpace.Mathematics;

namespace HyperSpace.Geometry;

/// <summary>
/// A regular 4-simplex (pentachoron), centered at the origin and inscribed in S3.
/// </summary>
public sealed class Simplex4D : IGeometry4D
{
    private readonly Vector4D[] _vertices;
    private readonly Edge[] _edges;
    private readonly Face4D[] _faces;
    private readonly Cell4D[] _cells;

    public Simplex4D(double radius = 1.35)
    {
        if (!double.IsFinite(radius) || radius <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(radius));
        }

        var a = Math.Sqrt(5.0) / 4.0;
        _vertices =
        [
            new Vector4D(a, a, a, -0.25) * radius,
            new Vector4D(a, -a, -a, -0.25) * radius,
            new Vector4D(-a, a, -a, -0.25) * radius,
            new Vector4D(-a, -a, a, -0.25) * radius,
            new Vector4D(0.0, 0.0, 0.0, 1.0) * radius
        ];

        _edges = Combinations(5, 2)
            .Select(pair => new Edge(pair[0], pair[1]))
            .ToArray();
        _faces = Combinations(5, 3)
            .Select(indices => new Face4D(indices))
            .ToArray();
        _cells = Combinations(5, 4)
            .Select((indices, index) => new Cell4D(
                $"Cell {index + 1}",
                indices,
                Combinations(indices.Length, 3)
                    .Select(local => new Face4D(local.Select(i => indices[i]).ToArray()))
                    .ToArray()))
            .ToArray();
    }

    public string Name => "4-Simplex";

    public GeometryVisualStyle4D VisualStyle => GeometryVisualStyle4D.Simplex;

    public IReadOnlyList<Vector4D> Vertices => _vertices;

    public IReadOnlyList<Edge> Edges => _edges;

    public IReadOnlyList<Face4D> Faces => _faces;

    public IReadOnlyList<Cell4D> Cells => _cells;

    public string ResolutionDescription => "Exact regular pentachoron";

    internal static IEnumerable<int[]> Combinations(int itemCount, int selectionCount)
    {
        var current = new int[selectionCount];
        return Enumerate(depth: 0, next: 0);

        IEnumerable<int[]> Enumerate(int depth, int next)
        {
            if (depth == selectionCount)
            {
                yield return [.. current];
                yield break;
            }

            for (var value = next; value <= itemCount - (selectionCount - depth); value++)
            {
                current[depth] = value;
                foreach (var result in Enumerate(depth + 1, value + 1))
                {
                    yield return result;
                }
            }
        }
    }
}
