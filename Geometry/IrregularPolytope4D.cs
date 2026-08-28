using System;
using System.Collections.Generic;
using System.Linq;
using HyperSpace.Mathematics;

namespace HyperSpace.Geometry;

/// <summary>
/// A deterministic asymmetric realization of the 4D cross-polytope topology.
/// Unequal axis radii and a fixed invertible shear make it irregular while the
/// combinatorial 16-cell topology remains exact and explainable.
/// </summary>
public sealed class IrregularPolytope4D : IGeometry4D
{
    private readonly Vector4D[] _vertices;
    private readonly Edge[] _edges;
    private readonly Face4D[] _faces;
    private readonly Cell4D[] _cells;

    public IrregularPolytope4D()
    {
        var source = new[]
        {
            new Vector4D(1.15, 0.0, 0.0, 0.0),
            new Vector4D(-0.78, 0.0, 0.0, 0.0),
            new Vector4D(0.0, 0.92, 0.0, 0.0),
            new Vector4D(0.0, -1.22, 0.0, 0.0),
            new Vector4D(0.0, 0.0, 1.30, 0.0),
            new Vector4D(0.0, 0.0, -0.84, 0.0),
            new Vector4D(0.0, 0.0, 0.0, 1.05),
            new Vector4D(0.0, 0.0, 0.0, -1.35)
        };

        var sheared = source.Select(Shear).ToArray();
        var centroid = sheared.Aggregate(Vector4D.Zero, (sum, vertex) => sum + vertex) *
            (1.0 / sheared.Length);
        _vertices = sheared.Select(vertex => vertex - centroid).ToArray();

        _edges = Simplex4D.Combinations(8, 2)
            .Where(indices => !AreOpposite(indices[0], indices[1]))
            .Select(indices => new Edge(indices[0], indices[1]))
            .ToArray();
        _faces = Simplex4D.Combinations(8, 3)
            .Where(HasNoOppositePair)
            .Select(indices => new Face4D(indices))
            .ToArray();
        _cells = CreateCells();
    }

    public string Name => "Irregular 16-cell";

    public GeometryVisualStyle4D VisualStyle => GeometryVisualStyle4D.Irregular;

    public IReadOnlyList<Vector4D> Vertices => _vertices;

    public IReadOnlyList<Edge> Edges => _edges;

    public IReadOnlyList<Face4D> Faces => _faces;

    public IReadOnlyList<Cell4D> Cells => _cells;

    public string ResolutionDescription => "Deterministic cross-polytope topology";

    private static Cell4D[] CreateCells()
    {
        var cells = new List<Cell4D>(capacity: 16);

        for (var mask = 0; mask < 16; mask++)
        {
            var indices = new int[4];
            for (var axis = 0; axis < 4; axis++)
            {
                indices[axis] = (2 * axis) + ((mask >> axis) & 1);
            }

            var faces = Simplex4D.Combinations(4, 3)
                .Select(local => new Face4D(local.Select(i => indices[i]).ToArray()))
                .ToArray();
            cells.Add(new Cell4D($"Cell {mask + 1}", indices, faces));
        }

        return cells.ToArray();
    }

    private static bool HasNoOppositePair(int[] indices)
    {
        for (var left = 0; left < indices.Length; left++)
        {
            for (var right = left + 1; right < indices.Length; right++)
            {
                if (AreOpposite(indices[left], indices[right]))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool AreOpposite(int left, int right) => left / 2 == right / 2;

    private static Vector4D Shear(Vector4D point) =>
        new(
            point.X + (0.18 * point.Y) - (0.12 * point.W),
            (0.10 * point.X) + point.Y + (0.16 * point.Z),
            (-0.08 * point.Y) + point.Z + (0.14 * point.W),
            (0.12 * point.X) - (0.10 * point.Z) + point.W);
}
