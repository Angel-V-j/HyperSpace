namespace HyperSpace.Physics;

public readonly record struct EnergyDiagnostics4D(
    double KineticEnergy,
    double PotentialEnergy,
    double TotalEnergy,
    double InitialTotalEnergy,
    double DriftPercent,
    bool HasReference,
    bool IsConservativeModel);
