using System;
using System.Collections.Generic;
using HyperSpace.Geometry;
using HyperSpace.Rendering;
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
    public const int PreferredWidth = 300;

    private const int Padding = 10;
    private const int InnerPadding = 8;
    private const int ColumnGap = 6;
    private const int ButtonHeight = 23;

    private readonly SpriteBatch _spriteBatch;
    private readonly SpriteFont _font;
    private readonly Texture2D _pixel;
    private readonly List<UiButton> _buttons;

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
            new("SHOW CELLS", TransformationCommand.ToggleCells),
            new("SHOW EDGES", TransformationCommand.ToggleEdges),
            new("SHOW VERTICES", TransformationCommand.ToggleVertices)
        ];
    }

    public Rectangle Bounds { get; private set; }

    public bool Contains(Point point) => Bounds.Contains(point);

    public TransformationCommand? Update(
        MouseState mouse,
        bool isGameActive,
        int viewportWidth,
        int viewportHeight,
        bool isAnimationActive)
    {
        Layout(viewportWidth, viewportHeight);

        var previousMouse = _hasPreviousMouse
            ? _previousMouse
            : ReleasedMouseAt(mouse.X, mouse.Y, mouse.ScrollWheelValue);
        TransformationCommand? requestedCommand = null;

        foreach (var button in _buttons)
        {
            var isEnabled = isGameActive &&
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
        DisplayOptions displayOptions)
    {
        _activeAnimationCommand = animationCommand;

        foreach (var button in _buttons)
        {
            button.SetActive(button.Command == animationCommand ||
                IsEnabledDisplayToggle(button.Command, displayOptions));
        }
    }

    public void Draw(
        int viewportWidth,
        int viewportHeight,
        TransformationAnimator4D animator,
        DisplayOptions displayOptions,
        IReadOnlyList<TesseractCell4D> cells)
    {
        Layout(viewportWidth, viewportHeight);

        _spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.NonPremultiplied,
            SamplerState.PointClamp,
            DepthStencilState.None,
            RasterizerState.CullNone);

        _spriteBatch.Draw(_pixel, Bounds, new Color(12, 17, 30));
        _spriteBatch.Draw(
            _pixel,
            new Rectangle(Bounds.X, 0, 2, Bounds.Height),
            new Color(65, 91, 135));

        DrawLabel("4D TESSERACT EXPLORER", Bounds.X + Padding, 7, new Color(225, 235, 255));
        DrawLabel(
            animator.IsActive ? $"Active: {animator.ActiveLabel}" : "Active: Ready",
            Bounds.X + Padding,
            26,
            animator.IsActive ? new Color(255, 207, 92) : new Color(122, 193, 164));

        var detail = animator.ActiveRotationPlane.HasValue
            ? $"Angle: {animator.CurrentRotationDegrees:0.0} / 90 deg"
            : animator.IsActive
                ? $"Progress: {animator.Progress * 100.0:0}%"
                : "One animation at a time";
        DrawLabel(detail, Bounds.X + Padding, 43, new Color(153, 171, 205));

        DrawGroup(new Rectangle(Bounds.X + Padding, 63, Bounds.Width - (2 * Padding), 111),
            "ROTATIONS", VisualizationPalette.RotationAccent);
        DrawGroup(new Rectangle(Bounds.X + Padding, 180, Bounds.Width - (2 * Padding), 165),
            "TRANSFORMS", VisualizationPalette.TransformAccent);
        DrawGroup(new Rectangle(Bounds.X + Padding, 351, Bounds.Width - (2 * Padding), 58),
            "SYSTEM", VisualizationPalette.SystemAccent);
        DrawGroup(new Rectangle(Bounds.X + Padding, 415, Bounds.Width - (2 * Padding), 297),
            "DISPLAY", VisualizationPalette.DisplayAccent);

        foreach (var button in _buttons)
        {
            DrawButton(button);
        }

        DrawLegend(cells, displayOptions);
        _spriteBatch.End();
    }

    public void Dispose()
    {
        _pixel.Dispose();
        _spriteBatch.Dispose();
    }

    private void Layout(int viewportWidth, int viewportHeight)
    {
        var width = Math.Min(PreferredWidth, Math.Max(1, viewportWidth));
        Bounds = new Rectangle(viewportWidth - width, 0, width, viewportHeight);

        var contentLeft = Bounds.X + Padding + InnerPadding;
        var contentWidth = Math.Max(2, width - (2 * (Padding + InnerPadding)));
        var columnWidth = Math.Max(1, (contentWidth - ColumnGap) / 2);
        var right = contentLeft + columnWidth + ColumnGap;

        SetTwoColumnRow(0, 1, contentLeft, right, columnWidth, 88);
        SetTwoColumnRow(2, 3, contentLeft, right, columnWidth, 117);
        SetTwoColumnRow(4, 5, contentLeft, right, columnWidth, 146);
        SetTwoColumnRow(6, 7, contentLeft, right, columnWidth, 204);
        SetTwoColumnRow(8, 9, contentLeft, right, columnWidth, 233);
        SetTwoColumnRow(10, 11, contentLeft, right, columnWidth, 262);
        SetTwoColumnRow(12, 13, contentLeft, right, columnWidth, 291);
        SetTwoColumnRow(14, 15, contentLeft, right, columnWidth, 320);
        SetTwoColumnRow(16, 17, contentLeft, right, columnWidth, 378);
        SetTwoColumnRow(18, 19, contentLeft, right, columnWidth, 439);
        SetTwoColumnRow(20, 21, contentLeft, right, columnWidth, 468);
        _buttons[22].SetBounds(new Rectangle(contentLeft, 497, contentWidth, ButtonHeight));
    }

    private void SetTwoColumnRow(
        int leftIndex,
        int rightIndex,
        int left,
        int right,
        int columnWidth,
        int y)
    {
        _buttons[leftIndex].SetBounds(new Rectangle(left, y, columnWidth, ButtonHeight));
        _buttons[rightIndex].SetBounds(new Rectangle(right, y, columnWidth, ButtonHeight));
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
        var isDisplayToggle = IsDisplayCommand(button.Command);
        var isCurrentAnimation = button.Command == _activeAnimationCommand;
        var fill = !button.IsEnabled
            ? new Color(33, 39, 53)
            : button.IsActive && isDisplayToggle
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
            : button.IsHovered || button.IsActive
                ? accent
                : new Color(61, 81, 112);
        var textColor = button.IsEnabled
            ? new Color(225, 235, 255)
            : new Color(92, 101, 120);

        _spriteBatch.Draw(_pixel, button.Bounds, border);
        var inner = new Rectangle(
            button.Bounds.X + 1,
            button.Bounds.Y + 1,
            Math.Max(0, button.Bounds.Width - 2),
            Math.Max(0, button.Bounds.Height - 2));
        _spriteBatch.Draw(_pixel, inner, fill);

        var label = isDisplayToggle
            ? $"{(button.IsActive ? "ON " : "OFF")}  {button.Label[5..]}"
            : button.Label;
        var size = _font.MeasureString(label);
        var position = new Vector2(
            button.Bounds.Center.X - (size.X / 2.0f),
            button.Bounds.Center.Y - (size.Y / 2.0f));
        _spriteBatch.DrawString(_font, label, position, textColor);
    }

    private void DrawLegend(
        IReadOnlyList<TesseractCell4D> cells,
        DisplayOptions displayOptions)
    {
        var left = Bounds.X + Padding + InnerPadding;
        var halfWidth = (Bounds.Width - (2 * (Padding + InnerPadding))) / 2;

        DrawLabel("CELLS", left, 528,
            displayOptions.ShowCells ? VisualizationPalette.DisplayAccent : new Color(92, 101, 120));
        for (var index = 0; index < cells.Count; index++)
        {
            var column = index % 2;
            var row = index / 2;
            DrawLegendEntry(
                left + (column * halfWidth),
                547 + (row * 18),
                VisualizationPalette.CellColor(index),
                $"{cells[index].Label} cell");
        }

        DrawLabel("EDGES", left, 621,
            displayOptions.ShowEdges ? VisualizationPalette.DisplayAccent : new Color(92, 101, 120));
        var edgeColumnWidth = Math.Max(1, (Bounds.Width - (2 * (Padding + InnerPadding))) / 4);
        DrawCompactLegendEntry(left, 640, VisualizationPalette.EdgeX, "X", edgeColumnWidth);
        DrawCompactLegendEntry(left + edgeColumnWidth, 640, VisualizationPalette.EdgeY, "Y", edgeColumnWidth);
        DrawCompactLegendEntry(left + (2 * edgeColumnWidth), 640, VisualizationPalette.EdgeZ, "Z", edgeColumnWidth);
        DrawCompactLegendEntry(left + (3 * edgeColumnWidth), 640, VisualizationPalette.EdgeW, "W", edgeColumnWidth);

        DrawLabel("VERTICES", left, 657,
            displayOptions.ShowVertices ? VisualizationPalette.DisplayAccent : new Color(92, 101, 120));
        DrawLegendEntry(left, 676, VisualizationPalette.VertexNegativeW, "W-");
        DrawLegendEntry(left + halfWidth, 676, VisualizationPalette.VertexPositiveW, "W+");
        DrawLabel(
            $"Cell alpha {VisualizationPalette.CellSurfaceAlpha:0.00}",
            left,
            694,
            new Color(112, 128, 155));
    }

    private void DrawLegendEntry(int x, int y, Color color, string label)
    {
        _spriteBatch.Draw(_pixel, new Rectangle(x, y + 2, 11, 11), color);
        DrawLabel(label, x + 17, y, new Color(190, 203, 225));
    }

    private void DrawCompactLegendEntry(int x, int y, Color color, string label, int width)
    {
        _spriteBatch.Draw(_pixel, new Rectangle(x, y + 2, 11, 11), color);
        DrawLabel(label, x + Math.Min(17, Math.Max(12, width - 8)), y, new Color(190, 203, 225));
    }

    private void DrawLabel(string text, int x, int y, Color color)
    {
        _spriteBatch.DrawString(_font, text, new Vector2(x, y), color);
    }

    private static Color AccentFor(TransformationCommand command) =>
        command switch
        {
            >= TransformationCommand.RotateXY and <= TransformationCommand.RotateZW =>
                VisualizationPalette.RotationAccent,
            >= TransformationCommand.ScaleUp and <= TransformationCommand.MoveNegativeW =>
                VisualizationPalette.TransformAccent,
            TransformationCommand.ResetObject or TransformationCommand.ResetCamera =>
                VisualizationPalette.SystemAccent,
            _ => VisualizationPalette.DisplayAccent
        };

    private static bool IsAnimationCommand(TransformationCommand command) =>
        command >= TransformationCommand.RotateXY &&
        command <= TransformationCommand.MoveNegativeW;

    private static bool IsDisplayCommand(TransformationCommand command) =>
        command >= TransformationCommand.ToggleGrid;

    private static bool IsEnabledDisplayToggle(
        TransformationCommand command,
        DisplayOptions options) =>
        command switch
        {
            TransformationCommand.ToggleGrid => options.ShowGrid,
            TransformationCommand.ToggleAxes => options.ShowAxes,
            TransformationCommand.ToggleCells => options.ShowCells,
            TransformationCommand.ToggleEdges => options.ShowEdges,
            TransformationCommand.ToggleVertices => options.ShowVertices,
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
