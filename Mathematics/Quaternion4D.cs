using System;

namespace HyperSpace.Mathematics;

/// <summary>
/// A quaternion q = A + B i + C j + D k.
/// Quaternion multiplication is non-commutative and is deliberately separate
/// from Vector4D, whose four components are spatial coordinates.
/// </summary>
public readonly record struct Quaternion4D(double A, double B, double C, double D)
{
    public static Quaternion4D Zero => new(0.0, 0.0, 0.0, 0.0);

    public bool IsFinite =>
        double.IsFinite(A) &&
        double.IsFinite(B) &&
        double.IsFinite(C) &&
        double.IsFinite(D);

    public double SquaredMagnitude =>
        (A * A) + (B * B) + (C * C) + (D * D);

    public double Magnitude => Math.Sqrt(SquaredMagnitude);

    public Vector4D ToVector4D() => new(A, B, C, D);

    public static Quaternion4D FromVector4D(Vector4D value) =>
        new(value.X, value.Y, value.Z, value.W);

    public Quaternion4D Square() =>
        new(
            (A * A) - (B * B) - (C * C) - (D * D),
            2.0 * A * B,
            2.0 * A * C,
            2.0 * A * D);

    public static Quaternion4D operator +(Quaternion4D left, Quaternion4D right) =>
        new(
            left.A + right.A,
            left.B + right.B,
            left.C + right.C,
            left.D + right.D);

    public static Quaternion4D operator *(double scalar, Quaternion4D value) =>
        new(
            scalar * value.A,
            scalar * value.B,
            scalar * value.C,
            scalar * value.D);

    public static Quaternion4D operator *(Quaternion4D left, Quaternion4D right) =>
        new(
            (left.A * right.A) - (left.B * right.B) -
                (left.C * right.C) - (left.D * right.D),
            (left.A * right.B) + (left.B * right.A) +
                (left.C * right.D) - (left.D * right.C),
            (left.A * right.C) - (left.B * right.D) +
                (left.C * right.A) + (left.D * right.B),
            (left.A * right.D) + (left.B * right.C) -
                (left.C * right.B) + (left.D * right.A));
}
