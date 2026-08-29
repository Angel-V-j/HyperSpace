using System;
using System.Collections.Generic;
using System.Linq;
using HyperSpace.Geometry;
using HyperSpace.Mathematics;
using HyperSpace.Physics;
using HyperSpace.Rendering;
using HyperSpace.Scene;
using HyperSpace.Transformations;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace HyperSpace.UI;

/// <summary>
/// A deliberately small MonoGame control panel; it is not a general UI framework.
/// </summary>
public sealed class TransformationControlPanel : IDisposable
{
    public const int PreferredWidth = 340;

    private const int Padding = 10;
    private const int InnerPadding = 8;

    private readonly SpriteBatch _spriteBatch;
    private readonly SpriteFont _font;
    private readonly Texture2D _pixel;
    private readonly List<UiButton> _buttons;
    private readonly Dictionary<TransformationCommand, UiButton> _buttonByCommand;
    private readonly TransformationControlLayout _layout;

    private MouseState _previousMouse;
    private KeyboardState _previousKeyboard;
    private bool _hasPreviousMouse;
    private TransformationCommand? _activeAnimationCommand;
    private bool _showPhysicsPanel;
    private bool _showGravityLab;
    private bool _showNBodyLab;
    private readonly IntegerInputField _bodyCountInput = new("500");
    private readonly IntegerInputField _seedInput = new("1337");

    public TransformationControlPanel(GraphicsDevice graphicsDevice, SpriteFont font)
    {
        _spriteBatch = new SpriteBatch(graphicsDevice);
        _font = font;
        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData([Color.White]);

        _buttons = TransformationControlCatalog.CreateButtons();
        _buttonByCommand = _buttons.ToDictionary(button => button.Command);
        _layout = new TransformationControlLayout(
            _buttonByCommand,
            _bodyCountInput,
            _seedInput);
    }

    public Rectangle Bounds { get; private set; }

    public bool IsGravityLabView => _showPhysicsPanel && _showGravityLab;

    public bool IsNBodyLabView => _showPhysicsPanel && _showNBodyLab;

    public bool Contains(Point point) => Bounds.Contains(point);

    public TransformationCommand? Update(
        MouseState mouse,
        KeyboardState keyboard,
        bool isGameActive,
        int viewportWidth,
        int viewportHeight,
        bool isAnimationActive,
        IGeometry4D geometry,
        bool isFractalGenerationActive,
        PhysicsWorld4D physicsWorld,
        NBodyLab4D nBodyLab)
    {
        var isSpiral = geometry.VisualStyle == GeometryVisualStyle4D.Spiral;
        var isFractal = geometry.VisualStyle == GeometryVisualStyle4D.Fractal;
        Layout(viewportWidth, viewportHeight, isSpiral, isFractal, _showPhysicsPanel, _showGravityLab);
        var previousMouse = _hasPreviousMouse
            ? _previousMouse
            : ReleasedMouseAt(mouse.X, mouse.Y, mouse.ScrollWheelValue);
        TransformationCommand? requestedCommand = null;

        if (_showNBodyLab)
        {
            var previousKeyboard = _hasPreviousMouse ? _previousKeyboard : new KeyboardState();
            if (_bodyCountInput.Update(mouse, previousMouse, keyboard, previousKeyboard, isGameActive))
            {
                requestedCommand = TransformationCommand.ApplyNBodyCount;
            }
            if (_seedInput.Update(mouse, previousMouse, keyboard, previousKeyboard, isGameActive) &&
                requestedCommand is null)
            {
                requestedCommand = TransformationCommand.ApplyNBodySeed;
            }
        }

        foreach (var button in _buttons)
        {
            var isApplicable = IsApplicable(
                button.Command,
                isSpiral,
                isFractal,
                _showPhysicsPanel,
                _showGravityLab);
            var isEnabled = isGameActive && isApplicable &&
                (!isAnimationActive || !IsAnimationCommand(button.Command)) &&
                (button.Command != TransformationCommand.CancelFractalGeneration ||
                    isFractalGenerationActive) &&
                (button.Command != TransformationCommand.GenerateFractal ||
                    !isFractalGenerationActive) &&
                (button.Command != TransformationCommand.StepPhysics ||
                    (physicsWorld.IsEnabled && physicsWorld.IsPaused)) &&
                (button.Command != TransformationCommand.ClearParticles || physicsWorld.Bodies.Count > 0);
            if (button.Update(mouse, previousMouse, isEnabled) && requestedCommand is null)
            {
                requestedCommand = button.Command;
            }
        }

        _previousMouse = mouse;
        _previousKeyboard = keyboard;
        _hasPreviousMouse = isGameActive;
        if (requestedCommand == TransformationCommand.OpenPhysicsPanel)
        {
            _showPhysicsPanel = true;
            _showGravityLab = false;
            _showNBodyLab = false;
            return null;
        }

        if (requestedCommand == TransformationCommand.ClosePhysicsPanel)
        {
            _showPhysicsPanel = false;
            return null;
        }

        if (requestedCommand == TransformationCommand.OpenParticlePhysicsView)
        {
            _showGravityLab = false;
            _showNBodyLab = false;
            return null;
        }

        if (requestedCommand == TransformationCommand.OpenGravityLabView)
        {
            _showGravityLab = true;
            _showNBodyLab = false;
            return null;
        }

        if (requestedCommand == TransformationCommand.OpenNBodyLabView)
        {
            _showGravityLab = false;
            _showNBodyLab = true;
            return null;
        }

        if (requestedCommand == TransformationCommand.ApplyNBodyCount)
        {
            nBodyLab.Settings.TryApplyBodyCount(_bodyCountInput.Text, out _);
            _bodyCountInput.SetText(nBodyLab.Settings.BodyCount.ToString());
            return null;
        }

        if (requestedCommand == TransformationCommand.ApplyNBodySeed)
        {
            nBodyLab.Settings.TryApplySeed(_seedInput.Text);
            _seedInput.SetText(nBodyLab.Settings.Seed.ToString());
            return null;
        }

        if (requestedCommand == TransformationCommand.RandomizeNBodySeed)
        {
            nBodyLab.Settings.SetRandomSeed();
            _seedInput.SetText(nBodyLab.Settings.Seed.ToString());
            return null;
        }

        if (requestedCommand == TransformationCommand.GenerateNBodySystem)
        {
            nBodyLab.Settings.TryApplyBodyCount(_bodyCountInput.Text, out _);
            nBodyLab.Settings.TryApplySeed(_seedInput.Text);
            _bodyCountInput.SetText(nBodyLab.Settings.BodyCount.ToString());
            _seedInput.SetText(nBodyLab.Settings.Seed.ToString());
        }

        return requestedCommand;
    }

