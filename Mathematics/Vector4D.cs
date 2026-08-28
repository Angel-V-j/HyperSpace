namespace HyperSpace.Mathematics;

/// <summary>
/// A vector in four-dimensional Euclidean space.
/// </summary>
public readonly record struct Vector4D(double X, double Y, double Z, double W)
{
    public static Vector4D Zero => new(0.0, 0.0, 0.0, 0.0);

    public bool IsFinite =>
        double.IsFinite(X) &&
        double.IsFinite(Y) &&
        double.IsFinite(Z) &&
        double.IsFinite(W);

    public double LengthSquared => X * X + Y * Y + Z * Z + W * W;

    public double Length => System.Math.Sqrt(LengthSquared);

    public static Vector4D operator +(Vector4D left, Vector4D right) =>
        new(
            left.X + right.X,
            left.Y + right.Y,
            left.Z + right.Z,
            left.W + right.W);

    public static Vector4D operator -(Vector4D left, Vector4D right) =>
        new(
            left.X - right.X,
            left.Y - right.Y,
            left.Z - right.Z,
            left.W - right.W);

    public static Vector4D operator *(Vector4D vector, double scalar) =>
        new(
            vector.X * scalar,
            vector.Y * scalar,
            vector.Z * scalar,
            vector.W * scalar);
}
