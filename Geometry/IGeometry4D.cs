using System.Collections.Generic;
using HyperSpace.Mathematics;

namespace HyperSpace.Geometry;

/// <summary>
/// The common, immutable geometric input to the 4D projection pipeline.
/// </summary>
public interface IGeometry4D
{
    string Name { get; }

    GeometryVisualStyle4D VisualStyle { get; }

    IReadOnlyList<Vector4D> Vertices { get; }

    IReadOnlyList<Edge> Edges { get; }

    IReadOnlyList<Face4D> Faces { get; }

    IReadOnlyList<Cell4D> Cells { get; }

    string ResolutionDescription { get; }
}
