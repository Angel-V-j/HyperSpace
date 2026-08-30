using System;
using HyperSpace.Mathematics;
using HyperSpace.Physics;
using HyperSpace.UI;

namespace HyperSpace.Application;

/// <summary>
/// Translates physics-panel commands into changes to the physics experiments.
/// It owns only panel-facing state; simulation rules remain in the physics domain.
/// </summary>
internal sealed class PhysicsCommandController
{
    private const double GravityAdjustmentStep = 0.5;
    private const double InitialVelocityAdjustmentStep = 1.0;
    private const double RestitutionAdjustmentStep = 0.1;
    private const double GravitationalConstantStep = 0.01;
    private const double GravitySofteningStep = 0.05;
    private const double CentralMassStep = 100.0;
    private const double OrbiterPositionStep = 0.5;
    private const double OrbiterVelocityStep = 0.25;
    private const int GravityTrailLengthStep = 250;

    private readonly PhysicsWorld4D _world;
    private readonly GravityLab4D _gravityLab;
    private readonly NBodyLab4D _nBodyLab;

    public PhysicsCommandController(
        PhysicsWorld4D world,
        GravityLab4D gravityLab,
        NBodyLab4D nBodyLab)
    {
        _world = world;
        _gravityLab = gravityLab;
        _nBodyLab = nBodyLab;
    }

    public Vector4D PendingParticleVelocity { get; private set; } = Vector4D.Zero;
    public bool ShowPhysicsPlane { get; private set; }
    public bool ShowGravityTrail { get; private set; } = true;
    public bool ShowGravityField { get; private set; } = true;

