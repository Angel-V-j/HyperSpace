using System;
using HyperSpace.Diagnostics;

namespace HyperSpace.Physics;

/// <summary>
/// Owns N-body generation settings and visualization state while reusing PhysicsWorld4D.
/// </summary>
public sealed class NBodyLab4D : IDisposable
{
    public const double DefaultGravitationalConstant = 0.060;
    public const double DefaultSoftening = 0.25;

    private readonly PhysicsWorld4D _world;
    private readonly NBodyGenerator4D _generator = new();
    private int _generatedWorldVersion = -1;
    private PhysicsBody4D? _trailedBody;

    public NBodyLab4D(PhysicsWorld4D world)
    {
        _world = world;
        _world.FixedStepCompleted += RecordTrailPoint;
    }

    public NBodyGenerationSettings4D Settings { get; } = new();
    public NBodyColorMode4D ColorMode { get; private set; } = NBodyColorMode4D.WDepth;
    public NBodyTrailMode4D TrailMode { get; private set; } = NBodyTrailMode4D.Off;
    public double GravitationalConstant { get; private set; } = DefaultGravitationalConstant;
    public double Softening { get; private set; } = DefaultSoftening;
    public GravityMode4D GravityMode { get; private set; } = GravityMode4D.Exact;
    public bool GravityEnabled { get; private set; } = true;
    public bool AggregationEnabled { get; private set; } = true;
    public Trajectory4D SelectedTrail { get; } = new();
    public string LastGenerationMessage { get; private set; } = "Not generated";
    public NBodyGenerationResult4D? LastGeneration { get; private set; }
    public bool HasSystem => _generatedWorldVersion == _world.StateVersion && _world.Bodies.Count > 0;

    public bool GenerateSystem()
    {
        try
        {
            // Generate first: an impossible dense setup leaves the current world intact.
            var result = _generator.Generate(Settings);
            _world.Pause();
            _world.SetMutualGravityEnabled(false);
            _world.SetGravity(HyperSpace.Mathematics.Vector4D.Zero);
            _world.SetCollisionsEnabled(false);
            _world.SetGravityMode(GravityMode);
            _world.SetGravitationalConstant(GravitationalConstant);
            _world.SetGravitySoftening(Softening);
            _world.SetAggregationRadiusScale(Settings.RadiusScale);
            _world.SetAggregationCollisionInterval(RecommendedCollisionInterval(Settings.BodyCount));
            _world.ReplaceBodies(result.Bodies);
            _world.SetAggregationEnabled(AggregationEnabled);
            _world.SetMutualGravityEnabled(GravityEnabled);

            _generatedWorldVersion = _world.StateVersion;
            LastGeneration = result;
            LastGenerationMessage =
                $"Ready {result.Bodies.Count:N0}  {result.ElapsedMilliseconds:F1} ms  " +
                $"retry {result.RejectedPositionAttempts:N0}";
            SetTrailTarget(_world.SelectedBody);

            return true;
        }
        catch (InvalidOperationException exception)
        {
            LastGenerationMessage = exception.Message;
            return false;
        }
    }

    public void ResetSystem() => GenerateSystem();
    public void SetColorMode(NBodyColorMode4D mode) => ColorMode = mode;

    public bool SelectBody(PhysicsBody4D body)
    {
        if (!_world.SelectBody(body))
        {
            return false;
        }

        SetTrailTarget(body);
        return true;
    }

    public void SetGravitationalConstant(double value)
    {
        GravitationalConstant = Math.Clamp(value, 0.0, 0.25);
        if (HasSystem)
        {
            _world.SetGravitationalConstant(GravitationalConstant);
        }
    }

    public void SetSoftening(double value)
    {
        Softening = Math.Clamp(value, 0.05, 2.0);
        if (HasSystem)
        {
            _world.SetGravitySoftening(Softening);
        }
    }

    public void SetGravityMode(GravityMode4D mode)
    {
        GravityMode = mode;
        if (HasSystem)
        {
            _world.SetGravityMode(mode);
        }
    }

    public void ToggleGravity()
    {
        GravityEnabled = !GravityEnabled;
        if (HasSystem)
        {
            _world.SetMutualGravityEnabled(GravityEnabled);
        }
    }

    public void ToggleAggregation()
    {
        AggregationEnabled = !AggregationEnabled;
        if (HasSystem)
        {
            _world.SetAggregationEnabled(AggregationEnabled);
        }
    }

    public void SetTrailMode(NBodyTrailMode4D mode)
    {
        TrailMode = mode;
        SetTrailTarget(_world.SelectedBody);
    }

    public void ClearTrail() => SelectedTrail.Clear();

    public void Dispose() => _world.FixedStepCompleted -= RecordTrailPoint;

    public static int RecommendedCollisionInterval(int bodyCount) => bodyCount switch
    {
        <= 1_000 => 1,
        <= 5_000 => 2,
        _ => 4
    };

    private void RecordTrailPoint()
    {
        var startedAt = _world.Performance.BeginPhase();
        if (!HasSystem || TrailMode == NBodyTrailMode4D.Off)
        {
            _world.Performance.EndPhase(PerformancePhase.TrailUpdate, startedAt);
            return;
        }

        if (!ReferenceEquals(_trailedBody, _world.SelectedBody))
        {
            SetTrailTarget(_world.SelectedBody);
        }

        if (_trailedBody is not null && _trailedBody.IsAlive)
        {
            SelectedTrail.Append(_trailedBody.Position);
        }
        _world.Performance.EndPhase(PerformancePhase.TrailUpdate, startedAt);
    }

    private void SetTrailTarget(PhysicsBody4D? body)
    {
        _trailedBody = body;
        SelectedTrail.Clear();
        if (TrailMode == NBodyTrailMode4D.SelectedBody && body is not null)
        {
            SelectedTrail.Append(body.Position);
        }
    }
}
