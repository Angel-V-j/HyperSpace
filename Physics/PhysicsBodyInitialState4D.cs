using HyperSpace.Mathematics;

namespace HyperSpace.Physics;

public readonly record struct PhysicsBodyInitialState4D(
    Vector4D Position,
    Vector4D Velocity,
    double Mass,
    double Radius,
    bool IsStatic = false);
