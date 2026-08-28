using System.Collections.Generic;
using System.Linq;

namespace HyperSpace.Geometry;

/// <summary>
/// One cubical boundary cell of a tesseract, obtained by fixing one axis to -/+ extent.
/// </summary>
public sealed class TesseractCell4D
{
    private readonly int[] _vertexIndices;
    private readonly QuadFace[] _faces;

    public TesseractCell4D(
        CoordinateAxis4D fixedAxis,
        int fixedSign,
        IReadOnlyList<int> vertexIndices,
        IReadOnlyList<QuadFace> faces)
    {
        FixedAxis = fixedAxis;
        FixedSign = fixedSign;
        _vertexIndices = vertexIndices.ToArray();
        _faces = faces.ToArray();
    }

    public CoordinateAxis4D FixedAxis { get; }

    public int FixedSign { get; }

    public string Label => $"{FixedAxis}{(FixedSign < 0 ? "-" : "+")}";

    public IReadOnlyList<int> VertexIndices => _vertexIndices;

    public IReadOnlyList<QuadFace> Faces => _faces;
}
