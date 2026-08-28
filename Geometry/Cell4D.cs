using System;
using System.Collections.Generic;
using System.Linq;

namespace HyperSpace.Geometry;

/// <summary>
/// One three-dimensional boundary cell of a four-dimensional polytope.
/// </summary>
public sealed class Cell4D
{
    private readonly int[] _vertexIndices;
    private readonly Face4D[] _faces;

    public Cell4D(
        string label,
        IReadOnlyList<int> vertexIndices,
        IReadOnlyList<Face4D> faces,
        CoordinateAxis4D? fixedAxis = null,
        int fixedSign = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        Label = label;
        _vertexIndices = vertexIndices.Distinct().ToArray();
        _faces = faces.ToArray();
        FixedAxis = fixedAxis;
        FixedSign = fixedSign;
    }

    public string Label { get; }

    public IReadOnlyList<int> VertexIndices => _vertexIndices;

    public IReadOnlyList<Face4D> Faces => _faces;

    // These optional fields give tesseract cells their useful X-/X+ semantics
    // without forcing that coordinate-specific concept onto every polytope.
    public CoordinateAxis4D? FixedAxis { get; }

    public int FixedSign { get; }
}
