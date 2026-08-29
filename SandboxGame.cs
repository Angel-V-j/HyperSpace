using System;
using HyperSpace.Application;
using HyperSpace.Diagnostics;
using HyperSpace.Geometry;
using HyperSpace.Input;
using HyperSpace.Mathematics;
using HyperSpace.Physics;
using HyperSpace.Projection;
using HyperSpace.Rendering;
using HyperSpace.Scene;
using HyperSpace.Transformations;
using HyperSpace.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace HyperSpace;

public sealed class SandboxGame : Game
{
    private const double PanelTranslationStep = 0.75;
    private const double PanelScaleUpFactor = 1.25;
    private const double PanelScaleDownFactor = 0.8;
    private const double SpiralRadiusStep = 0.10;
    private const double SpiralFrequencyStep = 0.25;
    private const int SpiralSampleStep = 100;
    private const double JuliaConstantStep = 0.05;
    private const int JuliaIterationStep = 4;
    private const double JuliaEscapeRadiusStep = 0.25;
    private const int JuliaResolutionStep = 2;
    private const double FractalSliceStep = 0.25;
    private const int FractalSamplesPerUpdate = 512;
    private readonly GraphicsDeviceManager _graphics;
    private readonly Spiral4DGenerator _spiralGenerator = new();
    private readonly QuaternionJuliaGenerator4D _fractalGenerator = new();
    private readonly FractalVisualizationSettings _fractalVisualization = new();
    private readonly SceneObject4D[] _objects;
    private readonly CurvePlayback4D _curvePlayback;
    private readonly ReferenceGrid4D _referenceGrid = new();
    private readonly Transform4D _referenceGridTransform = new();
    private readonly Camera4D _camera4D = new();
    private readonly PerspectiveProjector4D _projector4D = new();
    private readonly WireframeProjectionPipeline4D _projectionPipeline = new();
    private readonly OrbitCamera3D _camera3D = new();
    private readonly SandboxInputController _input = new();
    private readonly NBodySelectionController _nBodySelection = new();
    private readonly TransformationAnimator4D _transformAnimator = new();
    private readonly PhysicsWorld4D _physicsWorld = new();
    private readonly GravityLab4D _gravityLab;
    private readonly NBodyLab4D _nBodyLab;
    private readonly PhysicsCommandController _physicsCommands;
    private readonly PhysicsProjectionCache4D _physicsProjection;

    private int _selectedObjectIndex;
    private Wireframe3D _objectWireframe3D;
    private Wireframe3D _referenceGridWireframe3D;
    private WireframeRenderer3D? _wireframeRenderer;
    private DebugOverlayRenderer? _debugOverlay;
    private TransformationControlPanel? _controlPanel;
    private TransformationCommand? _activePanelCommand;
    private SpiralParameters _pendingSpiralParameters = SpiralParameters.Default;
    private JuliaParameters _pendingJuliaParameters = JuliaParameters.Default;
    private QuaternionJuliaGeneration4D? _fractalGeneration;

    public SandboxGame()
    {
        _gravityLab = new GravityLab4D(_physicsWorld);
        _nBodyLab = new NBodyLab4D(_physicsWorld);
        _physicsCommands = new PhysicsCommandController(_physicsWorld, _gravityLab, _nBodyLab);
        _physicsProjection = new PhysicsProjectionCache4D(
            _projectionPipeline,
            _camera4D,
            _projector4D);
        var spiral = _spiralGenerator.Generate(_pendingSpiralParameters);
        _objects =
        [
            new(new Tesseract4D()),
            new(new Hypersphere4D()),
            new(new Simplex4D()),
            new(new IrregularPolytope4D()),
            new(
                spiral,
                new DisplayOptions(
                    showCells: false,
                    showEdges: true,
                    showVertices: false,
                    showDirection: true)),
            new(
                QuaternionJuliaSet4D.Empty(_pendingJuliaParameters),
                new DisplayOptions(
                    showCells: false,
                    showEdges: false,
                    showVertices: true))
        ];
        _curvePlayback = new CurvePlayback4D(spiral.Vertices.Count);

        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 1280,
            PreferredBackBufferHeight = 900,
            SynchronizeWithVerticalRetrace = true
        };

