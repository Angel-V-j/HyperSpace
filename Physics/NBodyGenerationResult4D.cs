using System.Collections.Generic;

namespace HyperSpace.Physics;

public sealed record NBodyGenerationResult4D(
    IReadOnlyList<PhysicsBodyInitialState4D> Bodies,
    int RejectedPositionAttempts,
    double ElapsedMilliseconds);
