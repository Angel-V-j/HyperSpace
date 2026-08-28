using System;
using System.Collections.Generic;
using HyperSpace.Mathematics;

namespace HyperSpace.Geometry;

/// <summary>
/// An immutable polyline approximation of a continuous parametric 4D curve.
/// </summary>
public sealed class Spiral4D : IGeometry4D
{
    private readonly Vector4D[] _vertices;
    private readonly Edge[] _edges;

    internal Spiral4D(SpiralParameters parameters, Vector4D[] vertices, Edge[] edges)
    {
        Parameters = parameters;
        _vertices = vertices;
        _edges = edges;
    }

    public string Name => "4D Spiral";

    public GeometryVisualStyle4D VisualStyle => GeometryVisualStyle4D.Spiral;

    public IReadOnlyList<Vector4D> Vertices => _vertices;

    public IReadOnlyList<Edge> Edges => _edges;

    public IReadOnlyList<Face4D> Faces => Array.Empty<Face4D>();

    public IReadOnlyList<Cell4D> Cells => Array.Empty<Cell4D>();

    public string ResolutionDescription =>
        $"{Parameters.SampleCount} samples, t {Parameters.TStart:0.00}..{Parameters.TEnd:0.00}";

    public SpiralParameters Parameters { get; }
}