    public void SetActiveState(
        TransformationCommand? animationCommand,
        DisplayOptions displayOptions,
        GeometryVisualStyle4D selectedStyle,
        CurvePlayback4D curvePlayback,
        FractalVisualizationSettings fractalVisualization,
        bool isFractalGenerationActive,
        PhysicsWorld4D physicsWorld,
        NBodyLab4D nBodyLab,
        bool showPhysicsPlane,
        bool showGravityTrail,
        bool showGravityField)
    {
        _activeAnimationCommand = animationCommand;
        _buttonByCommand[TransformationCommand.ToggleVertices].SetLabel(
            selectedStyle == GeometryVisualStyle4D.Fractal ? "SHOW POINTS" : "SHOW VERTICES");
        _buttonByCommand[TransformationCommand.CycleFractalPointSize].SetLabel(
            $"POINT {fractalVisualization.PointSize}");
        _buttonByCommand[TransformationCommand.TogglePhysicsEnabled].SetLabel(
            physicsWorld.IsEnabled ? "PHYSICS ON" : "PHYSICS OFF");
        _buttonByCommand[TransformationCommand.TogglePhysicsCollisions].SetLabel(
            physicsWorld.CollisionsEnabled ? "COLLISIONS ON" : "COLLISIONS OFF");
        _buttonByCommand[TransformationCommand.TogglePhysicsPlane].SetLabel(
            showPhysicsPlane ? "PLANE ON" : "PLANE OFF");
        _buttonByCommand[TransformationCommand.ToggleMutualGravity].SetLabel(
            physicsWorld.MutualGravityEnabled ? "PAIR GRAV ON" : "PAIR GRAV OFF");
        _buttonByCommand[TransformationCommand.ToggleGravityTrail].SetLabel(
            showGravityTrail ? "TRAIL ON" : "TRAIL OFF");
        _buttonByCommand[TransformationCommand.ToggleGravityField].SetLabel(
            showGravityField ? "FIELD ON" : "FIELD OFF");
        _buttonByCommand[TransformationCommand.ToggleNBodyGravity].SetLabel(
            physicsWorld.MutualGravityEnabled ? "GRAVITY ON" : "GRAVITY OFF");
        _buttonByCommand[TransformationCommand.ToggleNBodyAggregation].SetLabel(
            physicsWorld.AggregationEnabled ? "MERGE ON" : "MERGE OFF");
        foreach (var button in _buttons)
        {
            button.SetActive(button.Command == animationCommand ||
                IsSelectedObject(button.Command, selectedStyle) ||
                (button.Command == TransformationCommand.OpenParticlePhysicsView &&
                    _showPhysicsPanel && !_showGravityLab && !_showNBodyLab) ||
                (button.Command == TransformationCommand.OpenGravityLabView &&
                    _showPhysicsPanel && _showGravityLab) ||
                (button.Command == TransformationCommand.OpenNBodyLabView &&
                    _showPhysicsPanel && _showNBodyLab) ||
                IsEnabledDisplayToggle(button.Command, displayOptions) ||
                (button.Command == TransformationCommand.PlayCurve && curvePlayback.IsPlaying) ||
                (button.Command == TransformationCommand.GenerateFractal && isFractalGenerationActive) ||
                (button.Command == TransformationCommand.ColorFractalByW &&
                    fractalVisualization.ColorMode == FractalColorMode.WCoordinate) ||
                (button.Command == TransformationCommand.ColorFractalByIterations &&
                    fractalVisualization.ColorMode == FractalColorMode.EscapeIterations) ||
                (button.Command == TransformationCommand.ToggleFractalWSlice &&
                    fractalVisualization.ShowWSlice) ||
                (button.Command == TransformationCommand.TogglePhysicsEnabled &&
                    physicsWorld.IsEnabled) ||
                (button.Command == TransformationCommand.PlayPhysics &&
                    physicsWorld.IsEnabled && !physicsWorld.IsPaused) ||
                (button.Command == TransformationCommand.PausePhysics &&
                    physicsWorld.IsEnabled && physicsWorld.IsPaused) ||
                (button.Command == TransformationCommand.TogglePhysicsCollisions &&
                    physicsWorld.CollisionsEnabled) ||
                (button.Command == TransformationCommand.TogglePhysicsPlane && showPhysicsPlane) ||
                (button.Command == TransformationCommand.ToggleMutualGravity &&
                    physicsWorld.MutualGravityEnabled) ||
                (button.Command == TransformationCommand.ToggleGravityTrail && showGravityTrail) ||
                (button.Command == TransformationCommand.ToggleGravityField && showGravityField) ||
                (button.Command == TransformationCommand.ToggleNBodyGravity &&
                    physicsWorld.MutualGravityEnabled) ||
                (button.Command == TransformationCommand.ToggleNBodyAggregation &&
                    physicsWorld.AggregationEnabled) ||
                (button.Command == TransformationCommand.SelectNBodyExactGravity &&
                    physicsWorld.RequestedGravityMode == GravityMode4D.Exact) ||
                (button.Command == TransformationCommand.SelectNBodyApproximateGravity &&
                    physicsWorld.RequestedGravityMode == GravityMode4D.MeanFieldApproximate) ||
                (button.Command == TransformationCommand.ColorNBodyByW &&
                    nBodyLab.ColorMode == NBodyColorMode4D.WDepth) ||
                (button.Command == TransformationCommand.ColorNBodyByMass &&
                    nBodyLab.ColorMode == NBodyColorMode4D.Mass) ||
                (button.Command == TransformationCommand.ColorNBodyBySpeed &&
                    nBodyLab.ColorMode == NBodyColorMode4D.Speed) ||
                (button.Command == TransformationCommand.DisableNBodyTrail &&
                    nBodyLab.TrailMode == NBodyTrailMode4D.Off) ||
                (button.Command == TransformationCommand.EnableSelectedNBodyTrail &&
                    nBodyLab.TrailMode == NBodyTrailMode4D.SelectedBody));
        }
    }