    public bool TryHandle(TransformationCommand command, bool isNBodyView)
    {
        switch (command)
        {
            case TransformationCommand.TogglePhysicsEnabled:
                _world.ToggleEnabled();
                return true;
            case TransformationCommand.PlayPhysics:
                _world.Play();
                return true;
            case TransformationCommand.PausePhysics:
                _world.Pause();
                return true;
            case TransformationCommand.StepPhysics:
                _world.StepOnce();
                return true;
            case TransformationCommand.DecreaseTimeScale:
                _world.AdjustTimeScale(-1);
                return true;
            case TransformationCommand.IncreaseTimeScale:
                _world.AdjustTimeScale(+1);
                return true;
            case TransformationCommand.DecreaseGravityX:
                AdjustGravity(new Vector4D(-GravityAdjustmentStep, 0.0, 0.0, 0.0));
                return true;
            case TransformationCommand.IncreaseGravityX:
                AdjustGravity(new Vector4D(GravityAdjustmentStep, 0.0, 0.0, 0.0));
                return true;
            case TransformationCommand.DecreaseGravityY:
                AdjustGravity(new Vector4D(0.0, -GravityAdjustmentStep, 0.0, 0.0));
                return true;
            case TransformationCommand.IncreaseGravityY:
                AdjustGravity(new Vector4D(0.0, GravityAdjustmentStep, 0.0, 0.0));
                return true;
            case TransformationCommand.DecreaseGravityZ:
                AdjustGravity(new Vector4D(0.0, 0.0, -GravityAdjustmentStep, 0.0));
                return true;
            case TransformationCommand.IncreaseGravityZ:
                AdjustGravity(new Vector4D(0.0, 0.0, GravityAdjustmentStep, 0.0));
                return true;
            case TransformationCommand.DecreaseGravityW:
                AdjustGravity(new Vector4D(0.0, 0.0, 0.0, -GravityAdjustmentStep));
                return true;
            case TransformationCommand.IncreaseGravityW:
                AdjustGravity(new Vector4D(0.0, 0.0, 0.0, GravityAdjustmentStep));
                return true;
            case TransformationCommand.SetZeroGravity:
                _world.SetGravity(Vector4D.Zero);
                return true;
            case TransformationCommand.SetYGravity:
                _world.SetGravity(new Vector4D(0.0, -9.8, 0.0, 0.0));
                return true;
            case TransformationCommand.SetWGravity:
                _world.SetGravity(new Vector4D(0.0, 0.0, 0.0, -9.8));
                return true;
            case TransformationCommand.SetYWGravity:
                _world.SetGravity(new Vector4D(0.0, -9.8, 0.0, -9.8));
                return true;
            case TransformationCommand.DecreaseInitialVelocityX:
                AdjustInitialVelocity(new Vector4D(-InitialVelocityAdjustmentStep, 0.0, 0.0, 0.0));
                return true;
            case TransformationCommand.IncreaseInitialVelocityX:
                AdjustInitialVelocity(new Vector4D(InitialVelocityAdjustmentStep, 0.0, 0.0, 0.0));
                return true;
            case TransformationCommand.DecreaseInitialVelocityY:
                AdjustInitialVelocity(new Vector4D(0.0, -InitialVelocityAdjustmentStep, 0.0, 0.0));
                return true;
            case TransformationCommand.IncreaseInitialVelocityY:
                AdjustInitialVelocity(new Vector4D(0.0, InitialVelocityAdjustmentStep, 0.0, 0.0));
                return true;
            case TransformationCommand.DecreaseInitialVelocityZ:
                AdjustInitialVelocity(new Vector4D(0.0, 0.0, -InitialVelocityAdjustmentStep, 0.0));
                return true;
            case TransformationCommand.IncreaseInitialVelocityZ:
                AdjustInitialVelocity(new Vector4D(0.0, 0.0, InitialVelocityAdjustmentStep, 0.0));
                return true;
            case TransformationCommand.DecreaseInitialVelocityW:
                AdjustInitialVelocity(new Vector4D(0.0, 0.0, 0.0, -InitialVelocityAdjustmentStep));
                return true;
            case TransformationCommand.IncreaseInitialVelocityW:
                AdjustInitialVelocity(new Vector4D(0.0, 0.0, 0.0, InitialVelocityAdjustmentStep));
                return true;
            case TransformationCommand.SpawnParticle:
                _world.SpawnParticles(1, PendingParticleVelocity);
                return true;
            case TransformationCommand.SpawnTenParticles:
                _world.SpawnParticles(10, PendingParticleVelocity);
                return true;
            case TransformationCommand.ClearParticles:
                _world.Clear();
                _gravityLab.DetachBodies();
                return true;
            case TransformationCommand.TogglePhysicsCollisions:
                _world.ToggleCollisions();
                return true;
            case TransformationCommand.DecreaseRestitution:
                _world.SetRestitution(Math.Clamp(
                    _world.Restitution - RestitutionAdjustmentStep,
                    0.0,
                    1.0));
                return true;
            case TransformationCommand.IncreaseRestitution:
                _world.SetRestitution(Math.Clamp(
                    _world.Restitution + RestitutionAdjustmentStep,
                    0.0,
                    1.0));
                return true;
            case TransformationCommand.TogglePhysicsPlane:
                ShowPhysicsPlane = !ShowPhysicsPlane;
                return true;
            case TransformationCommand.ToggleMutualGravity:
                _world.ToggleMutualGravity();
                return true;
            case TransformationCommand.DecreaseGravitationalConstant:
                AdjustGravitationalConstant(-GravitationalConstantStep, isNBodyView);
                return true;
            case TransformationCommand.IncreaseGravitationalConstant:
                AdjustGravitationalConstant(GravitationalConstantStep, isNBodyView);
                return true;
            case TransformationCommand.DecreaseGravitySoftening:
                AdjustGravitySoftening(-GravitySofteningStep, isNBodyView);
                return true;
            case TransformationCommand.IncreaseGravitySoftening:
                AdjustGravitySoftening(GravitySofteningStep, isNBodyView);
                return true;
            case TransformationCommand.DecreaseCentralMass:
                _gravityLab.AdjustCentralMass(-CentralMassStep);
                return true;
            case TransformationCommand.IncreaseCentralMass:
                _gravityLab.AdjustCentralMass(CentralMassStep);
                return true;
            case TransformationCommand.DecreaseOrbiterPositionX:
                _gravityLab.AdjustOrbiterInitialPosition(new Vector4D(-OrbiterPositionStep, 0.0, 0.0, 0.0));
                return true;
            case TransformationCommand.IncreaseOrbiterPositionX:
                _gravityLab.AdjustOrbiterInitialPosition(new Vector4D(OrbiterPositionStep, 0.0, 0.0, 0.0));
                return true;
            case TransformationCommand.DecreaseOrbiterPositionY:
                _gravityLab.AdjustOrbiterInitialPosition(new Vector4D(0.0, -OrbiterPositionStep, 0.0, 0.0));
                return true;
            case TransformationCommand.IncreaseOrbiterPositionY:
                _gravityLab.AdjustOrbiterInitialPosition(new Vector4D(0.0, OrbiterPositionStep, 0.0, 0.0));
                return true;
            case TransformationCommand.DecreaseOrbiterPositionZ:
                _gravityLab.AdjustOrbiterInitialPosition(new Vector4D(0.0, 0.0, -OrbiterPositionStep, 0.0));
                return true;
            case TransformationCommand.IncreaseOrbiterPositionZ:
                _gravityLab.AdjustOrbiterInitialPosition(new Vector4D(0.0, 0.0, OrbiterPositionStep, 0.0));
                return true;
            case TransformationCommand.DecreaseOrbiterPositionW:
                _gravityLab.AdjustOrbiterInitialPosition(new Vector4D(0.0, 0.0, 0.0, -OrbiterPositionStep));
                return true;
            case TransformationCommand.IncreaseOrbiterPositionW:
                _gravityLab.AdjustOrbiterInitialPosition(new Vector4D(0.0, 0.0, 0.0, OrbiterPositionStep));
                return true;
            case TransformationCommand.DecreaseOrbiterVelocityX:
                _gravityLab.AdjustOrbiterInitialVelocity(new Vector4D(-OrbiterVelocityStep, 0.0, 0.0, 0.0));
                return true;
            case TransformationCommand.IncreaseOrbiterVelocityX:
                _gravityLab.AdjustOrbiterInitialVelocity(new Vector4D(OrbiterVelocityStep, 0.0, 0.0, 0.0));
                return true;
            case TransformationCommand.DecreaseOrbiterVelocityY:
                _gravityLab.AdjustOrbiterInitialVelocity(new Vector4D(0.0, -OrbiterVelocityStep, 0.0, 0.0));
                return true;
            case TransformationCommand.IncreaseOrbiterVelocityY:
                _gravityLab.AdjustOrbiterInitialVelocity(new Vector4D(0.0, OrbiterVelocityStep, 0.0, 0.0));
                return true;
            case TransformationCommand.DecreaseOrbiterVelocityZ:
                _gravityLab.AdjustOrbiterInitialVelocity(new Vector4D(0.0, 0.0, -OrbiterVelocityStep, 0.0));
                return true;
            case TransformationCommand.IncreaseOrbiterVelocityZ:
                _gravityLab.AdjustOrbiterInitialVelocity(new Vector4D(0.0, 0.0, OrbiterVelocityStep, 0.0));
                return true;
            case TransformationCommand.DecreaseOrbiterVelocityW:
                _gravityLab.AdjustOrbiterInitialVelocity(new Vector4D(0.0, 0.0, 0.0, -OrbiterVelocityStep));
                return true;
            case TransformationCommand.IncreaseOrbiterVelocityW:
                _gravityLab.AdjustOrbiterInitialVelocity(new Vector4D(0.0, 0.0, 0.0, OrbiterVelocityStep));
                return true;
            case TransformationCommand.SelectLowOrbiterVelocity:
                _gravityLab.SetVelocityPreset(GravityLab4D.LowVelocity);
                return true;
            case TransformationCommand.SelectMediumOrbiterVelocity:
                _gravityLab.SetVelocityPreset(GravityLab4D.MediumVelocity);
                return true;
            case TransformationCommand.SelectHighOrbiterVelocity:
                _gravityLab.SetVelocityPreset(GravityLab4D.HighVelocity);
                return true;
            case TransformationCommand.SetOrbiterXYVelocity:
                _gravityLab.UseXYVelocity();
                return true;
            case TransformationCommand.SetOrbiterXYWVelocity:
                _gravityLab.UseXYWVelocity();
                return true;
            case TransformationCommand.ToggleGravityTrail:
                ShowGravityTrail = !ShowGravityTrail;
                return true;
            case TransformationCommand.ToggleGravityField:
                ShowGravityField = !ShowGravityField;
                return true;
            case TransformationCommand.ClearGravityTrail:
                _gravityLab.ClearTrail();
                return true;
            case TransformationCommand.DecreaseGravityTrailLength:
                _gravityLab.AdjustTrailCapacity(-GravityTrailLengthStep);
                return true;
            case TransformationCommand.IncreaseGravityTrailLength:
                _gravityLab.AdjustTrailCapacity(GravityTrailLengthStep);
                return true;
            case TransformationCommand.ResetGravityExperiment:
                _gravityLab.ResetExperiment();
                return true;
            case TransformationCommand.GenerateNBodySystem:
                _nBodyLab.GenerateSystem();
                return true;
            case TransformationCommand.ResetNBodySystem:
                _nBodyLab.ResetSystem();
                return true;
            case TransformationCommand.DecreaseNBodyRangeX:
                _nBodyLab.Settings.AdjustPositionHalfRange(0, -1.0);
                return true;
            case TransformationCommand.IncreaseNBodyRangeX:
                _nBodyLab.Settings.AdjustPositionHalfRange(0, 1.0);
                return true;
            case TransformationCommand.DecreaseNBodyRangeY:
                _nBodyLab.Settings.AdjustPositionHalfRange(1, -1.0);
                return true;
            case TransformationCommand.IncreaseNBodyRangeY:
                _nBodyLab.Settings.AdjustPositionHalfRange(1, 1.0);
                return true;
            case TransformationCommand.DecreaseNBodyRangeZ:
                _nBodyLab.Settings.AdjustPositionHalfRange(2, -1.0);
                return true;
            case TransformationCommand.IncreaseNBodyRangeZ:
                _nBodyLab.Settings.AdjustPositionHalfRange(2, 1.0);
                return true;
            case TransformationCommand.DecreaseNBodyRangeW:
                _nBodyLab.Settings.AdjustPositionHalfRange(3, -1.0);
                return true;
            case TransformationCommand.IncreaseNBodyRangeW:
                _nBodyLab.Settings.AdjustPositionHalfRange(3, 1.0);
                return true;
            case TransformationCommand.DecreaseNBodyMinimumSpeed:
                _nBodyLab.Settings.AdjustMinimumSpeed(-0.1);
                return true;
            case TransformationCommand.IncreaseNBodyMinimumSpeed:
                _nBodyLab.Settings.AdjustMinimumSpeed(0.1);
                return true;
            case TransformationCommand.DecreaseNBodyMaximumSpeed:
                _nBodyLab.Settings.AdjustMaximumSpeed(-0.1);
                return true;
            case TransformationCommand.IncreaseNBodyMaximumSpeed:
                _nBodyLab.Settings.AdjustMaximumSpeed(0.1);
                return true;
            case TransformationCommand.DecreaseNBodyMinimumMass:
                _nBodyLab.Settings.AdjustMinimumMass(-0.5);
                return true;
            case TransformationCommand.IncreaseNBodyMinimumMass:
                _nBodyLab.Settings.AdjustMinimumMass(0.5);
                return true;
            case TransformationCommand.DecreaseNBodyMaximumMass:
                _nBodyLab.Settings.AdjustMaximumMass(-0.5);
                return true;
            case TransformationCommand.IncreaseNBodyMaximumMass:
                _nBodyLab.Settings.AdjustMaximumMass(0.5);
                return true;
            case TransformationCommand.DecreaseNBodyRadiusScale:
                _nBodyLab.Settings.AdjustRadiusScale(-0.01);
                return true;
            case TransformationCommand.IncreaseNBodyRadiusScale:
                _nBodyLab.Settings.AdjustRadiusScale(0.01);
                return true;
            case TransformationCommand.DecreaseNBodyPointScale:
                _nBodyLab.Settings.AdjustPointScale(-0.25);
                return true;
            case TransformationCommand.IncreaseNBodyPointScale:
                _nBodyLab.Settings.AdjustPointScale(0.25);
                return true;
            case TransformationCommand.ToggleNBodyGravity:
                _nBodyLab.ToggleGravity();
                return true;
            case TransformationCommand.ToggleNBodyAggregation:
                _nBodyLab.ToggleAggregation();
                return true;
            case TransformationCommand.SelectNBodyExactGravity:
                _nBodyLab.SetGravityMode(GravityMode4D.Exact);
                return true;
            case TransformationCommand.SelectNBodyApproximateGravity:
                _nBodyLab.SetGravityMode(GravityMode4D.MeanFieldApproximate);
                return true;
            case TransformationCommand.ColorNBodyByW:
                _nBodyLab.SetColorMode(NBodyColorMode4D.WDepth);
                return true;
            case TransformationCommand.ColorNBodyByMass:
                _nBodyLab.SetColorMode(NBodyColorMode4D.Mass);
                return true;
            case TransformationCommand.ColorNBodyByAcceleration:
                _nBodyLab.SetColorMode(NBodyColorMode4D.Acceleration);
                return true;
            case TransformationCommand.ColorNBodyBySpeed:
                _nBodyLab.SetColorMode(NBodyColorMode4D.Speed);
                return true;
            case TransformationCommand.DisableNBodyTrail:
                _nBodyLab.SetTrailMode(NBodyTrailMode4D.Off);
                return true;
            case TransformationCommand.EnableSelectedNBodyTrail:
                _nBodyLab.SetTrailMode(NBodyTrailMode4D.SelectedBody);
                return true;
            default:
                return false;
        }
    }

    private void AdjustGravity(Vector4D delta) =>
        _world.SetGravity(_world.Gravity + delta);

    private void AdjustInitialVelocity(Vector4D delta) =>
        PendingParticleVelocity += delta;

    private void AdjustGravitationalConstant(double delta, bool isNBodyView)
    {
        if (isNBodyView)
        {
            _nBodyLab.SetGravitationalConstant(_nBodyLab.GravitationalConstant + delta);
            return;
        }

        _world.SetGravitationalConstant(Math.Clamp(
            _world.GravitySystem.GravitationalConstant + delta,
            0.0,
            0.25));
    }

    private void AdjustGravitySoftening(double delta, bool isNBodyView)
    {
        if (isNBodyView)
        {
            _nBodyLab.SetSoftening(_nBodyLab.Softening + delta);
            return;
        }

        _world.SetGravitySoftening(Math.Clamp(
            _world.GravitySystem.Softening + delta,
            0.05,
            2.0));
    }
}
