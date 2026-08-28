namespace HyperSpace.Mathematics;

/// <summary>
/// A renderer-independent three-dimensional position produced by 4D projection.
/// </summary>
public readonly record struct Vector3D(double X, double Y, double Z)
{
    public static Vector3D Zero => new(0.0, 0.0, 0.0);

    public bool IsFinite =>
        double.IsFinite(X) &&
        double.IsFinite(Y) &&
        double.IsFinite(Z);
}
