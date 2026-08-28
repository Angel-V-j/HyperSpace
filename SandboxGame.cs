using System;
using HyperSpace.Geometry;
using HyperSpace.Input;
using HyperSpace.Mathematics;
using HyperSpace.Projection;
using HyperSpace.Rendering;
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

    private readonly GraphicsDeviceManager _graphics;
    private readonly Tesseract4D _tesseract = new();
    private readonly ReferenceGrid4D _referenceGrid = new();
    private readonly Transform4D _tesseractTransform = new();
    private readonly Transform4D _referenceGridTransform = new();
    private readonly Camera4D _camera4D = new();
    private readonly PerspectiveProjector4D _projector4D = new();
    private readonly WireframeProjectionPipeline4D _projectionPipeline = new();
    private readonly OrbitCamera3D _camera3D = new();
    private readonly SandboxInputController _input = new();
    private readonly TransformationAnimator4D _transformAnimator = new();

    private Wireframe3D _tesseractWireframe3D;
    private Wireframe3D _referenceGridWireframe3D;
    private WireframeRenderer3D? _wireframeRenderer;
    private DebugOverlayRenderer? _debugOverlay;
    private TransformationControlPanel? _controlPanel;
    private TransformationCommand? _activePanelCommand;

    public SandboxGame()
    {
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 1000,
            PreferredBackBufferHeight = 480,
            SynchronizeWithVerticalRetrace = true
        };

        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        Window.AllowUserResizing = true;
        Window.Title = "HyperSpace - Interactive 4D Tesseract";

        _tesseractWireframe3D = _projectionPipeline.Project(
            _tesseract,
            _tesseractTransform,
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
            _transformAnimator.IsActive);
        var pointerOverPanel = _controlPanel?.Contains(mouse.Position) ?? false;

        _input.Update(
            gameTime,
            IsActive,
            keyboard,
            mouse,
            gamePad,
            _tesseractTransform,
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
            _tesseractTransform);

        if (!_transformAnimator.IsActive)
        {
            _activePanelCommand = null;
        }

        _controlPanel?.SetActiveCommand(_activePanelCommand);

        _tesseractWireframe3D = _projectionPipeline.Project(
            _tesseract,
            _tesseractTransform,
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

        _wireframeRenderer?.DrawReferenceGrid(
            GraphicsDevice,
            _referenceGridWireframe3D,
            _camera3D);
        _wireframeRenderer?.Draw(GraphicsDevice, _tesseractWireframe3D, _camera3D);
        _debugOverlay?.Draw(
            _tesseractTransform,
            _camera4D,
            _projector4D,
            _camera3D,
            _tesseractWireframe3D,
            _transformAnimator);

        GraphicsDevice.Viewport = fullViewport;
        _controlPanel?.Draw(
            fullViewport.Width,
            fullViewport.Height,
            _transformAnimator);

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
        if (command == TransformationCommand.ResetObject)
        {
            _transformAnimator.Cancel();
            _tesseractTransform.Reset();
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
}
