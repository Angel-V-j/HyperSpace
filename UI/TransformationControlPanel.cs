using System;
using System.Collections.Generic;
using System.Linq;
using HyperSpace.Geometry;
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
    public const int PreferredWidth = 320;

    private const int Padding = 10;
    private const int InnerPadding = 8;
    private const int ColumnGap = 6;
    private const int ButtonHeight = 23;

    private readonly SpriteBatch _spriteBatch;
    private readonly SpriteFont _font;
    private readonly Texture2D _pixel;
    private readonly List<UiButton> _buttons;
    private readonly Dictionary<TransformationCommand, UiButton> _buttonByCommand;

    private MouseState _previousMouse;
    private bool _hasPreviousMouse;
    private TransformationCommand? _activeAnimationCommand;

    public TransformationControlPanel(GraphicsDevice graphicsDevice, SpriteFont font)
    {
        _spriteBatch = new SpriteBatch(graphicsDevice);
        _font = font;
        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData([Color.White]);

        _buttons =
        [
            new("TESSERACT", TransformationCommand.SelectTesseract),
            new("HYPERSPHERE", TransformationCommand.SelectHypersphere),
            new("4-SIMPLEX", TransformationCommand.SelectSimplex),
            new("IRREGULAR", TransformationCommand.SelectIrregular),
            new("4D SPIRAL", TransformationCommand.SelectSpiral),
            new("XY  +90", TransformationCommand.RotateXY),
            new("XZ  +90", TransformationCommand.RotateXZ),
            new("XW  +90", TransformationCommand.RotateXW),
            new("YZ  +90", TransformationCommand.RotateYZ),
            new("YW  +90", TransformationCommand.RotateYW),
            new("ZW  +90", TransformationCommand.RotateZW),
            new("SCALE +", TransformationCommand.ScaleUp),
            new("SCALE -", TransformationCommand.ScaleDown),
            new("+ X", TransformationCommand.MovePositiveX),
            new("- X", TransformationCommand.MoveNegativeX),
            new("+ Y", TransformationCommand.MovePositiveY),
            new("- Y", TransformationCommand.MoveNegativeY),
            new("+ Z", TransformationCommand.MovePositiveZ),
            new("- Z", TransformationCommand.MoveNegativeZ),
            new("+ W", TransformationCommand.MovePositiveW),
            new("- W", TransformationCommand.MoveNegativeW),
            new("RESET OBJECT", TransformationCommand.ResetObject),
            new("RESET CAMERA", TransformationCommand.ResetCamera),
            new("SHOW GRID", TransformationCommand.ToggleGrid),
            new("SHOW AXES", TransformationCommand.ToggleAxes),
            new("SHOW SURFACE", TransformationCommand.ToggleCells),
            new("SHOW EDGES", TransformationCommand.ToggleEdges),
            new("SHOW VERTICES", TransformationCommand.ToggleVertices),
            new("-", TransformationCommand.DecreaseSpiralR1),
            new("+", TransformationCommand.IncreaseSpiralR1),
            new("-", TransformationCommand.DecreaseSpiralR2),
            new("+", TransformationCommand.IncreaseSpiralR2),
            new("-", TransformationCommand.DecreaseSpiralK),
            new("+", TransformationCommand.IncreaseSpiralK),
            new("-", TransformationCommand.DecreaseSpiralSamples),
            new("+", TransformationCommand.IncreaseSpiralSamples),
            new("REGENERATE", TransformationCommand.RegenerateSpiral),
            new("PLAY CURVE", TransformationCommand.PlayCurve),
            new("RESET CURVE", TransformationCommand.ResetCurve),
            new("SHOW CURVE", TransformationCommand.ToggleCurve),
            new("SHOW POINTS", TransformationCommand.ToggleCurvePoints),
            new("SHOW DIRECTION", TransformationCommand.ToggleCurveDirection)
        ];
        _buttonByCommand = _buttons.ToDictionary(button => button.Command);
    }

    public Rectangle Bounds { get; private set; }

    public bool Contains(Point point) => Bounds.Contains(point);

    public TransformationCommand? Update(
        MouseState mouse,
        bool isGameActive,
        int viewportWidth,
        int viewportHeight,
        bool isAnimationActive,
        IGeometry4D geometry)
    {
        var isSpiral = geometry.VisualStyle == GeometryVisualStyle4D.Spiral;
        Layout(viewportWidth, viewportHeight, isSpiral);
        var previousMouse = _hasPreviousMouse
            ? _previousMouse
            : ReleasedMouseAt(mouse.X, mouse.Y, mouse.ScrollWheelValue);
        TransformationCommand? requestedCommand = null;

        foreach (var button in _buttons)
        {
            var isApplicable = IsApplicable(button.Command, isSpiral);
            var isEnabled = isGameActive && isApplicable &&
                (!isAnimationActive || !IsAnimationCommand(button.Command));
            if (button.Update(mouse, previousMouse, isEnabled) && requestedCommand is null)
            {
                requestedCommand = button.Command;
            }
        }

        _previousMouse = mouse;
        _hasPreviousMouse = isGameActive;
        return requestedCommand;
    }

    public void SetActiveState(
        TransformationCommand? animationCommand,
        DisplayOptions displayOptions,
        GeometryVisualStyle4D selectedStyle,
        CurvePlayback4D curvePlayback)
    {
        _activeAnimationCommand = animationCommand;
        foreach (var button in _buttons)
        {
            button.SetActive(button.Command == animationCommand ||
                IsSelectedObject(button.Command, selectedStyle) ||
                IsEnabledDisplayToggle(button.Command, displayOptions) ||
                (button.Command == TransformationCommand.PlayCurve && curvePlayback.IsPlaying));
        }
    }

    public void Draw(
        int viewportWidth,
        int viewportHeight,
        TransformationAnimator4D animator,
        DisplayOptions displayOptions,
        IGeometry4D geometry,
        SpiralParameters pendingSpiralParameters,
        CurvePlayback4D curvePlayback)
    {
        var isSpiral = geometry.VisualStyle == GeometryVisualStyle4D.Spiral;
        Layout(viewportWidth, viewportHeight, isSpiral);
        _spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.NonPremultiplied,
            SamplerState.PointClamp,
            DepthStencilState.None,
            RasterizerState.CullNone);

        _spriteBatch.Draw(_pixel, Bounds, new Color(12, 17, 30));
        _spriteBatch.Draw(_pixel, new Rectangle(Bounds.X, 0, 2, Bounds.Height), new Color(65, 91, 135));
        DrawHeader(animator, curvePlayback, isSpiral);
        DrawGroup(new Rectangle(Bounds.X + Padding, 63, Bounds.Width - (2 * Padding), 145),
            "OBJECT", VisualizationPalette.ObjectInfoAccent);

        if (isSpiral)
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
            if (IsApplicable(button.Command, isSpiral))
            {
                DrawButton(button);
            }
        }

        DrawObjectInfo(geometry);
        if (isSpiral)
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
        bool isSpiral)
    {
        DrawLabel("4D GEOMETRY EXPLORER", Bounds.X + Padding, 7, new Color(225, 235, 255));
        var activeLabel = animator.IsActive
            ? $"Active: {animator.ActiveLabel}"
            : isSpiral && curvePlayback.IsPlaying
                ? "Active: Drawing curve"
                : "Active: Ready";
        DrawLabel(
            activeLabel,
            Bounds.X + Padding,
            26,
            animator.IsActive || curvePlayback.IsPlaying
                ? new Color(255, 207, 92)
                : new Color(122, 193, 164));
        var detail = animator.ActiveRotationPlane.HasValue
            ? $"Angle: {animator.CurrentRotationDegrees:0.0} / 90 deg"
            : animator.IsActive
                ? $"Progress: {animator.Progress * 100.0:0}%"
                : isSpiral && curvePlayback.IsPlaying
                    ? $"Curve: {curvePlayback.Progress * 100.0:0}%"
                    : "One selected object at a time";
        DrawLabel(detail, Bounds.X + Padding, 43, new Color(153, 171, 205));
    }

    private void Layout(int viewportWidth, int viewportHeight, bool isSpiral)
    {
        var width = Math.Min(PreferredWidth, Math.Max(1, viewportWidth));
        Bounds = new Rectangle(viewportWidth - width, 0, width, viewportHeight);
        var contentLeft = Bounds.X + Padding + InnerPadding;
        var contentWidth = Math.Max(2, width - (2 * (Padding + InnerPadding)));
        var columnWidth = Math.Max(1, (contentWidth - ColumnGap) / 2);
        var right = contentLeft + columnWidth + ColumnGap;

        SetTwoColumnRow(TransformationCommand.SelectTesseract, TransformationCommand.SelectHypersphere,
            contentLeft, right, columnWidth, 87);
        SetTwoColumnRow(TransformationCommand.SelectSimplex, TransformationCommand.SelectIrregular,
            contentLeft, right, columnWidth, 116);
        SetBounds(TransformationCommand.SelectSpiral,
            new Rectangle(contentLeft, 145, contentWidth, ButtonHeight));

        if (isSpiral)
        {
            var adjustmentLeft = contentLeft + 145;
            const int adjustmentWidth = 62;
            var adjustmentRight = adjustmentLeft + adjustmentWidth + ColumnGap;
            SetTwoColumnRow(TransformationCommand.DecreaseSpiralR1, TransformationCommand.IncreaseSpiralR1,
                adjustmentLeft, adjustmentRight, adjustmentWidth, 238);
            SetTwoColumnRow(TransformationCommand.DecreaseSpiralR2, TransformationCommand.IncreaseSpiralR2,
                adjustmentLeft, adjustmentRight, adjustmentWidth, 267);
            SetTwoColumnRow(TransformationCommand.DecreaseSpiralK, TransformationCommand.IncreaseSpiralK,
                adjustmentLeft, adjustmentRight, adjustmentWidth, 296);
            SetTwoColumnRow(TransformationCommand.DecreaseSpiralSamples, TransformationCommand.IncreaseSpiralSamples,
                adjustmentLeft, adjustmentRight, adjustmentWidth, 325);
            SetBounds(TransformationCommand.RegenerateSpiral,
                new Rectangle(contentLeft, 354, contentWidth, ButtonHeight));
            SetTwoColumnRow(TransformationCommand.PlayCurve, TransformationCommand.ResetCurve,
                contentLeft, right, columnWidth, 383);

            LayoutCommonControls(contentLeft, right, columnWidth, contentWidth,
                rotationY: 462, transformY: 579, systemY: 750);
            SetTwoColumnRow(TransformationCommand.ToggleGrid, TransformationCommand.ToggleAxes,
                contentLeft, right, columnWidth, 814);
            SetTwoColumnRow(TransformationCommand.ToggleCurve, TransformationCommand.ToggleCurvePoints,
                contentLeft, right, columnWidth, 843);
            SetBounds(TransformationCommand.ToggleCurveDirection,
                new Rectangle(contentLeft, 872, contentWidth, ButtonHeight));
        }
        else
        {
            LayoutCommonControls(contentLeft, right, columnWidth, contentWidth,
                rotationY: 238, transformY: 355, systemY: 526);
            SetTwoColumnRow(TransformationCommand.ToggleGrid, TransformationCommand.ToggleAxes,
                contentLeft, right, columnWidth, 590);
            SetTwoColumnRow(TransformationCommand.ToggleCells, TransformationCommand.ToggleEdges,
                contentLeft, right, columnWidth, 619);
            SetBounds(TransformationCommand.ToggleVertices,
                new Rectangle(contentLeft, 648, contentWidth, ButtonHeight));
        }
    }

    private void LayoutCommonControls(
        int left,
        int right,
        int columnWidth,
        int contentWidth,
        int rotationY,
        int transformY,
        int systemY)
    {
        SetTwoColumnRow(TransformationCommand.RotateXY, TransformationCommand.RotateXZ,
            left, right, columnWidth, rotationY);
        SetTwoColumnRow(TransformationCommand.RotateXW, TransformationCommand.RotateYZ,
            left, right, columnWidth, rotationY + 29);
        SetTwoColumnRow(TransformationCommand.RotateYW, TransformationCommand.RotateZW,
            left, right, columnWidth, rotationY + 58);
        SetTwoColumnRow(TransformationCommand.ScaleUp, TransformationCommand.ScaleDown,
            left, right, columnWidth, transformY);
        SetTwoColumnRow(TransformationCommand.MovePositiveX, TransformationCommand.MoveNegativeX,
            left, right, columnWidth, transformY + 29);
        SetTwoColumnRow(TransformationCommand.MovePositiveY, TransformationCommand.MoveNegativeY,
            left, right, columnWidth, transformY + 58);
        SetTwoColumnRow(TransformationCommand.MovePositiveZ, TransformationCommand.MoveNegativeZ,
            left, right, columnWidth, transformY + 87);
        SetTwoColumnRow(TransformationCommand.MovePositiveW, TransformationCommand.MoveNegativeW,
            left, right, columnWidth, transformY + 116);
        SetTwoColumnRow(TransformationCommand.ResetObject, TransformationCommand.ResetCamera,
            left, right, columnWidth, systemY);
    }

    private void SetTwoColumnRow(
        TransformationCommand leftCommand,
        TransformationCommand rightCommand,
        int left,
        int right,
        int width,
        int y)
    {
        SetBounds(leftCommand, new Rectangle(left, y, width, ButtonHeight));
        SetBounds(rightCommand, new Rectangle(right, y, width, ButtonHeight));
    }

    private void SetBounds(TransformationCommand command, Rectangle bounds) =>
        _buttonByCommand[command].SetBounds(bounds);

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
                button.Command == TransformationCommand.PlayCurve)
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
        >= TransformationCommand.SelectTesseract and <= TransformationCommand.SelectSpiral =>
            VisualizationPalette.ObjectInfoAccent,
        >= TransformationCommand.RotateXY and <= TransformationCommand.RotateZW =>
            VisualizationPalette.RotationAccent,
        >= TransformationCommand.ScaleUp and <= TransformationCommand.MoveNegativeW =>
            VisualizationPalette.TransformAccent,
        TransformationCommand.ResetObject or TransformationCommand.ResetCamera =>
            VisualizationPalette.SystemAccent,
        >= TransformationCommand.DecreaseSpiralR1 and <= TransformationCommand.ResetCurve =>
            VisualizationPalette.CurveAccent,
        _ => VisualizationPalette.DisplayAccent
    };

    private static bool IsApplicable(TransformationCommand command, bool isSpiral)
    {
        if (command is TransformationCommand.ToggleCells or
            TransformationCommand.ToggleEdges or
            TransformationCommand.ToggleVertices)
        {
            return !isSpiral;
        }

        if (command >= TransformationCommand.DecreaseSpiralR1)
        {
            return isSpiral;
        }

        return true;
    }

    private static bool IsObjectCommand(TransformationCommand command) =>
        command >= TransformationCommand.SelectTesseract &&
        command <= TransformationCommand.SelectSpiral;

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
            TransformationCommand.ToggleCurveDirection;

    private static bool IsSelectedObject(TransformationCommand command, GeometryVisualStyle4D style) =>
        (command, style) switch
        {
            (TransformationCommand.SelectTesseract, GeometryVisualStyle4D.Tesseract) => true,
            (TransformationCommand.SelectHypersphere, GeometryVisualStyle4D.Hypersphere) => true,
            (TransformationCommand.SelectSimplex, GeometryVisualStyle4D.Simplex) => true,
            (TransformationCommand.SelectIrregular, GeometryVisualStyle4D.Irregular) => true,
            (TransformationCommand.SelectSpiral, GeometryVisualStyle4D.Spiral) => true,
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
