using System;
using System.Collections.Generic;
using HyperSpace.Mathematics;

namespace HyperSpace.Physics;

/// <summary>
/// Bounded history of original 4D positions. Projection is intentionally deferred.
/// </summary>
public sealed class Trajectory4D
{
    public const int MinimumCapacity = 100;
    public const int MaximumCapacity = 5000;
    public const int DefaultCapacity = 1000;

    private readonly List<Vector4D> _points = new(DefaultCapacity);

    public IReadOnlyList<Vector4D> Points => _points;

    public int Capacity { get; private set; } = DefaultCapacity;

    public void Append(Vector4D point)
    {
        if (!point.IsFinite)
        {
            throw new ArgumentOutOfRangeException(nameof(point));
        }

        if (_points.Count == Capacity)
        {
            _points.RemoveAt(0);
        }

        _points.Add(point);
    }

    public void SetCapacity(int capacity)
    {
        if (capacity < MinimumCapacity || capacity > MaximumCapacity)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        Capacity = capacity;
        if (_points.Count > Capacity)
        {
            _points.RemoveRange(0, _points.Count - Capacity);
        }
    }

    public void Clear() => _points.Clear();
}
