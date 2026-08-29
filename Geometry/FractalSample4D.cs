using HyperSpace.Mathematics;

namespace HyperSpace.Geometry;

/// <summary>
/// One sampled 4D position and the escape-time result of its Julia iteration.
/// </summary>
public readonly record struct FractalSample4D(
    Vector4D Position,
    int Iterations,
    bool IsBounded);
