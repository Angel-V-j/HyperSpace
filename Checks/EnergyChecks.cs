using System;
using HyperSpace.Physics;

internal static class EnergyChecks
{
    public static void CheckEnergyStability()
    {
        CheckPotentialGradient();
        CheckMode(bodyCount: 500, maximumDriftPercent: 1.0);
        CheckMode(bodyCount: 2_500, maximumDriftPercent: 0.01);
        CheckMergeDoesNotCreateKineticEnergy();
    }

    public static void RunAudit()
    {
        var world = new PhysicsWorld4D();
        using var lab = new NBodyLab4D(world);
        lab.Settings.TryApplyBodyCount("2500", out _);
        if (!lab.GenerateSystem())
        {
            throw new InvalidOperationException(lab.LastGenerationMessage);
        }
        lab.ToggleAggregation();
        world.ResetEnergyReference();
        Print(world);
        for (var step = 1; step <= 940; step++)
        {
            world.StepOnce();
            if (step % 100 == 0 || step == 940) Print(world);
        }
    }

    private static void Print(PhysicsWorld4D world)
    {
        var energy = world.EnergyDiagnostics;
        Console.WriteLine(
            $"ENERGY step={world.CompletedStepCount,4} K={energy.KineticEnergy,14:0.000} " +
            $"U={energy.PotentialEnergy,14:0.000} E={energy.TotalEnergy,14:0.000} " +
            $"drift={energy.DriftPercent,12:0.000000}%");
    }

    private static void CheckMode(int bodyCount, double maximumDriftPercent)
    {
        var world = new PhysicsWorld4D();
        using var lab = new NBodyLab4D(world);
        lab.Settings.TryApplyBodyCount(bodyCount.ToString(), out _);
        if (!lab.GenerateSystem()) throw new InvalidOperationException(lab.LastGenerationMessage);
        lab.ToggleAggregation();
        world.ResetEnergyReference();
        for (var step = 0; step < 940; step++) world.StepOnce();
        var energy = world.EnergyDiagnostics;
        if (!double.IsFinite(energy.TotalEnergy) ||
            Math.Abs(energy.DriftPercent) > maximumDriftPercent)
        {
            throw new InvalidOperationException(
                $"{bodyCount:N0}-body {world.EffectiveGravityMode} energy drift " +
                $"was {energy.DriftPercent:F6}% (limit {maximumDriftPercent:F3}%).");
        }
    }

    private static void CheckPotentialGradient()
    {
        const double delta = 1e-5;
        var gravity = new GravitySystem4D();
        gravity.SetGravitationalConstant(0.4);
        gravity.SetSoftening(0.3);
        var source = new PhysicsBody4D(1, new HyperSpace.Mathematics.Vector4D(1.7, 0, 0, 0),
            HyperSpace.Mathematics.Vector4D.Zero, 3.0);
        var targetMinus = new PhysicsBody4D(2, new HyperSpace.Mathematics.Vector4D(-delta, 0, 0, 0),
            HyperSpace.Mathematics.Vector4D.Zero, 2.0);
        var targetPlus = new PhysicsBody4D(3, new HyperSpace.Mathematics.Vector4D(delta, 0, 0, 0),
            HyperSpace.Mathematics.Vector4D.Zero, 2.0);
        var derivative = (gravity.PairPotentialEnergy(targetPlus, source) -
            gravity.PairPotentialEnergy(targetMinus, source)) / (2.0 * delta);
        var acceleration = gravity.AccelerationToward(
            HyperSpace.Mathematics.Vector4D.Zero, source.Position, source.Mass);
        if (Math.Abs(acceleration.X + derivative / targetPlus.Mass) > 1e-9)
        {
            throw new InvalidOperationException("Gravity force and softened potential are inconsistent.");
        }
    }

    private static void CheckMergeDoesNotCreateKineticEnergy()
    {
        var bodies = new[]
        {
            new PhysicsBody4D(1, HyperSpace.Mathematics.Vector4D.Zero,
                new HyperSpace.Mathematics.Vector4D(3, 0, 0, 0), 2.0, radius: 1.0),
            new PhysicsBody4D(2, new HyperSpace.Mathematics.Vector4D(0.5, 0, 0, 0),
                new HyperSpace.Mathematics.Vector4D(-1, 0, 0, 0), 4.0, radius: 1.0)
        };
        var before = bodies[0].KineticEnergy + bodies[1].KineticEnergy;
        var collisions = new AggregationCollisionSystem4D().Resolve(bodies);
        var after = bodies[0].KineticEnergy + bodies[1].KineticEnergy;
        if (collisions != 1 || after > before + 1e-12)
        {
            throw new InvalidOperationException("An inelastic merge created kinetic energy.");
        }
    }
}