    public void Draw(
        int viewportWidth,
        int viewportHeight,
        TransformationAnimator4D animator,
        DisplayOptions displayOptions,
        IGeometry4D geometry,
        SpiralParameters pendingSpiralParameters,
        CurvePlayback4D curvePlayback,
        JuliaParameters pendingJuliaParameters,
        FractalVisualizationSettings fractalVisualization,
        QuaternionJuliaGeneration4D? fractalGeneration,
        PhysicsWorld4D physicsWorld,
        GravityLab4D gravityLab,
        NBodyLab4D nBodyLab,
        Vector4D pendingParticleVelocity,
        bool showPhysicsPlane,
        bool showGravityTrail,
        bool showGravityField)
    {
        var isSpiral = geometry.VisualStyle == GeometryVisualStyle4D.Spiral;
        var isFractal = geometry.VisualStyle == GeometryVisualStyle4D.Fractal;
        Layout(viewportWidth, viewportHeight, isSpiral, isFractal, _showPhysicsPanel, _showGravityLab);
        _spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.NonPremultiplied,
            SamplerState.PointClamp,
            DepthStencilState.None,
            RasterizerState.CullNone);

        _spriteBatch.Draw(_pixel, Bounds, new Color(12, 17, 30));
        _spriteBatch.Draw(_pixel, new Rectangle(Bounds.X, 0, 2, Bounds.Height), new Color(65, 91, 135));
        DrawHeader(
            animator,
            curvePlayback,
            isSpiral,
            fractalGeneration,
            physicsWorld,
            _showPhysicsPanel,
            _showGravityLab);

        if (_showPhysicsPanel)
        {
            if (_showNBodyLab)
            {
                DrawNBodyLabGroups(physicsWorld);
            }
            else if (_showGravityLab)
            {
                DrawGravityLabGroups(physicsWorld);
            }
            else
            {
                DrawPhysicsGroups(physicsWorld);
            }

            foreach (var button in _buttons)
            {
                if (IsApplicable(
                    button.Command,
                    isSpiral,
                    isFractal,
                    showPhysicsPanel: true,
                    showGravityLab: _showGravityLab))
                {
                    DrawButton(button);
                }
            }

            if (_showNBodyLab)
            {
                DrawNBodyLabValues(physicsWorld, nBodyLab);
                DrawIntegerInput(_bodyCountInput);
                DrawIntegerInput(_seedInput);
            }
            else if (_showGravityLab)
            {
                DrawGravityLabValues(
                    physicsWorld,
                    gravityLab,
                    showGravityTrail,
                    showGravityField);
            }
            else
            {
                DrawPhysicsValues(
                    physicsWorld,
                    pendingParticleVelocity,
                    showPhysicsPlane);
            }
            _spriteBatch.End();
            return;
        }

        DrawGroup(new Rectangle(Bounds.X + Padding, 63, Bounds.Width - (2 * Padding), 145),
            "OBJECT", VisualizationPalette.ObjectInfoAccent);

        if (isFractal)
        {
            DrawGroup(new Rectangle(Bounds.X + Padding, 214, Bounds.Width - (2 * Padding), 270),
                "4D FRACTAL / JULIA PARAMETERS", VisualizationPalette.FractalAccent);
            if (geometry is QuaternionJuliaSet4D fractal &&
                fractal.Parameters != pendingJuliaParameters)
            {
                DrawLabel("PENDING", Bounds.X + 266, 218, new Color(255, 207, 92));
            }
            DrawGroup(new Rectangle(Bounds.X + Padding, 490, Bounds.Width - (2 * Padding), 140),
                $"FRACTAL VIEW  W {fractalVisualization.SliceW:0.00}  POINT {fractalVisualization.PointSize}",
                VisualizationPalette.DisplayAccent);
            DrawGroup(new Rectangle(Bounds.X + Padding, 636, Bounds.Width - (2 * Padding), 82),
                "ROTATIONS", VisualizationPalette.RotationAccent);
            DrawGroup(new Rectangle(Bounds.X + Padding, 724, Bounds.Width - (2 * Padding), 106),
                "TRANSFORMS", VisualizationPalette.TransformAccent);
            DrawGroup(new Rectangle(Bounds.X + Padding, 836, Bounds.Width - (2 * Padding), 55),
                "SYSTEM", VisualizationPalette.SystemAccent);
        }
        else if (isSpiral)
        {
            DrawGroup(new Rectangle(Bounds.X + Padding, 214, Bounds.Width - (2 * Padding), 218),
                "4D SPIRAL PARAMETERS", VisualizationPalette.CurveAccent);
            if (geometry is Spiral4D spiral && spiral.Parameters != pendingSpiralParameters)
            {
                DrawLabel("PENDING", Bounds.X + 246, 218, new Color(255, 207, 92));
            }
            DrawGroup(new Rectangle(Bounds.X + Padding, 438, Bounds.Width - (2 * Padding), 111),
                "ROTATIONS", VisualizationPalette.RotationAccent);
            DrawGroup(new Rectangle(Bounds.X + Padding, 555, Bounds.Width - (2 * Padding), 165),
                "TRANSFORMS", VisualizationPalette.TransformAccent);
            DrawGroup(new Rectangle(Bounds.X + Padding, 726, Bounds.Width - (2 * Padding), 58),
                "SYSTEM", VisualizationPalette.SystemAccent);
            DrawGroup(new Rectangle(Bounds.X + Padding, 790, Bounds.Width - (2 * Padding), 110),
                "DISPLAY", VisualizationPalette.DisplayAccent);
        }
        else
        {
            DrawGroup(new Rectangle(Bounds.X + Padding, 214, Bounds.Width - (2 * Padding), 111),
                "ROTATIONS", VisualizationPalette.RotationAccent);
            DrawGroup(new Rectangle(Bounds.X + Padding, 331, Bounds.Width - (2 * Padding), 165),
                "TRANSFORMS", VisualizationPalette.TransformAccent);
            DrawGroup(new Rectangle(Bounds.X + Padding, 502, Bounds.Width - (2 * Padding), 58),
                "SYSTEM", VisualizationPalette.SystemAccent);
            DrawGroup(new Rectangle(Bounds.X + Padding, 566, Bounds.Width - (2 * Padding), 326),
                "DISPLAY", VisualizationPalette.DisplayAccent);
        }

        foreach (var button in _buttons)
        {
            if (IsApplicable(
                button.Command,
                isSpiral,
                isFractal,
                showPhysicsPanel: false,
                showGravityLab: false))
            {
                DrawButton(button);
            }
        }

        DrawObjectInfo(geometry);
        if (isFractal)
        {
            DrawFractalParameters(pendingJuliaParameters, geometry, fractalGeneration);
        }
        else if (isSpiral)
        {
            DrawSpiralParameters(pendingSpiralParameters);
        }
        else
        {
            DrawPolytopeLegend(geometry, displayOptions);
        }

