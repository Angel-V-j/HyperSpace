namespace HyperSpace.Geometry;

/// <summary>
/// Indices of the two vertices joined by a wireframe edge.
/// </summary>
public readonly record struct Edge(int Start, int End, EdgeKind Kind = EdgeKind.Default);
