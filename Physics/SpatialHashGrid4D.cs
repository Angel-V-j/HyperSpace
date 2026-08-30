using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using HyperSpace.Diagnostics;
using HyperSpace.Mathematics;

namespace HyperSpace.Physics;

/// <summary>
/// Minimal uniform 4D grid used only for local overlap/collision candidates.
/// </summary>
internal sealed class SpatialHashGrid4D
{
    private static readonly Cell4D[] ForwardNeighborOffsets = CreateForwardNeighborOffsets();

    private readonly Dictionary<Cell4D, List<int>> _indicesByCell = [];
    private readonly Stack<List<int>> _availableLists = [];
    private readonly List<Cell4D> _occupiedCells = [];
    private List<(int FirstIndex, int SecondIndex)>[] _workerPairs = [];
    private Cell4D[] _bodyCells = [];
    private double _cellSize = 1.0;

    public int LastCandidateWorkerCount { get; private set; }

    public void Reset(double cellSize)
    {
        if (!double.IsFinite(cellSize) || cellSize <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(cellSize));
        }

        _cellSize = cellSize;
        foreach (var indices in _indicesByCell.Values)
        {
            indices.Clear();
            _availableLists.Push(indices);
        }
        _indicesByCell.Clear();
        _occupiedCells.Clear();
    }

    public void Build(IReadOnlyList<PhysicsBody4D> bodies, bool[] eligible, double cellSize)
    {
        Reset(cellSize);
        if (_bodyCells.Length < bodies.Count)
            Array.Resize(ref _bodyCells, Math.Max(bodies.Count, _bodyCells.Length * 2));
        ParallelWork.ForRanges(bodies.Count, 2_048, (_, start, end) =>
        {
            for (var index = start; index < end; index++)
                if (eligible[index]) _bodyCells[index] = CellFor(bodies[index].Position);
        });
        // Dictionary publication is serial; concurrent readers begin only after
        // this method returns. Body-index insertion order remains deterministic.
        for (var index = 0; index < bodies.Count; index++)
            if (eligible[index]) AddCell(index, _bodyCells[index]);
    }

    public void Add(int index, Vector4D position) => AddCell(index, CellFor(position));

    private void AddCell(int index, Cell4D cell)
    {
        ref var indices = ref CollectionsMarshal.GetValueRefOrAddDefault(
            _indicesByCell,
            cell,
            out var exists);
        if (!exists)
        {
            indices = _availableLists.Count > 0
                ? _availableLists.Pop()
                : new List<int>(capacity: 1);
            _occupiedCells.Add(cell);
        }
        indices!.Add(index);
    }

    public void CollectNeighborIndices(Vector4D position, List<int> destination) =>
        CollectNeighborIndices(CellFor(position), int.MinValue, destination);

    public void CollectCandidatePairs(
        List<(int FirstIndex, int SecondIndex)> destination)
    {
        destination.Clear();
        var workerCount = ParallelWork.WorkerCountFor(_occupiedCells.Count, 512);
        LastCandidateWorkerCount = workerCount;
        EnsureWorkerBuffers(workerCount);
        ParallelWork.ForRanges(
            _occupiedCells.Count,
            minimumItemsPerWorker: 512,
            (workerIndex, start, end) =>
            {
                var workerDestination = _workerPairs[workerIndex];
                workerDestination.Clear();
                for (var cellIndex = start; cellIndex < end; cellIndex++)
                {
                    CollectCellPairs(_occupiedCells[cellIndex], workerDestination);
                }
            });

        var pairCount = 0;
        for (var workerIndex = 0; workerIndex < workerCount; workerIndex++)
        {
            pairCount += _workerPairs[workerIndex].Count;
        }
        destination.EnsureCapacity(pairCount);
        for (var workerIndex = 0; workerIndex < workerCount; workerIndex++)
        {
            destination.AddRange(_workerPairs[workerIndex]);
        }
    }

    private void CollectCellPairs(
        Cell4D cell,
        List<(int FirstIndex, int SecondIndex)> destination)
    {
        var indices = _indicesByCell[cell];
        for (var firstOffset = 0; firstOffset < indices.Count - 1; firstOffset++)
        {
            for (var secondOffset = firstOffset + 1;
                 secondOffset < indices.Count;
                 secondOffset++)
            {
                AddOrderedPair(indices[firstOffset], indices[secondOffset], destination);
            }
        }

        foreach (var offset in ForwardNeighborOffsets)
        {
            var neighbor = new Cell4D(
                cell.X + offset.X,
                cell.Y + offset.Y,
                cell.Z + offset.Z,
                cell.W + offset.W);
            if (!_indicesByCell.TryGetValue(neighbor, out var neighborIndices))
            {
                continue;
            }

            foreach (var firstIndex in indices)
            {
                foreach (var secondIndex in neighborIndices)
                {
                    AddOrderedPair(firstIndex, secondIndex, destination);
                }
            }
        }
    }

    private void EnsureWorkerBuffers(int workerCount)
    {
        if (_workerPairs.Length >= workerCount)
        {
            return;
        }

        var previousLength = _workerPairs.Length;
        Array.Resize(ref _workerPairs, workerCount);
        for (var workerIndex = previousLength; workerIndex < workerCount; workerIndex++)
        {
            _workerPairs[workerIndex] = new List<(int FirstIndex, int SecondIndex)>();
        }
    }

    private void CollectNeighborIndices(
        Cell4D center,
        int minimumExclusive,
        List<int> destination)
    {
        destination.Clear();
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
                            for (var index = FirstGreaterThan(indices, minimumExclusive);
                                 index < indices.Count;
                                 index++)
                            {
                                destination.Add(indices[index]);
                            }
                        }
                    }
                }
            }
        }
    }

    private static int FirstGreaterThan(List<int> sortedIndices, int value)
    {
        var low = 0;
        var high = sortedIndices.Count;
        while (low < high)
        {
            var middle = low + ((high - low) / 2);
            if (sortedIndices[middle] <= value)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        return low;
    }

    private static void AddOrderedPair(
        int firstIndex,
        int secondIndex,
        List<(int FirstIndex, int SecondIndex)> destination)
    {
        destination.Add(firstIndex < secondIndex
            ? (firstIndex, secondIndex)
            : (secondIndex, firstIndex));
    }

    private static Cell4D[] CreateForwardNeighborOffsets()
    {
        var offsets = new List<Cell4D>(capacity: 40);
        for (var dx = -1; dx <= 1; dx++)
        {
            for (var dy = -1; dy <= 1; dy++)
            {
                for (var dz = -1; dz <= 1; dz++)
                {
                    for (var dw = -1; dw <= 1; dw++)
                    {
                        if (dx > 0 ||
                            (dx == 0 && dy > 0) ||
                            (dx == 0 && dy == 0 && dz > 0) ||
                            (dx == 0 && dy == 0 && dz == 0 && dw > 0))
                        {
                            offsets.Add(new Cell4D(dx, dy, dz, dw));
                        }
                    }
                }
            }
        }

        return [.. offsets];
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
