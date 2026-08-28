using System;
using HyperSpace.Geometry;
using HyperSpace.Input;
using HyperSpace.Mathematics;
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

    private readonly GraphicsDeviceManager _graphics;
    private readonly Spiral4DGenerator _spiralGenerator = new();
    private readonly SceneObject4D[] _objects;
    private readonly CurvePlayback4D _curvePlayback;
    private readonly ReferenceGrid4D _referenceGrid = new();
    private readonly Transform4D _referenceGridTransform = new();
    private readonly Camera4D _camera4D = new();
    private readonly PerspectiveProjector4D _projector4D = new();
    private readonly WireframeProjectionPipeline4D _projectionPipeline = new();
    private readonly OrbitCamera3D _camera3D = new();
    private readonly SandboxInputController _input = new();
    private readonly TransformationAnimator4D _transformAnimator = new();

    private int _selectedObjectIndex;
    private Wireframe3D _objectWireframe3D;
    private Wireframe3D _referenceGridWireframe3D;
    private WireframeRenderer3D? _wireframeRenderer;
    private DebugOverlayRenderer? _debugOverlay;
    private TransformationControlPanel? _controlPanel;
    private TransformationCommand? _activePanelCommand;
    private SpiralParameters _pendingSpiralParameters = SpiralParameters.Default;

    public SandboxGame()
    {
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
                    showDirection: true))
        ];
        _curvePlayback = new CurvePlayback4D(spiral.Vertices.Count);

        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 1280,
            PreferredBackBufferHeight = 900,
            SynchronizeWithVerticalRetrace = true
        };

        Content.RootDirectory = "Content";
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
        var keyboard = Keyboard.GetState();
        var mouse = Mouse.GetState();
        var gamePad = GamePad.GetState(PlayerIndex.One);
        var viewport = GraphicsDevice.Viewport;
        var panelCommand = _controlPanel?.Update(
            mouse,
            IsActive,
            viewport.Width,
            viewport.Height,
            _transformAnimator.IsActive,
            SelectedObject.Geometry);
        var pointerOverPanel = _controlPanel?.Contains(mouse.Position) ?? false;

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

        if (!_transformAnimator.IsActive)
        {
            _activePanelCommand = null;
        }

        _controlPanel?.SetActiveState(
            _activePanelCommand,
            SelectedObject.DisplayOptions,
            SelectedObject.Geometry.VisualStyle,
            _curvePlayback);

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

        _debugOverlay?.UpdateTiming(gameTime);
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(8, 11, 22));

        var fullViewport = GraphicsDevice.Viewport;
        var panelWidth = Math.Min(
            TransformationControlPanel.PreferredWidth,
            fullViewport.Width);
        var sceneViewport = new Viewport(
            0,
            0,
            Math.Max(1, fullViewport.Width - panelWidth),
            fullViewport.Height);
        GraphicsDevice.Viewport = sceneViewport;
        var visibleVertexLimit = IsSpiralSelected
            ? _curvePlayback.VisibleSampleCount
            : int.MaxValue;

        _wireframeRenderer?.DrawReferenceGrid(
            GraphicsDevice,
            _referenceGridWireframe3D,
            _camera3D,
            SelectedObject.DisplayOptions.ShowGrid,
            SelectedObject.DisplayOptions.ShowAxes);

        if (SelectedObject.DisplayOptions.ShowCells)
        {
            _wireframeRenderer?.DrawSurfaces(
                GraphicsDevice,
                _objectWireframe3D,
                SelectedObject.Geometry,
                _camera3D);
        }

        if (SelectedObject.DisplayOptions.ShowEdges)
        {
            _wireframeRenderer?.Draw(
                GraphicsDevice,
                _objectWireframe3D,
                SelectedObject.Geometry,
                _camera3D,
                visibleVertexLimit);
        }

        if (SelectedObject.DisplayOptions.ShowVertices)
        {
            _wireframeRenderer?.DrawVertices(
                GraphicsDevice,
                _objectWireframe3D,
                SelectedObject.Geometry,
                _camera3D,
                visibleVertexLimit);
        }

        if (IsSpiralSelected && SelectedObject.DisplayOptions.ShowDirection)
        {
            _wireframeRenderer?.DrawCurveDirectionMarkers(
                GraphicsDevice,
                _objectWireframe3D,
                _camera3D,
                visibleVertexLimit);
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
            _curvePlayback);

        GraphicsDevice.Viewport = fullViewport;
        _controlPanel?.Draw(
            fullViewport.Width,
            fullViewport.Height,
            _transformAnimator,
            SelectedObject.DisplayOptions,
            SelectedObject.Geometry,
            _pendingSpiralParameters,
            _curvePlayback);

        base.Draw(gameTime);
    }

    protected override void UnloadContent()
    {
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

    private bool IsSpiralSelected =>
        SelectedObject.Geometry.VisualStyle == GeometryVisualStyle4D.Spiral;

    private bool TrySelectObject(TransformationCommand command)
    {
        var selectedIndex = command switch
        {
            TransformationCommand.SelectTesseract => 0,
            TransformationCommand.SelectHypersphere => 1,
            TransformationCommand.SelectSimplex => 2,
            TransformationCommand.SelectIrregular => 3,
            TransformationCommand.SelectSpiral => 4,
            _ => -1
        };

        if (selectedIndex < 0)
        {
            return false;
        }

        _transformAnimator.Cancel();
        _activePanelCommand = null;
        _selectedObjectIndex = selectedIndex;
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
}
