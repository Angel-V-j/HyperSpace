using System;
using System.Collections.Generic;
using HyperSpace.Mathematics;

namespace HyperSpace.Physics;

/// <summary>
/// Minimal uniform 4D grid used only for local overlap/collision candidates.
/// </summary>
internal sealed class SpatialHashGrid4D
{
    private readonly Dictionary<Cell4D, List<int>> _indicesByCell = [];
    private double _cellSize = 1.0;

    public void Reset(double cellSize)
    {
        if (!double.IsFinite(cellSize) || cellSize <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(cellSize));
        }

        _cellSize = cellSize;
        _indicesByCell.Clear();
    }

    public void Add(int index, Vector4D position)
    {
        var cell = CellFor(position);
        if (!_indicesByCell.TryGetValue(cell, out var indices))
        {
            indices = [];
            _indicesByCell.Add(cell, indices);
        }

        indices.Add(index);
    }

    public void CollectNeighborIndices(Vector4D position, List<int> destination)
    {
        destination.Clear();
        var center = CellFor(position);
        for (var dx = -1; dx <= 1; dx++)
        {
            for (var dy = -1; dy <= 1; dy++)
            {
                for (var dz = -1; dz <= 1; dz++)
                {
                    for (var dw = -1; dw <= 1; dw++)
                    {
                        var neighbor = new Cell4D(
                            center.X + dx,
                            center.Y + dy,
                            center.Z + dz,
                            center.W + dw);
                        if (_indicesByCell.TryGetValue(neighbor, out var indices))
                        {
                            destination.AddRange(indices);
                        }
                    }
                }
            }
        }
    }

    private Cell4D CellFor(Vector4D position) =>
        new(
            Coordinate(position.X),
            Coordinate(position.Y),
            Coordinate(position.Z),
            Coordinate(position.W));

    private int Coordinate(double value) => (int)Math.Floor(value / _cellSize);

    private readonly record struct Cell4D(int X, int Y, int Z, int W);
}