        _spriteBatch.End();
    }

    public void Dispose()
    {
        _pixel.Dispose();
        _spriteBatch.Dispose();
    }

    private void DrawHeader(
        TransformationAnimator4D animator,
        CurvePlayback4D curvePlayback,
        bool isSpiral,
        QuaternionJuliaGeneration4D? fractalGeneration,
        PhysicsWorld4D physicsWorld,
        bool showPhysicsPanel,
        bool showGravityLab)
    {
        DrawLabel("4D GEOMETRY EXPLORER", Bounds.X + Padding, 7, new Color(225, 235, 255));
        var physicsState = !physicsWorld.IsEnabled
            ? "OFF"
            : physicsWorld.IsPaused
                ? "PAUSED"
                : "RUNNING";
        var activeLabel = showPhysicsPanel
            ? _showNBodyLab
                ? $"N-Body Lab: {physicsState}  Bodies: {physicsWorld.Bodies.Count:N0}"
                : showGravityLab
                ? $"Gravity Lab: {physicsState}  Bodies: {physicsWorld.Bodies.Count}"
                : $"Physics: {physicsState}  Bodies: {physicsWorld.Bodies.Count}"
            : animator.IsActive
            ? $"Active: {animator.ActiveLabel}"
            : fractalGeneration is not null
                ? "Active: Generating 4D fractal"
            : isSpiral && curvePlayback.IsPlaying
                ? "Active: Drawing curve"
                : "Active: Ready";
        DrawLabel(
            activeLabel,
            Bounds.X + Padding,
            26,
            (showPhysicsPanel && physicsWorld.IsEnabled && !physicsWorld.IsPaused) ||
                animator.IsActive || curvePlayback.IsPlaying || fractalGeneration is not null
                ? new Color(255, 207, 92)
                : new Color(122, 193, 164));
        var detail = showPhysicsPanel
            ? $"Fixed {physicsWorld.FixedDeltaTime:0.0000}s  time x{physicsWorld.TimeScale:0.##}"
            : animator.ActiveRotationPlane.HasValue
            ? $"Angle: {animator.CurrentRotationDegrees:0.0} / 90 deg"
            : animator.IsActive
                ? $"Progress: {animator.Progress * 100.0:0}%"
                : fractalGeneration is not null
                    ? $"Generation: {fractalGeneration.Progress * 100.0:0.0}%"
                : isSpiral && curvePlayback.IsPlaying
                    ? $"Curve: {curvePlayback.Progress * 100.0:0}%"
                    : "One selected object at a time";
        DrawLabel(detail, Bounds.X + Padding, 43, new Color(153, 171, 205));
    }

    private void Layout(
        int viewportWidth,
        int viewportHeight,
        bool isSpiral,
        bool isFractal,
        bool showPhysicsPanel,
        bool showGravityLab)
    {
        _layout.Apply(
            viewportWidth,
            viewportHeight,
            isSpiral,
            isFractal,
            showPhysicsPanel,
            showGravityLab,
            _showNBodyLab);
        Bounds = _layout.Bounds;
    }
    private void DrawObjectInfo(IGeometry4D geometry)
    {
        var left = Bounds.X + Padding + InnerPadding;
        DrawLabel(
            $"V {geometry.Vertices.Count}   E {geometry.Edges.Count}   F {geometry.Faces.Count}   C {geometry.Cells.Count}",
            left,
            174,
            new Color(210, 222, 244));
        DrawLabel(geometry.ResolutionDescription, left, 191, new Color(133, 158, 195));
    }

    private void DrawSpiralParameters(SpiralParameters parameters)
    {
        var left = Bounds.X + Padding + InnerPadding;
        DrawLabel($"r1    {parameters.R1:0.00}", left, 242, new Color(205, 224, 239));
        DrawLabel($"r2    {parameters.R2:0.00}", left, 271, new Color(205, 224, 239));
        DrawLabel($"k     {parameters.K:0.00}", left, 300, new Color(205, 224, 239));
        DrawLabel($"Samples {parameters.SampleCount}", left, 329, new Color(205, 224, 239));
        DrawLegendEntry(left, 412, VisualizationPalette.CurveStart, "START: octahedron");
        DrawLegendEntry(left + 145, 412, VisualizationPalette.CurveEnd, "END: cube");
    }

    private void DrawFractalParameters(
        JuliaParameters parameters,
        IGeometry4D geometry,
        QuaternionJuliaGeneration4D? generation)
    {
        var left = Bounds.X + Padding + InnerPadding;
        var color = new Color(205, 224, 239);
        DrawLabel($"C.a  {parameters.Constant.A,7:0.000}", left, 242, color);
        DrawLabel($"C.b  {parameters.Constant.B,7:0.000}", left, 267, color);
        DrawLabel($"C.c  {parameters.Constant.C,7:0.000}", left, 292, color);
        DrawLabel($"C.d  {parameters.Constant.D,7:0.000}", left, 317, color);
        DrawLabel($"Max iterations  {parameters.MaxIterations}", left, 342, color);
        DrawLabel($"Escape radius   {parameters.EscapeRadius:0.00}", left, 367, color);
        DrawLabel($"Resolution      {parameters.Resolution}^4", left, 392, color);

        if (generation is not null)
        {
            DrawLabel(
                $"Generating {generation.Progress * 100.0:0.0}%  " +
                $"{generation.ProcessedSampleCount:N0}/{generation.TotalSampleCount:N0}",
                left,
                470,
                new Color(255, 207, 92));
        }
        else if (geometry is QuaternionJuliaSet4D fractal && fractal.Samples.Count > 0)
        {
            DrawLabel(
                $"Ready: {fractal.BoundedPointCount:N0} bounded  {fractal.GenerationTime.TotalSeconds:0.000}s",
                left,
                470,
                new Color(122, 193, 164));
        }
        else
        {
            DrawLabel("No dataset yet - press GENERATE", left, 470, new Color(153, 171, 205));
        }
    }

    private void DrawPolytopeLegend(IGeometry4D geometry, DisplayOptions options)
    {
        var left = Bounds.X + Padding + InnerPadding;
        var halfWidth = (Bounds.Width - (2 * (Padding + InnerPadding))) / 2;
        DrawLabel("SURFACE / CELLS", left, 678,
            options.ShowCells ? VisualizationPalette.DisplayAccent : new Color(92, 101, 120));

        if (geometry.VisualStyle == GeometryVisualStyle4D.Tesseract)
        {
            for (var index = 0; index < geometry.Cells.Count; index++)
            {
                DrawLegendEntry(
                    left + ((index % 2) * halfWidth),
                    696 + ((index / 2) * 17),
                    VisualizationPalette.CellColor(index),
                    geometry.Cells[index].Label);
            }
        }
        else if (geometry.VisualStyle == GeometryVisualStyle4D.Simplex)
        {
            for (var index = 0; index < geometry.Cells.Count; index++)
            {
                DrawLegendEntry(
                    left + ((index % 2) * halfWidth),
                    696 + ((index / 2) * 17),
                    VisualizationPalette.CellColor(index, geometry.VisualStyle),
                    geometry.Cells[index].Label);
            }
        }
        else
        {
            DrawLegendEntry(left, 696, VisualizationPalette.WDepthColor(geometry.VisualStyle, 0.0f), "W-");
            DrawLegendEntry(left + halfWidth, 696, VisualizationPalette.WDepthColor(geometry.VisualStyle, 1.0f), "W+");
            DrawLabel(
                geometry.Cells.Count == 0 ? "sampled 2-sphere shells" : $"{geometry.Cells.Count} tetrahedral cells",
                left,
                714,
                new Color(155, 174, 207));
        }

        var edgeLegend = geometry.VisualStyle == GeometryVisualStyle4D.Tesseract
            ? "EDGES: XYZW direction + camera W shade"
            : "EDGES: local W color + camera W shade";
        DrawLabel(edgeLegend, left, 773,
            options.ShowEdges ? new Color(185, 204, 233) : new Color(92, 101, 120));
        DrawLabel("VERTICES: W depth, style tinted", left, 793,
            options.ShowVertices ? new Color(185, 204, 233) : new Color(92, 101, 120));
        DrawLabel($"Surface alpha {VisualizationPalette.CellSurfaceAlpha:0.00}",
            left, 813, new Color(112, 128, 155));
    }

    private void DrawPhysicsGroups(PhysicsWorld4D world)
    {
        var width = Bounds.Width - (2 * Padding);
        DrawGroup(new Rectangle(Bounds.X + Padding, 63, width, 27),
            string.Empty, VisualizationPalette.PhysicsAccent);
        DrawGroup(new Rectangle(Bounds.X + Padding, 96, width, 112),
            $"SIMULATION  x{world.TimeScale:0.##}", VisualizationPalette.PhysicsAccent);
        DrawGroup(new Rectangle(Bounds.X + Padding, 214, width, 203),
            "4D GRAVITY", VisualizationPalette.TransformAccent);
        DrawGroup(new Rectangle(Bounds.X + Padding, 423, width, 145),
            "INITIAL 4D VELOCITY", VisualizationPalette.RotationAccent);
        DrawGroup(new Rectangle(Bounds.X + Padding, 574, width, 82),
            "PARTICLES", VisualizationPalette.ObjectInfoAccent);
        DrawGroup(new Rectangle(Bounds.X + Padding, 662, width, 111),
            "W=0 HYPERPLANE COLLISION", VisualizationPalette.DisplayAccent);
        DrawGroup(new Rectangle(Bounds.X + Padding, 779, width, 112),
            "PHYSICS DEBUG", VisualizationPalette.SystemAccent);
    }

    private void DrawGravityLabGroups(PhysicsWorld4D world)
    {
        var width = Bounds.Width - (2 * Padding);
        DrawGroup(new Rectangle(Bounds.X + Padding, 63, width, 27),
            string.Empty, VisualizationPalette.GravityLabAccent);
        DrawGroup(new Rectangle(Bounds.X + Padding, 96, width, 83),
            $"SIMULATION  x{world.TimeScale:0.##}", VisualizationPalette.PhysicsAccent);
        DrawGroup(new Rectangle(Bounds.X + Padding, 185, width, 110),
            "4D PAIR GRAVITY", VisualizationPalette.GravityLabAccent);
        DrawGroup(new Rectangle(Bounds.X + Padding, 301, width, 208),
            "CENTRAL + ORBITER INITIAL POSITION", VisualizationPalette.ObjectInfoAccent);
        DrawGroup(new Rectangle(Bounds.X + Padding, 515, width, 195),
            "INITIAL 4D VELOCITY / PRESETS", VisualizationPalette.RotationAccent);
        DrawGroup(new Rectangle(Bounds.X + Padding, 716, width, 175),
            "4D TRAIL / LIVE", VisualizationPalette.DisplayAccent);
    }

    private void DrawNBodyLabGroups(PhysicsWorld4D world)
    {
        var width = Bounds.Width - (2 * Padding);
        DrawGroup(new Rectangle(Bounds.X + Padding, 63, width, 27),
            string.Empty, VisualizationPalette.GravityLabAccent);
        DrawGroup(new Rectangle(Bounds.X + Padding, 96, width, 83),
            $"SIMULATION  x{world.TimeScale:0.##}", VisualizationPalette.PhysicsAccent);
        DrawGroup(new Rectangle(Bounds.X + Padding, 185, width, 115),
            "RANDOM CLOUD", VisualizationPalette.ObjectInfoAccent);
        DrawGroup(new Rectangle(Bounds.X + Padding, 306, width, 142),
            "POSITION HALF-RANGES", VisualizationPalette.TransformAccent);
        DrawGroup(new Rectangle(Bounds.X + Padding, 454, width, 169),
            "SPEED / MASS / SIZE", VisualizationPalette.RotationAccent);
        DrawGroup(new Rectangle(Bounds.X + Padding, 629, width, 117),
            "4D GRAVITY / AGGREGATION", VisualizationPalette.GravityLabAccent);
        DrawGroup(new Rectangle(Bounds.X + Padding, 752, width, 139),
            "QUALITY / COLOR / LIVE", VisualizationPalette.DisplayAccent);
    }

    private void DrawNBodyLabValues(PhysicsWorld4D world, NBodyLab4D lab)
    {
        var left = Bounds.X + Padding + InnerPadding;
        var color = new Color(205, 224, 239);
        var settings = lab.Settings;

        DrawLabel("Count", left, 213, color);
        DrawLabel("Seed", left, 242, color);
        DrawLabel(lab.LastGenerationMessage, left, 287,
            lab.HasSystem ? new Color(122, 193, 164) : new Color(255, 207, 92));

        DrawLabel($"X +/- {settings.PositionHalfRanges.X,6:0.0}", left, 334, color);
        DrawLabel($"Y +/- {settings.PositionHalfRanges.Y,6:0.0}", left, 363, color);
        DrawLabel($"Z +/- {settings.PositionHalfRanges.Z,6:0.0}", left, 392, color);
        DrawLabel($"W +/- {settings.PositionHalfRanges.W,6:0.0}", left, 421, color);

        DrawLabel($"Speed min {settings.MinimumSpeed,6:0.0}", left, 482, color);
        DrawLabel($"Speed max {settings.MaximumSpeed,6:0.0}", left, 511, color);
        DrawLabel($"Mass min  {settings.MinimumMass,6:0.0}", left, 540, color);
        DrawLabel($"Mass max  {settings.MaximumMass,6:0.0}", left, 569, color);
        DrawLabel($"radius k {settings.RadiusScale:0.00}", left, 598, color);
        DrawLabel($"point x{settings.PointScale:0.00}", left + 160, 598, color);

        DrawLabel($"G {world.GravitySystem.GravitationalConstant,8:0.000}", left, 657, color);
        DrawLabel($"Softening {world.GravitySystem.Softening,5:0.00}", left, 686, color);

        var requested = world.RequestedGravityMode == GravityMode4D.Exact ? "EXACT" : "MEAN";
        var effective = world.EffectiveGravityMode == GravityMode4D.Exact ? "EXACT" : "MEAN";
        DrawLabel($"Requested {requested}  effective {effective}  collision /{world.AggregationCollisionInterval}",
            left, 858, color);
        DrawLabel(
            $"N {world.Bodies.Count:N0}  merge {world.AggregationCollisionCount:N0}  " +
            $"{world.LastPhysicsStepMilliseconds:0.0} ms  {world.SimulationStepsPerSecond:0} step/s",
            left,
            875,
            color);
    }

    private void DrawIntegerInput(IntegerInputField input)
    {
        var border = input.IsFocused ? new Color(255, 207, 92) : new Color(65, 91, 135);
        _spriteBatch.Draw(_pixel, input.Bounds, border);
        var inside = new Rectangle(
            input.Bounds.X + 1,
            input.Bounds.Y + 1,
            Math.Max(0, input.Bounds.Width - 2),
            Math.Max(0, input.Bounds.Height - 2));
        _spriteBatch.Draw(_pixel, inside, new Color(9, 14, 25));
        DrawLabel(input.Text, input.Bounds.X + 6, input.Bounds.Y + 4, new Color(225, 235, 255));
    }

    private void DrawGravityLabValues(
        PhysicsWorld4D world,
        GravityLab4D lab,
        bool showGravityTrail,
        bool showGravityField)
    {
        var left = Bounds.X + Padding + InnerPadding;
        var color = new Color(205, 224, 239);
        DrawLabel($"G {world.GravitySystem.GravitationalConstant,8:0.000}", left, 213, color);
        DrawLabel($"Softening {world.GravitySystem.Softening,5:0.00}", left, 242, color);

        DrawLabel($"Central mass {lab.CentralMass,7:0}", left, 329, color);
        DrawLabel("Central P (0.00, 0.00, 0.00, 0.00) STATIC", left, 354,
            VisualizationPalette.GravityCentralMass);
        DrawLabel($"Orbiter X {lab.OrbiterInitialPosition.X,7:0.00}", left, 387, color);
        DrawLabel($"Orbiter Y {lab.OrbiterInitialPosition.Y,7:0.00}", left, 416, color);
        DrawLabel($"Orbiter Z {lab.OrbiterInitialPosition.Z,7:0.00}", left, 445, color);
        DrawLabel($"Orbiter W {lab.OrbiterInitialPosition.W,7:0.00}", left, 474, color);
        DrawLabel("Pending values apply on RESET EXP", left, 493, new Color(153, 171, 205));

        DrawLabel($"Velocity X {lab.OrbiterInitialVelocity.X,7:0.00}", left, 543, color);
        DrawLabel($"Velocity Y {lab.OrbiterInitialVelocity.Y,7:0.00}", left, 572, color);
        DrawLabel($"Velocity Z {lab.OrbiterInitialVelocity.Z,7:0.00}", left, 601, color);
        DrawLabel($"Velocity W {lab.OrbiterInitialVelocity.W,7:0.00}", left, 630, color);

        DrawLabel($"Trail length {lab.Trail.Capacity,5}", left, 804, color);
        var diagnostics = lab.Diagnostics;
        if (!diagnostics.IsAvailable)
        {
            DrawLabel("RESET EXP creates central mass + orbiter.", left, 834,
                new Color(153, 171, 205));
            return;
        }

        DrawLabel(
            $"Distance {diagnostics.Distance:0.000}  Speed {diagnostics.Speed:0.000}  W {diagnostics.OrbiterW:0.000}",
            left,
            830,
            color);
        DrawLabel(
            $"Central accel {diagnostics.CentralAccelerationMagnitude:0.0000}  " +
            $"trail {lab.Trail.Points.Count}/{lab.Trail.Capacity}",
            left,
            849,
            color);
        DrawLabel(
            $"Toward center ({diagnostics.DirectionTowardCentral.X:0.00}, " +
            $"{diagnostics.DirectionTowardCentral.Y:0.00}, " +
            $"{diagnostics.DirectionTowardCentral.Z:0.00}, " +
            $"{diagnostics.DirectionTowardCentral.W:0.00})",
            left,
            868,
            showGravityTrail || showGravityField
                ? VisualizationPalette.GravityLabAccent
                : new Color(153, 171, 205));
    }

    private void DrawPhysicsValues(
        PhysicsWorld4D world,
        Vector4D initialVelocity,
        bool showPhysicsPlane)
    {
        var left = Bounds.X + Padding + InnerPadding;
        var color = new Color(205, 224, 239);
        var state = !world.IsEnabled
            ? "OFF"
            : world.IsPaused
                ? "PAUSED"
                : "RUNNING";
        DrawLabel(
            $"{state}   dt {world.FixedDeltaTime:0.0000}s   step {world.CompletedStepCount:N0}",
            left,
            180,
            world.IsEnabled ? new Color(122, 193, 164) : new Color(155, 164, 181));

        DrawLabel($"Gravity X  {world.Gravity.X,7:0.00}", left, 242, color);
        DrawLabel($"Gravity Y  {world.Gravity.Y,7:0.00}", left, 271, color);
        DrawLabel($"Gravity Z  {world.Gravity.Z,7:0.00}", left, 300, color);
        DrawLabel($"Gravity W  {world.Gravity.W,7:0.00}", left, 329, color);

        DrawLabel($"Velocity X {initialVelocity.X,7:0.00}", left, 451, color);
        DrawLabel($"Velocity Y {initialVelocity.Y,7:0.00}", left, 480, color);
        DrawLabel($"Velocity Z {initialVelocity.Z,7:0.00}", left, 509, color);
        DrawLabel($"Velocity W {initialVelocity.W,7:0.00}", left, 538, color);

        DrawLabel(
            $"Bodies {world.Bodies.Count}/{PhysicsWorld4D.MaximumParticleBodyCount}   selected " +
            (world.SelectedBody?.Id.ToString() ?? "none"),
            left,
            628,
            color);
        DrawLabel($"Restitution  {world.Restitution:0.0}", left, 690, color);

        DrawLabel(
            $"Collisions {(world.CollisionsEnabled ? "ON" : "OFF")}   " +
            $"Plane {(showPhysicsPlane ? "ON" : "OFF")}   Hits {world.CollisionCount:N0}",
            left,
            803,
            color);
        DrawLabel($"Total kinetic energy  {world.TotalKineticEnergy:0.000}",
            left, 822, color);

        if (world.SelectedBody is not { } body)
        {
            DrawLabel("Spawn a particle to inspect its 4D state.",
                left, 848, new Color(153, 171, 205));
            return;
        }

        DrawLabel(
            $"P{body.Id} P ({body.Position.X:0.00}, {body.Position.Y:0.00}, " +
            $"{body.Position.Z:0.00}, {body.Position.W:0.00})",
            left,
            841,
            new Color(255, 224, 92));
        DrawLabel(
            $"V ({body.Velocity.X:0.00}, {body.Velocity.Y:0.00}, " +
            $"{body.Velocity.Z:0.00}, {body.Velocity.W:0.00})",
            left,
            860,
            color);
        DrawLabel(
            $"A ({body.Acceleration.X:0.00}, {body.Acceleration.Y:0.00}, " +
            $"{body.Acceleration.Z:0.00}, {body.Acceleration.W:0.00})",
            left,
            879,
            color);
    }

    private void DrawGroup(Rectangle bounds, string title, Color accent)
    {
        _spriteBatch.Draw(_pixel, bounds, new Color(18, 25, 42, 224));
        _spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Y, 3, bounds.Height), accent);
        _spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, 1), accent * 0.55f);
        DrawLabel(title, bounds.X + InnerPadding, bounds.Y + 4, accent);
    }

    private void DrawButton(UiButton button)
    {
        var accent = AccentFor(button.Command);
        var isToggle = IsDisplayToggleCommand(button.Command);
        var isCurrentAnimation = button.Command == _activeAnimationCommand;
        var fill = !button.IsEnabled
            ? new Color(33, 39, 53)
            : button.IsActive && (isToggle || IsObjectCommand(button.Command) ||
                button.Command == TransformationCommand.PlayCurve ||
                IsFractalModeCommand(button.Command) ||
                IsPhysicsStateCommand(button.Command))
                ? Color.Lerp(new Color(28, 42, 65), accent, 0.32f)
                : isCurrentAnimation
                    ? new Color(104, 72, 173)
                    : button.IsPressed
                        ? Color.Lerp(new Color(28, 42, 65), accent, 0.45f)
                        : button.IsHovered
                            ? Color.Lerp(new Color(28, 42, 65), accent, 0.25f)
                            : new Color(27, 40, 61);
        var border = isCurrentAnimation
            ? new Color(255, 207, 92)
            : button.IsHovered || button.IsActive ? accent : new Color(61, 81, 112);
        var textColor = button.IsEnabled ? new Color(225, 235, 255) : new Color(92, 101, 120);

        _spriteBatch.Draw(_pixel, button.Bounds, border);
        _spriteBatch.Draw(_pixel, new Rectangle(
            button.Bounds.X + 1,
            button.Bounds.Y + 1,
            Math.Max(0, button.Bounds.Width - 2),
            Math.Max(0, button.Bounds.Height - 2)), fill);
        var label = isToggle
            ? $"{(button.IsActive ? "ON " : "OFF")}  {button.Label[5..]}"
            : button.Label;
        var size = _font.MeasureString(label);
        _spriteBatch.DrawString(_font, label, new Vector2(
            button.Bounds.Center.X - (size.X / 2.0f),
            button.Bounds.Center.Y - (size.Y / 2.0f)), textColor);
    }

    private void DrawLegendEntry(int x, int y, Color color, string label)
    {
        _spriteBatch.Draw(_pixel, new Rectangle(x, y + 2, 11, 11), color);
        DrawLabel(label, x + 17, y, new Color(190, 203, 225));
    }

    private void DrawLabel(string text, int x, int y, Color color) =>
        _spriteBatch.DrawString(_font, text, new Vector2(x, y), color);

    private static Color AccentFor(TransformationCommand command) => command switch
    {
        >= TransformationCommand.SelectTesseract and <= TransformationCommand.SelectFractal =>
            VisualizationPalette.ObjectInfoAccent,
        >= TransformationCommand.RotateXY and <= TransformationCommand.RotateZW =>
            VisualizationPalette.RotationAccent,
        >= TransformationCommand.ScaleUp and <= TransformationCommand.MoveNegativeW =>
            VisualizationPalette.TransformAccent,
        TransformationCommand.ResetObject or TransformationCommand.ResetCamera =>
            VisualizationPalette.SystemAccent,
        >= TransformationCommand.DecreaseSpiralR1 and <= TransformationCommand.ResetCurve =>
            VisualizationPalette.CurveAccent,
        >= TransformationCommand.ToggleMutualGravity => VisualizationPalette.GravityLabAccent,
        >= TransformationCommand.OpenPhysicsPanel => VisualizationPalette.PhysicsAccent,
        >= TransformationCommand.DecreaseJuliaA and <= TransformationCommand.CycleFractalPointSize =>
            VisualizationPalette.FractalAccent,
        _ => VisualizationPalette.DisplayAccent
    };

    private bool IsApplicable(
        TransformationCommand command,
        bool isSpiral,
        bool isFractal,
        bool showPhysicsPanel,
        bool showGravityLab)
    {
        if (command == TransformationCommand.OpenPhysicsPanel)
        {
            return !showPhysicsPanel;
        }

        if (command == TransformationCommand.ClosePhysicsPanel)
        {
            return showPhysicsPanel;
        }

        if (command is TransformationCommand.OpenParticlePhysicsView or
            TransformationCommand.OpenGravityLabView or
            TransformationCommand.OpenNBodyLabView)
        {
            return showPhysicsPanel;
        }

        if (showPhysicsPanel)
        {
            if (IsCommonPhysicsCommand(command))
            {
                return !_showNBodyLab ||
                    command != TransformationCommand.TogglePhysicsEnabled;
            }

            if (_showNBodyLab)
            {
                return IsNBodyLabCommand(command) ||
                    command is TransformationCommand.DecreaseGravitationalConstant or
                        TransformationCommand.IncreaseGravitationalConstant or
                        TransformationCommand.DecreaseGravitySoftening or
                        TransformationCommand.IncreaseGravitySoftening;
            }

            return showGravityLab
                ? IsGravityLabCommand(command)
                : IsParticlePhysicsCommand(command);
        }

        if (IsPhysicsControlCommand(command) || IsGravityLabCommand(command) || IsNBodyLabCommand(command))
        {
            return false;
        }

        if (command is TransformationCommand.ToggleCells or TransformationCommand.ToggleEdges)
        {
            return !isSpiral && !isFractal;
        }

        if (command == TransformationCommand.ToggleVertices)
        {
            return !isSpiral;
        }

        if (IsSpiralCommand(command))
        {
            return isSpiral;
        }

        if (IsFractalCommand(command))
        {
            return isFractal;
        }

        return true;
    }

    private static bool IsObjectCommand(TransformationCommand command) =>
        command >= TransformationCommand.SelectTesseract &&
        command <= TransformationCommand.SelectFractal;

    private static bool IsAnimationCommand(TransformationCommand command) =>
        command >= TransformationCommand.RotateXY &&
        command <= TransformationCommand.MoveNegativeW;

    private static bool IsDisplayToggleCommand(TransformationCommand command) =>
        command is TransformationCommand.ToggleGrid or
            TransformationCommand.ToggleAxes or
            TransformationCommand.ToggleCells or
            TransformationCommand.ToggleEdges or
            TransformationCommand.ToggleVertices or
            TransformationCommand.ToggleCurve or
            TransformationCommand.ToggleCurvePoints or
            TransformationCommand.ToggleCurveDirection or
            TransformationCommand.ToggleFractalWSlice;

    private static bool IsFractalModeCommand(TransformationCommand command) =>
        command is TransformationCommand.GenerateFractal or
            TransformationCommand.ColorFractalByW or
            TransformationCommand.ColorFractalByIterations;

    private static bool IsPhysicsStateCommand(TransformationCommand command) =>
        command is TransformationCommand.TogglePhysicsEnabled or
            TransformationCommand.PlayPhysics or
            TransformationCommand.PausePhysics or
            TransformationCommand.TogglePhysicsCollisions or
            TransformationCommand.TogglePhysicsPlane or
            TransformationCommand.ToggleMutualGravity or
            TransformationCommand.ToggleGravityTrail or
            TransformationCommand.ToggleGravityField or
            TransformationCommand.ToggleNBodyGravity or
            TransformationCommand.ToggleNBodyAggregation or
            TransformationCommand.SelectNBodyExactGravity or
            TransformationCommand.SelectNBodyApproximateGravity or
            TransformationCommand.ColorNBodyByW or
            TransformationCommand.ColorNBodyByMass or
            TransformationCommand.ColorNBodyBySpeed or
            TransformationCommand.DisableNBodyTrail or
            TransformationCommand.EnableSelectedNBodyTrail;

    private static bool IsSpiralCommand(TransformationCommand command) =>
        command >= TransformationCommand.DecreaseSpiralR1 &&
        command <= TransformationCommand.ToggleCurveDirection;

    private static bool IsFractalCommand(TransformationCommand command) =>
        command >= TransformationCommand.DecreaseJuliaA &&
        command <= TransformationCommand.CycleFractalPointSize;

    private static bool IsPhysicsControlCommand(TransformationCommand command) =>
        command >= TransformationCommand.TogglePhysicsEnabled &&
        command <= TransformationCommand.TogglePhysicsPlane;

    private static bool IsCommonPhysicsCommand(TransformationCommand command) =>
        command >= TransformationCommand.TogglePhysicsEnabled &&
        command <= TransformationCommand.IncreaseTimeScale;

    private static bool IsParticlePhysicsCommand(TransformationCommand command) =>
        command >= TransformationCommand.DecreaseGravityX &&
        command <= TransformationCommand.TogglePhysicsPlane;

    private static bool IsGravityLabCommand(TransformationCommand command) =>
        command >= TransformationCommand.ToggleMutualGravity &&
        command <= TransformationCommand.ResetGravityExperiment;

    private static bool IsNBodyLabCommand(TransformationCommand command) =>
        command >= TransformationCommand.ApplyNBodyCount &&
        command <= TransformationCommand.EnableSelectedNBodyTrail;

    private static bool IsSelectedObject(TransformationCommand command, GeometryVisualStyle4D style) =>
        (command, style) switch
        {
            (TransformationCommand.SelectTesseract, GeometryVisualStyle4D.Tesseract) => true,
            (TransformationCommand.SelectHypersphere, GeometryVisualStyle4D.Hypersphere) => true,
            (TransformationCommand.SelectSimplex, GeometryVisualStyle4D.Simplex) => true,
            (TransformationCommand.SelectIrregular, GeometryVisualStyle4D.Irregular) => true,
            (TransformationCommand.SelectSpiral, GeometryVisualStyle4D.Spiral) => true,
            (TransformationCommand.SelectFractal, GeometryVisualStyle4D.Fractal) => true,
            _ => false
        };

    private static bool IsEnabledDisplayToggle(TransformationCommand command, DisplayOptions options) =>
        command switch
        {
            TransformationCommand.ToggleGrid => options.ShowGrid,
            TransformationCommand.ToggleAxes => options.ShowAxes,
            TransformationCommand.ToggleCells => options.ShowCells,
            TransformationCommand.ToggleEdges => options.ShowEdges,
            TransformationCommand.ToggleVertices => options.ShowVertices,
            TransformationCommand.ToggleCurve => options.ShowEdges,
            TransformationCommand.ToggleCurvePoints => options.ShowVertices,
            TransformationCommand.ToggleCurveDirection => options.ShowDirection,
            _ => false
        };

    private static MouseState ReleasedMouseAt(int x, int y, int scrollWheelValue) =>
        new(
            x,
            y,
            scrollWheelValue,
            ButtonState.Released,
            ButtonState.Released,
            ButtonState.Released,
            ButtonState.Released,
            ButtonState.Released);
}