        Content.RootDirectory = "Content";
        IsFixedTimeStep = true;
        TargetElapsedTime = TimeSpan.FromSeconds(PhysicsWorld4D.DefaultFixedDeltaTime);
        IsMouseVisible = true;
        Window.AllowUserResizing = true;
        Window.Title = "HyperSpace - 4D Geometry Explorer";

        _objectWireframe3D = _projectionPipeline.Project(
            SelectedObject.Geometry,
            SelectedObject.Transform,
            _camera4D,
            _projector4D);
        _referenceGridWireframe3D = _projectionPipeline.Project(
            _referenceGrid.Vertices,
            _referenceGrid.Edges,
            _referenceGridTransform,
            _camera4D,
            _projector4D);
    }

    protected override void LoadContent()
    {
        var debugFont = Content.Load<SpriteFont>("DebugFont");
        _wireframeRenderer = new WireframeRenderer3D(GraphicsDevice);
        _debugOverlay = new DebugOverlayRenderer(GraphicsDevice, debugFont);
        _controlPanel = new TransformationControlPanel(GraphicsDevice, debugFont);
    }

    protected override void Update(GameTime gameTime)
    {
        var performance = _physicsWorld.Performance;
        performance.BeginFrame(
            gameTime.ElapsedGameTime.TotalSeconds,
            _physicsWorld.FixedDeltaTime,
            _physicsWorld.TimeScale);
        var updateStartedAt = performance.BeginPhase();
        var keyboard = Keyboard.GetState();
        var mouse = Mouse.GetState();
        var gamePad = GamePad.GetState(PlayerIndex.One);
        var viewport = GraphicsDevice.Viewport;
        var uiUpdateStartedAt = performance.BeginPhase();
        var panelCommand = _controlPanel?.Update(
            mouse,
            keyboard,
            IsActive,
            viewport.Width,
            viewport.Height,
            _transformAnimator.IsActive,
            SelectedObject.Geometry,
            IsFractalGenerationActive,
            _physicsWorld,
            _nBodyLab);
        performance.EndPhase(PerformancePhase.UiUpdate, uiUpdateStartedAt);
        var pointerOverPanel = _controlPanel?.Contains(mouse.Position) ?? false;

        if (_controlPanel?.IsGravityLabView == true && !_gravityLab.HasExperiment)
        {
            _gravityLab.ResetExperiment();
        }


        if (_controlPanel?.IsNBodyLabView == true && !_nBodyLab.HasSystem)
        {
            _nBodyLab.GenerateSystem();
        }

        _input.Update(
            gameTime,
            IsActive,
            keyboard,
            mouse,
            gamePad,
            SelectedObject.Transform,
            _camera4D,
            _projector4D,
            _camera3D,
            allowMouseInput: !pointerOverPanel);

        if (_input.ExitRequested)
        {
            Exit();
        }

        if (_input.ResetRequested)
        {
            _transformAnimator.Cancel();
            _activePanelCommand = null;
        }

        if (panelCommand.HasValue)
        {
            HandlePanelCommand(panelCommand.Value);
        }

        _transformAnimator.Update(
            gameTime.ElapsedGameTime.TotalSeconds,
            SelectedObject.Transform);
        if (IsSpiralSelected)
        {
            _curvePlayback.Update(gameTime.ElapsedGameTime.TotalSeconds);
        }
        AdvanceFractalGeneration();
        _physicsWorld.Update(gameTime.ElapsedGameTime.TotalSeconds);

        if (!_transformAnimator.IsActive)
        {
            _activePanelCommand = null;
        }

        _controlPanel?.SetActiveState(
            _activePanelCommand,
            SelectedObject.DisplayOptions,
            SelectedObject.Geometry.VisualStyle,
            _curvePlayback,
            _fractalVisualization,
            IsFractalGenerationActive,
            _physicsWorld,
            _nBodyLab,
            _physicsCommands.ShowPhysicsPlane,
            _physicsCommands.ShowGravityTrail,
            _physicsCommands.ShowGravityField);

        var isNBodyView = _controlPanel?.IsNBodyLabView == true;
        var renderPreparationStartedAt = isNBodyView
            ? performance.BeginPhase()
            : 0L;
        _objectWireframe3D = _projectionPipeline.Project(
            SelectedObject.Geometry,
            SelectedObject.Transform,
            _camera4D,
            _projector4D);
        _referenceGridWireframe3D = _projectionPipeline.Project(
            _referenceGrid.Vertices,
            _referenceGrid.Edges,
            _referenceGridTransform,
            _camera4D,
            _projector4D);
        _physicsProjection.Update(_physicsWorld, _gravityLab, _nBodyLab);
        var selectedBody = _nBodySelection.Update(
            mouse,
            IsActive,
            isNBodyView,
            pointerOverPanel,
            CreateSceneViewport(GraphicsDevice.Viewport),
            _physicsProjection.Particles,
            _physicsWorld.Bodies,
            _camera3D,
            _nBodyLab.Settings.PointScale);
        if (selectedBody is not null)
        {
            _nBodyLab.SelectBody(selectedBody);
        }
        if (isNBodyView)
        {
            performance.EndPhase(
                PerformancePhase.RenderingPreparation,
                renderPreparationStartedAt);
        }

        _debugOverlay?.UpdateTiming(gameTime);
        base.Update(gameTime);
        performance.EndPhase(PerformancePhase.UpdateTotal, updateStartedAt);
    }

    protected override void Draw(GameTime gameTime)
    {
        var performance = _physicsWorld.Performance;
        var renderStartedAt = performance.BeginPhase();
        GraphicsDevice.Clear(new Color(8, 11, 22));

        var fullViewport = GraphicsDevice.Viewport;
        var sceneViewport = CreateSceneViewport(fullViewport);
        GraphicsDevice.Viewport = sceneViewport;
        var visibleVertexLimit = IsSpiralSelected
            ? _curvePlayback.VisibleSampleCount
            : int.MaxValue;
        var isNBodyView = _controlPanel?.IsNBodyLabView == true;

        _wireframeRenderer?.DrawReferenceGrid(
            GraphicsDevice,
            _referenceGridWireframe3D,
            _camera3D,
            SelectedObject.DisplayOptions.ShowGrid,
            SelectedObject.DisplayOptions.ShowAxes);

        if (_physicsCommands.ShowPhysicsPlane)
        {
            _wireframeRenderer?.DrawPhysicsHyperplane(
                GraphicsDevice,
                _physicsProjection.Hyperplane,
                _camera3D);
        }

        if (!isNBodyView && SelectedObject.DisplayOptions.ShowCells)
        {
            _wireframeRenderer?.DrawSurfaces(
                GraphicsDevice,
                _objectWireframe3D,
                SelectedObject.Geometry,
                _camera3D);
        }

        if (!isNBodyView && SelectedObject.DisplayOptions.ShowEdges)
        {
            _wireframeRenderer?.Draw(
                GraphicsDevice,
                _objectWireframe3D,
                SelectedObject.Geometry,
                _camera3D,
                visibleVertexLimit);
        }

        if (!isNBodyView && IsFractalSelected &&
            SelectedObject.DisplayOptions.ShowVertices &&
            SelectedObject.Geometry is QuaternionJuliaSet4D fractal)
        {
            _wireframeRenderer?.DrawFractalPoints(
                GraphicsDevice,
                _objectWireframe3D,
                fractal,
                _camera3D,
                _fractalVisualization);
        }
        else if (!isNBodyView && SelectedObject.DisplayOptions.ShowVertices)
        {
            _wireframeRenderer?.DrawVertices(
                GraphicsDevice,
                _objectWireframe3D,
                SelectedObject.Geometry,
                _camera3D,
                visibleVertexLimit);
        }

        if (!isNBodyView && IsSpiralSelected && SelectedObject.DisplayOptions.ShowDirection)
        {
            _wireframeRenderer?.DrawCurveDirectionMarkers(
                GraphicsDevice,
                _objectWireframe3D,
                _camera3D,
                visibleVertexLimit);
        }

        if (_controlPanel?.IsGravityLabView == true && _physicsCommands.ShowGravityTrail)
        {
            _wireframeRenderer?.DrawGravityTrail(
                GraphicsDevice,
                _physicsProjection.GravityTrail,
                _camera3D);
        }

        var nBodyRenderStartedAt = isNBodyView
            ? performance.BeginPhase()
            : 0L;
        if (_controlPanel?.IsNBodyLabView == true &&
            _nBodyLab.TrailMode == NBodyTrailMode4D.SelectedBody)
        {
            _wireframeRenderer?.DrawGravityTrail(
                GraphicsDevice,
                _physicsProjection.NBodyTrail,
                _camera3D);
        }

        if (_controlPanel?.IsGravityLabView == true && _physicsCommands.ShowGravityField)
        {
            _wireframeRenderer?.DrawGravityFieldLink(
                GraphicsDevice,
                _physicsProjection.GravityField,
                _camera3D);
        }

        _wireframeRenderer?.DrawPhysicsParticles(
            GraphicsDevice,
            _physicsProjection.Particles,
            _physicsWorld.Bodies,
            _physicsWorld.SelectedBody,
            _gravityLab.CentralBody,
            _gravityLab.Orbiter,
            _camera3D,
            nBodyMode: _controlPanel?.IsNBodyLabView == true,
            nBodyColorMode: _nBodyLab.ColorMode,
            pointScale: _nBodyLab.Settings.PointScale);
        if (isNBodyView)
        {
            performance.EndPhase(
                PerformancePhase.NBodyRenderCpu,
                nBodyRenderStartedAt);
        }

        _debugOverlay?.Draw(
            SelectedObject.Geometry,
            SelectedObject.Transform,
            _camera4D,
            _projector4D,
            _camera3D,
            _objectWireframe3D,
            _transformAnimator,
            SelectedObject.DisplayOptions,
            _curvePlayback,
            _fractalVisualization,
            _fractalGeneration,
            _physicsWorld,
            _gravityLab,
            _nBodyLab,
            _physicsCommands.ShowPhysicsPlane,
            _physicsCommands.ShowGravityTrail,
            _physicsCommands.ShowGravityField,
            showNBodyPerformance: isNBodyView);

        GraphicsDevice.Viewport = fullViewport;
        _controlPanel?.Draw(
            fullViewport.Width,
            fullViewport.Height,
            _transformAnimator,
            SelectedObject.DisplayOptions,
            SelectedObject.Geometry,
            _pendingSpiralParameters,
            _curvePlayback,
            _pendingJuliaParameters,
            _fractalVisualization,
            _fractalGeneration,
            _physicsWorld,
            _gravityLab,
            _nBodyLab,
            _physicsCommands.PendingParticleVelocity,
            _physicsCommands.ShowPhysicsPlane,
            _physicsCommands.ShowGravityTrail,
            _physicsCommands.ShowGravityField);

        base.Draw(gameTime);
        performance.EndPhase(PerformancePhase.RenderTotal, renderStartedAt);
        performance.CompleteFrame(
            _physicsWorld.AccumulatedSimulationTime,
            _physicsWorld.SimulationStepsPerSecond);
    }

    protected override void UnloadContent()
    {
        _nBodyLab.Dispose();
        _gravityLab.Dispose();
        _controlPanel?.Dispose();
        _debugOverlay?.Dispose();
        _wireframeRenderer?.Dispose();
        base.UnloadContent();
    }

    private void HandlePanelCommand(TransformationCommand command)
    {
        if (TrySelectObject(command))
        {
            return;
        }

        if (TryHandleSpiralCommand(command))
        {
            return;
        }

        if (TryHandleFractalCommand(command))
        {
            return;
        }

        if (_physicsCommands.TryHandle(
            command,
            _controlPanel?.IsNBodyLabView == true))
        {
            return;
        }

        if (TryHandleDisplayCommand(command))
        {
            return;
        }

        if (command == TransformationCommand.ResetObject)
        {
            _transformAnimator.Cancel();
            SelectedObject.Transform.Reset();
            _activePanelCommand = null;
            return;
        }

        if (command == TransformationCommand.ResetCamera)
        {
            _transformAnimator.Cancel();
            _camera4D.Reset();
            _projector4D.Reset();
            _camera3D.Reset();
            _activePanelCommand = null;
            return;
        }

        var started = command switch
        {
            TransformationCommand.RotateXY =>
                _transformAnimator.TryStartRotation(RotationPlane4D.XY),
            TransformationCommand.RotateXZ =>
                _transformAnimator.TryStartRotation(RotationPlane4D.XZ),
            TransformationCommand.RotateXW =>
                _transformAnimator.TryStartRotation(RotationPlane4D.XW),
            TransformationCommand.RotateYZ =>
                _transformAnimator.TryStartRotation(RotationPlane4D.YZ),
            TransformationCommand.RotateYW =>
                _transformAnimator.TryStartRotation(RotationPlane4D.YW),
            TransformationCommand.RotateZW =>
                _transformAnimator.TryStartRotation(RotationPlane4D.ZW),
            TransformationCommand.ScaleUp =>
                _transformAnimator.TryStartUniformScale(PanelScaleUpFactor),
            TransformationCommand.ScaleDown =>
                _transformAnimator.TryStartUniformScale(PanelScaleDownFactor),
            TransformationCommand.MovePositiveX =>
                _transformAnimator.TryStartTranslation(new Vector4D(PanelTranslationStep, 0, 0, 0)),
            TransformationCommand.MoveNegativeX =>
                _transformAnimator.TryStartTranslation(new Vector4D(-PanelTranslationStep, 0, 0, 0)),
            TransformationCommand.MovePositiveY =>
                _transformAnimator.TryStartTranslation(new Vector4D(0, PanelTranslationStep, 0, 0)),
            TransformationCommand.MoveNegativeY =>
                _transformAnimator.TryStartTranslation(new Vector4D(0, -PanelTranslationStep, 0, 0)),
            TransformationCommand.MovePositiveZ =>
                _transformAnimator.TryStartTranslation(new Vector4D(0, 0, PanelTranslationStep, 0)),
            TransformationCommand.MoveNegativeZ =>
                _transformAnimator.TryStartTranslation(new Vector4D(0, 0, -PanelTranslationStep, 0)),
            TransformationCommand.MovePositiveW =>
                _transformAnimator.TryStartTranslation(new Vector4D(0, 0, 0, PanelTranslationStep)),
            TransformationCommand.MoveNegativeW =>
                _transformAnimator.TryStartTranslation(new Vector4D(0, 0, 0, -PanelTranslationStep)),
            _ => false
        };

        if (started)
        {
            _activePanelCommand = command;
        }
    }

    private bool TryHandleDisplayCommand(TransformationCommand command)
    {
        var displayOptions = SelectedObject.DisplayOptions;
        switch (command)
        {
            case TransformationCommand.ToggleGrid:
                displayOptions.ToggleGrid();
                return true;
            case TransformationCommand.ToggleAxes:
                displayOptions.ToggleAxes();
                return true;
            case TransformationCommand.ToggleCells:
                displayOptions.ToggleCells();
                return true;
            case TransformationCommand.ToggleEdges:
                displayOptions.ToggleEdges();
                return true;
            case TransformationCommand.ToggleVertices:
                displayOptions.ToggleVertices();
                return true;
            case TransformationCommand.ToggleCurve:
                displayOptions.ToggleEdges();
                return true;
            case TransformationCommand.ToggleCurvePoints:
                displayOptions.ToggleVertices();
                return true;
            case TransformationCommand.ToggleCurveDirection:
                displayOptions.ToggleDirection();
                return true;
            default:
                return false;
        }
    }

    private SceneObject4D SelectedObject => _objects[_selectedObjectIndex];

    private SceneObject4D SpiralObject => _objects[4];

    private SceneObject4D FractalObject => _objects[5];

    private bool IsSpiralSelected =>
        SelectedObject.Geometry.VisualStyle == GeometryVisualStyle4D.Spiral;

    private bool IsFractalSelected =>
        SelectedObject.Geometry.VisualStyle == GeometryVisualStyle4D.Fractal;

    private bool IsFractalGenerationActive =>
        _fractalGeneration is { IsCompleted: false, IsCancelled: false };

    private bool TrySelectObject(TransformationCommand command)
    {
        var selectedIndex = command switch
        {
            TransformationCommand.SelectTesseract => 0,
            TransformationCommand.SelectHypersphere => 1,
            TransformationCommand.SelectSimplex => 2,
            TransformationCommand.SelectIrregular => 3,
            TransformationCommand.SelectSpiral => 4,
            TransformationCommand.SelectFractal => 5,
            _ => -1
        };

        if (selectedIndex < 0)
        {
            return false;
        }

        _transformAnimator.Cancel();
        _activePanelCommand = null;
        _selectedObjectIndex = selectedIndex;
        if (IsFractalSelected &&
            FractalObject.Geometry.Vertices.Count == 0 &&
            !IsFractalGenerationActive)
        {
            StartFractalGeneration();
        }
        Window.Title = $"HyperSpace - {SelectedObject.Geometry.Name}";
        return true;
    }

    private bool TryHandleSpiralCommand(TransformationCommand command)
    {
        switch (command)
        {
            case TransformationCommand.DecreaseSpiralR1:
                _pendingSpiralParameters = _pendingSpiralParameters with
                {
                    R1 = Math.Clamp(_pendingSpiralParameters.R1 - SpiralRadiusStep, 0.10, 3.0)
                };
                return true;
            case TransformationCommand.IncreaseSpiralR1:
                _pendingSpiralParameters = _pendingSpiralParameters with
                {
                    R1 = Math.Clamp(_pendingSpiralParameters.R1 + SpiralRadiusStep, 0.10, 3.0)
                };
                return true;
            case TransformationCommand.DecreaseSpiralR2:
                _pendingSpiralParameters = _pendingSpiralParameters with
                {
                    R2 = Math.Clamp(_pendingSpiralParameters.R2 - SpiralRadiusStep, 0.10, 3.0)
                };
                return true;
            case TransformationCommand.IncreaseSpiralR2:
                _pendingSpiralParameters = _pendingSpiralParameters with
                {
                    R2 = Math.Clamp(_pendingSpiralParameters.R2 + SpiralRadiusStep, 0.10, 3.0)
                };
                return true;
            case TransformationCommand.DecreaseSpiralK:
                _pendingSpiralParameters = _pendingSpiralParameters with
                {
                    K = Math.Clamp(_pendingSpiralParameters.K - SpiralFrequencyStep, 0.25, 32.0)
                };
                return true;
            case TransformationCommand.IncreaseSpiralK:
                _pendingSpiralParameters = _pendingSpiralParameters with
                {
                    K = Math.Clamp(_pendingSpiralParameters.K + SpiralFrequencyStep, 0.25, 32.0)
                };
                return true;
            case TransformationCommand.DecreaseSpiralSamples:
                _pendingSpiralParameters = _pendingSpiralParameters with
                {
                    SampleCount = Math.Clamp(
                        _pendingSpiralParameters.SampleCount - SpiralSampleStep,
                        100,
                        1200)
                };
                return true;
            case TransformationCommand.IncreaseSpiralSamples:
                _pendingSpiralParameters = _pendingSpiralParameters with
                {
                    SampleCount = Math.Clamp(
                        _pendingSpiralParameters.SampleCount + SpiralSampleStep,
                        100,
                        1200)
                };
                return true;
            case TransformationCommand.RegenerateSpiral:
                var spiral = _spiralGenerator.Generate(_pendingSpiralParameters);
                SpiralObject.ReplaceGeometry(spiral);
                _curvePlayback.SetTotalSampleCount(spiral.Vertices.Count, showComplete: true);
                return true;
            case TransformationCommand.PlayCurve:
                _curvePlayback.Play();
                return true;
            case TransformationCommand.ResetCurve:
                _curvePlayback.Reset();
                return true;
            default:
                return false;
        }
    }

    private bool TryHandleFractalCommand(TransformationCommand command)
    {
        var constant = _pendingJuliaParameters.Constant;
        switch (command)
        {
            case TransformationCommand.DecreaseJuliaA:
                SetJuliaConstant(constant with { A = ClampJuliaConstant(constant.A - JuliaConstantStep) });
                return true;
            case TransformationCommand.IncreaseJuliaA:
                SetJuliaConstant(constant with { A = ClampJuliaConstant(constant.A + JuliaConstantStep) });
                return true;
            case TransformationCommand.DecreaseJuliaB:
                SetJuliaConstant(constant with { B = ClampJuliaConstant(constant.B - JuliaConstantStep) });
                return true;
            case TransformationCommand.IncreaseJuliaB:
                SetJuliaConstant(constant with { B = ClampJuliaConstant(constant.B + JuliaConstantStep) });
                return true;
            case TransformationCommand.DecreaseJuliaC:
                SetJuliaConstant(constant with { C = ClampJuliaConstant(constant.C - JuliaConstantStep) });
                return true;
            case TransformationCommand.IncreaseJuliaC:
                SetJuliaConstant(constant with { C = ClampJuliaConstant(constant.C + JuliaConstantStep) });
                return true;
            case TransformationCommand.DecreaseJuliaD:
                SetJuliaConstant(constant with { D = ClampJuliaConstant(constant.D - JuliaConstantStep) });
                return true;
            case TransformationCommand.IncreaseJuliaD:
                SetJuliaConstant(constant with { D = ClampJuliaConstant(constant.D + JuliaConstantStep) });
                return true;
            case TransformationCommand.DecreaseJuliaIterations:
                _pendingJuliaParameters = _pendingJuliaParameters with
                {
                    MaxIterations = Math.Clamp(
                        _pendingJuliaParameters.MaxIterations - JuliaIterationStep,
                        4,
                        128)
                };
                return true;
            case TransformationCommand.IncreaseJuliaIterations:
                _pendingJuliaParameters = _pendingJuliaParameters with
                {
                    MaxIterations = Math.Clamp(
                        _pendingJuliaParameters.MaxIterations + JuliaIterationStep,
                        4,
                        128)
                };
                return true;
            case TransformationCommand.DecreaseJuliaEscapeRadius:
                _pendingJuliaParameters = _pendingJuliaParameters with
                {
                    EscapeRadius = Math.Clamp(
                        _pendingJuliaParameters.EscapeRadius - JuliaEscapeRadiusStep,
                        1.0,
                        8.0)
                };
                return true;
            case TransformationCommand.IncreaseJuliaEscapeRadius:
                _pendingJuliaParameters = _pendingJuliaParameters with
                {
                    EscapeRadius = Math.Clamp(
                        _pendingJuliaParameters.EscapeRadius + JuliaEscapeRadiusStep,
                        1.0,
                        8.0)
                };
                return true;
            case TransformationCommand.DecreaseJuliaResolution:
                _pendingJuliaParameters = _pendingJuliaParameters with
                {
                    Resolution = Math.Clamp(
                        _pendingJuliaParameters.Resolution - JuliaResolutionStep,
                        6,
                        20)
                };
                return true;
            case TransformationCommand.IncreaseJuliaResolution:
                _pendingJuliaParameters = _pendingJuliaParameters with
                {
                    Resolution = Math.Clamp(
                        _pendingJuliaParameters.Resolution + JuliaResolutionStep,
                        6,
                        20)
                };
                return true;
            case TransformationCommand.SelectJuliaPreset1:
                SetJuliaConstant(JuliaParameters.Preset1);
                return true;
            case TransformationCommand.SelectJuliaPreset2:
                SetJuliaConstant(JuliaParameters.Preset2);
                return true;
            case TransformationCommand.SelectJuliaPreset3:
                SetJuliaConstant(JuliaParameters.Preset3);
                return true;
            case TransformationCommand.GenerateFractal:
                StartFractalGeneration();
                return true;
            case TransformationCommand.CancelFractalGeneration:
                _fractalGeneration?.Cancel();
                _fractalGeneration = null;
                return true;
            case TransformationCommand.ResetFractal:
                _pendingJuliaParameters = JuliaParameters.Default;
                _fractalVisualization.Reset();
                StartFractalGeneration();
                return true;
            case TransformationCommand.ColorFractalByW:
                _fractalVisualization.SetColorMode(FractalColorMode.WCoordinate);
                return true;
            case TransformationCommand.ColorFractalByIterations:
                _fractalVisualization.SetColorMode(FractalColorMode.EscapeIterations);
                return true;
            case TransformationCommand.ToggleFractalWSlice:
                _fractalVisualization.ToggleWSlice();
                return true;
            case TransformationCommand.DecreaseFractalSliceW:
                _fractalVisualization.AdjustSliceW(
                    -FractalSliceStep,
                    _pendingJuliaParameters.MinimumCoordinate,
                    _pendingJuliaParameters.MaximumCoordinate);
                return true;
            case TransformationCommand.IncreaseFractalSliceW:
                _fractalVisualization.AdjustSliceW(
                    FractalSliceStep,
                    _pendingJuliaParameters.MinimumCoordinate,
                    _pendingJuliaParameters.MaximumCoordinate);
                return true;
            case TransformationCommand.CycleFractalPointSize:
                _fractalVisualization.CyclePointSize();
                return true;
            default:
                return false;
        }
    }

    private void StartFractalGeneration()
    {
        _fractalGeneration?.Cancel();
        _fractalGeneration = _fractalGenerator.Start(_pendingJuliaParameters);
    }

    private void AdvanceFractalGeneration()
    {
        if (!IsFractalGenerationActive)
        {
            return;
        }

        _fractalGeneration!.ProcessBatch(FractalSamplesPerUpdate);
        if (_fractalGeneration.IsCompleted)
        {
            FractalObject.ReplaceGeometry(_fractalGeneration.CreateResult());
            _fractalGeneration = null;
        }
    }

    private void SetJuliaConstant(Quaternion4D constant) =>
        _pendingJuliaParameters = _pendingJuliaParameters with { Constant = constant };

    private static double ClampJuliaConstant(double value) => Math.Clamp(value, -1.5, 1.5);

    private static Viewport CreateSceneViewport(Viewport fullViewport)
    {
        var panelWidth = Math.Min(
            TransformationControlPanel.PreferredWidth,
            fullViewport.Width);
        return new Viewport(
            0,
            0,
            Math.Max(1, fullViewport.Width - panelWidth),
            fullViewport.Height);
    }
}
