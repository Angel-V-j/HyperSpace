using System.Collections.Generic;
using System.Linq;

namespace HyperSpace.Geometry;

/// <summary>
/// The explicit 3D representation passed from 4D projection to rendering.
/// </summary>
public sealed class Wireframe3D
{
    private readonly ProjectedVertex3D[] _vertices;
    private readonly Edge[] _edges;

    public Wireframe3D(ProjectedVertex3D[] vertices, IReadOnlyList<Edge> edges)
    {
        _vertices = vertices;
        _edges = edges.ToArray();
    }

    public IReadOnlyList<ProjectedVertex3D> Vertices => _vertices;

    public IReadOnlyList<Edge> Edges => _edges;

    public int VisibleVertexCount => _vertices.Count(vertex => vertex.IsVisible);

    public int VisibleEdgeCount => _edges.Count(
        edge => _vertices[edge.Start].IsVisible && _vertices[edge.End].IsVisible);
}
