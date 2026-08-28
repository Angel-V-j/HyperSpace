using System;
using System.Collections.Generic;
using System.Linq;

namespace HyperSpace.Geometry;

/// <summary>
/// Cyclic vertex indices of one polygonal 2-face in a 4D object's topology.
/// The current renderer triangulates this polygon as a fan.
/// </summary>
public sealed class Face4D
{
    private readonly int[] _vertexIndices;

    public Face4D(params int[] vertexIndices)
    {
        ArgumentNullException.ThrowIfNull(vertexIndices);
        if (vertexIndices.Length < 3)
        {
            throw new ArgumentException("A face needs at least three vertices.", nameof(vertexIndices));
        }

        if (vertexIndices.Distinct().Count() != vertexIndices.Length)
        {
            throw new ArgumentException("A face cannot repeat a vertex.", nameof(vertexIndices));
        }

        _vertexIndices = [.. vertexIndices];
    }

    public IReadOnlyList<int> VertexIndices => _vertexIndices;
}
