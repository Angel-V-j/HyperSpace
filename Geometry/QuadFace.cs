namespace HyperSpace.Geometry;

/// <summary>
/// Four cyclic vertex indices describing one square 2D face of a tesseract cell.
/// </summary>
public readonly record struct QuadFace(int A, int B, int C, int D);
