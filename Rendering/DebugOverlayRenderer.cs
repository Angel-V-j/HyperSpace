using System;
using System.Globalization;
using System.Text;
using HyperSpace.Diagnostics;
using HyperSpace.Geometry;
using HyperSpace.Physics;
using HyperSpace.Projection;
using HyperSpace.Scene;
using HyperSpace.Transformations;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace HyperSpace.Rendering;

/// <summary>
/// A small text overlay for inspecting the current sandbox state.
/// </summary>
public sealed class DebugOverlayRenderer : IDisposable
{
    private const float PerformanceTextScale = 0.85f;

    private readonly SpriteBatch _spriteBatch;
    private readonly SpriteFont _font;
    private readonly Texture2D _pixel;
    private readonly StringBuilder _text = new(capacity: 1024);
    private readonly StringBuilder _performanceText = new(capacity: 1024);

    private double _sampleTime;
    private int _sampleFrames;
    private double _framesPerSecond;

    public DebugOverlayRenderer(GraphicsDevice graphicsDevice, SpriteFont font)
    {
        _spriteBatch = new SpriteBatch(graphicsDevice);
        _font = font;
        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData([Color.White]);
    }

    public void UpdateTiming(GameTime gameTime)
    {
        _sampleTime += gameTime.ElapsedGameTime.TotalSeconds;
        _sampleFrames++;

        if (_sampleTime >= 0.5)
        {
            _framesPerSecond = _sampleFrames / _sampleTime;
            _sampleTime = 0.0;
            _sampleFrames = 0;
        }
    }

    public void Draw(
        IGeometry4D geometry,
        Transform4D objectTransform,
        Camera4D camera4D,
        PerspectiveProjector4D projector,
        OrbitCamera3D camera3D,
        Wireframe3D wireframe,
        TransformationAnimator4D animator,
        DisplayOptions displayOptions,
        CurvePlayback4D curvePlayback,
        FractalVisualizationSettings fractalVisualization,
        QuaternionJuliaGeneration4D? fractalGeneration,
        PhysicsWorld4D physicsWorld,
        GravityLab4D gravityLab,
        NBodyLab4D nBodyLab,
        bool showPhysicsPlane,
        bool showGravityTrail,
        bool showGravityField,
        bool showNBodyPerformance)
    {
        BuildText(
            geometry,
            objectTransform,
            camera4D,
            projector,
            camera3D,
            wireframe,
            animator,
            displayOptions,
            curvePlayback,
            fractalVisualization,
            fractalGeneration,
            physicsWorld,
            gravityLab,
            nBodyLab,
            showPhysicsPlane,
            showGravityTrail,
            showGravityField);
        if (showNBodyPerformance)
        {
            BuildPerformanceText(physicsWorld, nBodyLab);
        }
        else
        {
            _performanceText.Clear();
        }

        _spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.NonPremultiplied,
            SamplerState.LinearClamp,
            DepthStencilState.None,
            RasterizerState.CullNone);

        var position = new Vector2(14.0f, 31.0f);
        var textSize = _font.MeasureString(_text);
        var panelBounds = new Rectangle(
            8,
            8,
            (int)Math.Ceiling(textSize.X) + 20,
            (int)Math.Ceiling(textSize.Y) + 31);
        _spriteBatch.Draw(_pixel, panelBounds, new Color(9, 14, 27, 218));
        _spriteBatch.Draw(
            _pixel,
            new Rectangle(panelBounds.X, panelBounds.Y, 3, panelBounds.Height),
            VisualizationPalette.ObjectInfoAccent);
        _spriteBatch.DrawString(
            _font,
            "OBJECT INFO / CAMERA",
            new Vector2(14.0f, 11.0f),
            VisualizationPalette.ObjectInfoAccent);
        _spriteBatch.DrawString(_font, _text, position + new Vector2(2.0f), Color.Black * 0.8f);
        _spriteBatch.DrawString(_font, _text, position, new Color(225, 235, 255));

        if (showNBodyPerformance)
        {
            var performanceY = panelBounds.Bottom + 8;
            var performancePosition = new Vector2(14.0f, performanceY + 23.0f);
            var performanceSize = _font.MeasureString(_performanceText) * PerformanceTextScale;
            var performanceBounds = new Rectangle(
                8,
                performanceY,
                (int)Math.Ceiling(performanceSize.X) + 20,
                (int)Math.Ceiling(performanceSize.Y) + 31);
            _spriteBatch.Draw(_pixel, performanceBounds, new Color(9, 14, 27, 228));
            _spriteBatch.Draw(
                _pixel,
                new Rectangle(
                    performanceBounds.X,
                    performanceBounds.Y,
                    3,
                    performanceBounds.Height),
                VisualizationPalette.GravityLabAccent);
            _spriteBatch.DrawString(
                _font,
                "N-BODY PERFORMANCE  current / avg (60 frames)",
                new Vector2(14.0f, performanceY + 3.0f),
                VisualizationPalette.GravityLabAccent);
            _spriteBatch.DrawString(
                _font,
                _performanceText,
                performancePosition + new Vector2(1.5f),
                Color.Black * 0.8f,
                0.0f,
                Vector2.Zero,
                PerformanceTextScale,
                SpriteEffects.None,
                0.0f);
            _spriteBatch.DrawString(
                _font,
                _performanceText,
                performancePosition,
                new Color(225, 235, 255),
                0.0f,
                Vector2.Zero,
                PerformanceTextScale,
                SpriteEffects.None,
                0.0f);
        }
        _spriteBatch.End();
    }

    public void Dispose()
    {
        _pixel.Dispose();
        _spriteBatch.Dispose();
    }

    private void BuildText(
        IGeometry4D geometry,
        Transform4D objectTransform,
        Camera4D camera4D,
        PerspectiveProjector4D projector,
        OrbitCamera3D camera3D,
        Wireframe3D wireframe,
        TransformationAnimator4D animator,
        DisplayOptions displayOptions,
        CurvePlayback4D curvePlayback,
        FractalVisualizationSettings fractalVisualization,
        QuaternionJuliaGeneration4D? fractalGeneration,
        PhysicsWorld4D physicsWorld,
        GravityLab4D gravityLab,
        NBodyLab4D nBodyLab,
        bool showPhysicsPlane,
        bool showGravityTrail,
        bool showGravityField)
    {
        _text.Clear();
        AppendFormat("{0}   FPS {1,5:0.0}\n", geometry.Name, _framesPerSecond);
        AppendFormat("Topology  V {0}  E {1}  F {2}  C {3}   Visible V {4}  E {5}\n",
            geometry.Vertices.Count,
            geometry.Edges.Count,
            geometry.Faces.Count,
            geometry.Cells.Count,
            wireframe.VisibleVertexCount,
            wireframe.VisibleEdgeCount);
        AppendFormat("Sampling  {0}\n",
            geometry.ResolutionDescription);
        if (geometry is Spiral4D spiral)
        {
            var sampleIndex = Math.Clamp(
                curvePlayback.VisibleSampleCount - 1,
                0,
                spiral.Vertices.Count - 1);
            var sample = spiral.Vertices[sampleIndex];
            var xyRadius = Math.Sqrt((sample.X * sample.X) + (sample.Y * sample.Y));
            var zwRadius = Math.Sqrt((sample.Z * sample.Z) + (sample.W * sample.W));
            AppendFormat("Curve  r1 {0:0.00}  r2 {1:0.00}  k {2:0.00}  visible {3}/{4}  {5}\n",
                spiral.Parameters.R1,
                spiral.Parameters.R2,
                spiral.Parameters.K,
                curvePlayback.VisibleSampleCount,
                curvePlayback.TotalSampleCount,
                curvePlayback.IsPlaying ? "PLAY" : "PAUSED");
            AppendFormat("Dual circles at P{0}: XY radius {1:0.000}  ZW radius {2:0.000}\n",
                sampleIndex,
                xyRadius,
                zwRadius);
        }
        if (geometry is QuaternionJuliaSet4D fractal)
        {
            var parameters = fractalGeneration?.Parameters ?? fractal.Parameters;
            AppendFormat(
                "Fractal  points {0:N0}  bounded {1:N0}  escaped {2:N0}\n",
                fractal.Samples.Count,
                fractal.BoundedPointCount,
                fractal.EscapedPointCount);
            AppendFormat(
                "Julia C  ({0:0.000}, {1:0.000}, {2:0.000}, {3:0.000})\n",
                parameters.Constant.A,
                parameters.Constant.B,
                parameters.Constant.C,
                parameters.Constant.D);
            AppendFormat(
                "Resolution {0}^4 = {1:N0}  iterations {2}  escape radius {3:0.00}\n",
                parameters.Resolution,
                parameters.TotalSampleCount,
                parameters.MaxIterations,
                parameters.EscapeRadius);
            if (fractalGeneration is not null)
            {
                AppendFormat(
                    "Generation  {0:0.0}%  {1:N0}/{2:N0}  elapsed {3:0.000}s\n",
                    fractalGeneration.Progress * 100.0,
                    fractalGeneration.ProcessedSampleCount,
                    fractalGeneration.TotalSampleCount,
                    fractalGeneration.Elapsed.TotalSeconds);
            }
            else
            {
                AppendFormat(
                    "Generation  complete in {0:0.000}s\n",
                    fractal.GenerationTime.TotalSeconds);
            }
            AppendFormat(
                "Fractal view  color {0}  point {1}  W slice {2} at {3:0.00}\n",
                fractalVisualization.ColorMode == FractalColorMode.WCoordinate
                    ? "WORLD W"
                    : "ITERATIONS",
                fractalVisualization.PointSize,
                OnOff(fractalVisualization.ShowWSlice),
                fractalVisualization.SliceW);
        }
        AppendPhysics(
            physicsWorld,
            gravityLab,
            nBodyLab,
            showPhysicsPlane,
            showGravityTrail,
            showGravityField);
        AppendFormat("Object4D pos ({0,6:0.00}, {1,6:0.00}, {2,6:0.00}, {3,6:0.00})  scale {4:0.000}\n",
            objectTransform.Position.X,
            objectTransform.Position.Y,
            objectTransform.Position.Z,
            objectTransform.Position.W,
            objectTransform.Scale);
        AppendRotation("Object", objectTransform.Rotation);
        AppendFormat("Camera4D pos ({0,6:0.00}, {1,6:0.00}, {2,6:0.00}, {3,6:0.00})\n",
            camera4D.Position.X,
            camera4D.Position.Y,
            camera4D.Position.Z,
            camera4D.Position.W);
        AppendRotation("Camera", camera4D.Orientation);
        AppendFormat("4D projection  focal {0:0.00}   near W {1:0.00}\n",
            projector.FocalDistance,
            projector.NearPlane);
        AppendFormat("3D view  yaw {0:0.0} deg   pitch {1:0.0} deg   distance {2:0.00}\n",
            Degrees(camera3D.Yaw),
            Degrees(camera3D.Pitch),
            camera3D.Distance);
        if (animator.IsActive)
        {
            var detail = animator.ActiveRotationPlane.HasValue
                ? $"step angle {animator.CurrentRotationDegrees:0.0} / 90 deg"
                : $"progress {animator.Progress * 100.0:0}%";
            AppendFormat("Animation  {0}   {1}\n", animator.ActiveLabel, detail);
        }
        else
        {
            _text.AppendLine("Animation  idle");
        }

        if (geometry.VisualStyle == GeometryVisualStyle4D.Fractal)
        {
            AppendFormat(
                "Layers  Grid {0}  Axes {1}  Points {2}  debug slice {3}\n",
                OnOff(displayOptions.ShowGrid),
                OnOff(displayOptions.ShowAxes),
                OnOff(displayOptions.ShowVertices),
                OnOff(fractalVisualization.ShowWSlice));
        }
        else if (geometry.VisualStyle == GeometryVisualStyle4D.Spiral)
        {
            AppendFormat(
                "Layers  Grid {0}  Axes {1}  Curve {2}  Points {3}  Direction {4}\n",
                OnOff(displayOptions.ShowGrid),
                OnOff(displayOptions.ShowAxes),
                OnOff(displayOptions.ShowEdges),
                OnOff(displayOptions.ShowVertices),
                OnOff(displayOptions.ShowDirection));
        }
        else
        {
            AppendFormat(
                "Layers  Grid {0}  Axes {1}  Surface {2}  Edges {3}  Vertices {4}\n",
                OnOff(displayOptions.ShowGrid),
                OnOff(displayOptions.ShowAxes),
                OnOff(displayOptions.ShowCells),
                OnOff(displayOptions.ShowEdges),
                OnOff(displayOptions.ShowVertices));
        }
    }

    private void AppendRotation(string label, Rotation4D rotation)
    {
        AppendFormat(
            "{0,-6} deg  XY {1,6:0.0}  XZ {2,6:0.0}  XW {3,6:0.0}  YZ {4,6:0.0}  YW {5,6:0.0}  ZW {6,6:0.0}\n",
            label,
            Degrees(rotation.XY),
            Degrees(rotation.XZ),
            Degrees(rotation.XW),
            Degrees(rotation.YZ),
            Degrees(rotation.YW),
            Degrees(rotation.ZW));
    }

    private void BuildPerformanceText(PhysicsWorld4D world, NBodyLab4D nBodyLab)
    {
        _performanceText.Clear();
        var performance = world.Performance;
        var requested = world.RequestedGravityMode == GravityMode4D.Exact
            ? "EXACT"
            : "MEAN FIELD";
        var effective = world.EffectiveGravityMode == GravityMode4D.Exact
            ? "EXACT"
            : "MEAN FIELD";
        _performanceText.AppendFormat(
            CultureInfo.InvariantCulture,
            "Bodies {0:N0}  alive {0:N0}  candidates {1:N0}  merges {2:N0}\n",
            world.Bodies.Count,
            performance.CollisionCandidatesThisFrame,
            performance.MergesThisFrame);
        _performanceText.AppendFormat(
            CultureInfo.InvariantCulture,
            "Gravity requested {0}  effective {1}\n",
            requested,
            effective);
        if (nBodyLab.LastGeneration is { } generation)
        {
            _performanceText.AppendFormat(
                CultureInfo.InvariantCulture,
                "Generation last {0:0.0} ms  rejected {1:N0}\n",
                generation.ElapsedMilliseconds,
                generation.RejectedPositionAttempts);
        }

        AppendPerformanceMetric("Physics total", performance, PerformancePhase.PhysicsTotal);
        AppendPerformanceMetric("  Gravity", performance, PerformancePhase.Gravity);
        AppendPerformanceMetric("  Collision", performance, PerformancePhase.CollisionDetection);
        AppendPerformanceMetric("    Grid", performance, PerformancePhase.CollisionGrid);
        AppendPerformanceMetric("    Candidates", performance, PerformancePhase.CollisionCandidates);
        AppendPerformanceMetric("    Sort", performance, PerformancePhase.CollisionSort);
        AppendPerformanceMetric("    Resolve", performance, PerformancePhase.CollisionResolution);
        AppendPerformanceMetric("  Aggregation", performance, PerformancePhase.Aggregation);
        AppendPerformanceMetric("  Integration", performance, PerformancePhase.Integration);
        AppendPerformanceMetric("  Trails", performance, PerformancePhase.TrailUpdate);
        AppendPerformanceMetric("Prep 4D->3D", performance, PerformancePhase.RenderingPreparation);
        AppendPerformanceMetric("N-body draw CPU", performance, PerformancePhase.NBodyRenderCpu);
        AppendPerformanceMetric("UI update", performance, PerformancePhase.UiUpdate);
        AppendPerformanceMetric("Update total", performance, PerformancePhase.UpdateTotal);
        AppendPerformanceMetric("Render CPU", performance, PerformancePhase.RenderTotal);
        AppendPerformanceMetric("Frame total", performance, PerformancePhase.FrameTotal);

        _performanceText.AppendFormat(
            CultureInfo.InvariantCulture,
            "Wall dt {0:0.00} ms  scheduler dt {1:0.00} ms\n",
            performance.RealElapsedMilliseconds,
            performance.SchedulerElapsedMilliseconds);
        _performanceText.AppendFormat(
            CultureInfo.InvariantCulture,
            "Simulated {0:0.00} ms  accumulator {1:0.00} ms\n",
            performance.SimulatedSecondsThisFrame * 1000.0,
            performance.AccumulatedSimulationMilliseconds);
        if (world.CatchUpLimitedLastUpdate)
        {
            _performanceText.Append("Catch-up CPU budget reached; fixed-step debt retained\n");
        }
        _performanceText.AppendFormat(
            CultureInfo.InvariantCulture,
            "Fixed {0:0.000} ms  steps/frame {1}  real steps/sec {2:0.0}\n",
            performance.FixedTimestepMilliseconds,
            performance.PhysicsStepsThisFrame,
            performance.SimulationStepsPerSecond);
        _performanceText.AppendFormat(
            CultureInfo.InvariantCulture,
            "Scheduler steps/sec {0:0.0}  time x{1:0.##}\n",
            performance.SchedulerStepsPerSecond,
            performance.TimeScale);
        var thread = performance.LastPhysicsThreadId.HasValue
            ? performance.PhysicsRunsOnMainThread ? "MAIN" : "WORKER"
            : "NOT SAMPLED";
        _performanceText.AppendFormat(
            CultureInfo.InvariantCulture,
            "CPU logical {0}  coordinator {1}  parallel workers <= {2}\n",
            performance.LogicalProcessorCount,
            thread,
            performance.ParallelWorkerCountThisFrame);
        _performanceText.Append("GPU instanced 4D projection; draw values are CPU submission time");
    }

    private void AppendPerformanceMetric(
        string label,
        PerformanceProfiler performance,
        PerformancePhase phase)
    {
        var metric = performance.Metric(phase);
        _performanceText.AppendFormat(
            CultureInfo.InvariantCulture,
            "{0,-18} {1,8:0.000} / {2,8:0.000} ms\n",
            label,
            metric.CurrentMilliseconds,
            metric.AverageMilliseconds);
    }

    private void AppendPhysics(
        PhysicsWorld4D world,
        GravityLab4D gravityLab,
        NBodyLab4D nBodyLab,
        bool showPhysicsPlane,
        bool showGravityTrail,
        bool showGravityField)
    {
        var state = !world.IsEnabled
            ? "OFF"
            : world.IsPaused
                ? "PAUSED"
                : "RUNNING";
        AppendFormat(
            "Physics  {0}  bodies {1}  fixed {2:0.0000}s  time x{3:0.##}\n",
            state,
            world.Bodies.Count,
            world.FixedDeltaTime,
            world.TimeScale);
        AppendFormat(
            "Gravity  ({0:0.00}, {1:0.00}, {2:0.00}, {3:0.00})  collisions {4}  e {5:0.0}  plane {6}\n",
            world.Gravity.X,
            world.Gravity.Y,
            world.Gravity.Z,
            world.Gravity.W,
            OnOff(world.CollisionsEnabled),
            world.Restitution,
            OnOff(showPhysicsPlane));
        AppendFormat(
            "Physics steps {0:N0}  hits {1:N0}  kinetic energy {2:0.000}\n",
            world.CompletedStepCount,
            world.CollisionCount,
            world.TotalKineticEnergy);
        AppendFormat(
            "Pair gravity {0}  G {1:0.000}  softening {2:0.000}  trail {3}  field {4}\n",
            OnOff(world.MutualGravityEnabled),
            world.GravitySystem.GravitationalConstant,
            world.GravitySystem.Softening,
            OnOff(showGravityTrail),
            OnOff(showGravityField));

        var diagnostics = gravityLab.Diagnostics;
        if (diagnostics.IsAvailable)
        {
            AppendFormat(
                "Gravity Lab  M {0:0} STATIC  distance {1:0.000}  speed {2:0.000}  W {3:0.000}\n",
                gravityLab.CentralBody!.Mass,
                diagnostics.Distance,
                diagnostics.Speed,
                diagnostics.OrbiterW);
            AppendFormat(
                "Central acceleration {0:0.0000}  toward ({1:0.00}, {2:0.00}, {3:0.00}, {4:0.00})  trail {5}/{6}\n",
                diagnostics.CentralAccelerationMagnitude,
                diagnostics.DirectionTowardCentral.X,
                diagnostics.DirectionTowardCentral.Y,
                diagnostics.DirectionTowardCentral.Z,
                diagnostics.DirectionTowardCentral.W,
                gravityLab.Trail.Points.Count,
                gravityLab.Trail.Capacity);
        }

        if (nBodyLab.HasSystem)
        {
            var momentum = world.TotalMomentum;
            var energy = world.EnergyDiagnostics;
            AppendFormat(
                "N-body  gravity {0}/{1}  aggregation {2} /{3}  merges {4:N0} last {5} ({6:0.0}/s)\n",
                world.RequestedGravityMode,
                world.EffectiveGravityMode,
                OnOff(world.AggregationEnabled),
                world.AggregationCollisionInterval,
                world.AggregationCollisionCount,
                world.LastAggregationCollisionCount,
                world.AggregationCollisionsPerSecond);
            AppendFormat(
                "Mass {0:0.000}  momentum ({1:0.00}, {2:0.00}, {3:0.00}, {4:0.00})\n",
                world.TotalMass,
                momentum.X,
                momentum.Y,
                momentum.Z,
                momentum.W);
            AppendFormat(
                "Energy K {0:0.000}  U {1:0.000}  total {2:0.000}  dE {3:+0.000;-0.000;0.000}%{4}\n",
                energy.KineticEnergy,
                energy.PotentialEnergy,
                energy.TotalEnergy,
                energy.DriftPercent,
                energy.IsConservativeModel ? string.Empty : " approx");
            AppendFormat(
                "Average speed {0:0.000}  max mass {1:0.000}  max |W| {2:0.000}  physics {3:0.0} ms  {4:0.0} step/s\n",
                world.AverageSpeed,
                world.MaximumMass,
                world.MaximumAbsoluteW,
                world.LastPhysicsStepMilliseconds,
                world.SimulationStepsPerSecond);
        }

        if (world.SelectedBody is not { } body)
        {
            return;
        }

        AppendFormat(
            "Body {0}{1} P ({2:0.00}, {3:0.00}, {4:0.00}, {5:0.00})\n",
            body.Id,
            body.IsStatic ? " STATIC" : string.Empty,
            body.Position.X,
            body.Position.Y,
            body.Position.Z,
            body.Position.W);
        AppendFormat(
            "           V ({0:0.00}, {1:0.00}, {2:0.00}, {3:0.00})  A ({4:0.0000}, {5:0.0000}, {6:0.0000}, {7:0.0000})\n",
            body.Velocity.X,
            body.Velocity.Y,
            body.Velocity.Z,
            body.Velocity.W,
            body.Acceleration.X,
            body.Acceleration.Y,
            body.Acceleration.Z,
            body.Acceleration.W);
    }

    private void AppendFormat(string format, params object[] arguments)
    {
        _text.AppendFormat(CultureInfo.InvariantCulture, format, arguments);
    }

    private static double Degrees(double radians) => radians * 180.0 / Math.PI;

    private static string OnOff(bool value) => value ? "ON" : "OFF";
}
