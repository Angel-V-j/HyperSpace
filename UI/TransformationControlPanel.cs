using System;
using System.Collections.Generic;
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
    public const int PreferredWidth = 250;

    private const int Padding = 12;
    private const int ColumnGap = 6;
    private const int ButtonHeight = 26;

    private readonly SpriteBatch _spriteBatch;
    private readonly SpriteFont _font;
    private readonly Texture2D _pixel;
    private readonly List<UiButton> _buttons;

    private MouseState _previousMouse;
    private bool _hasPreviousMouse;

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
            new("RESET CAMERA", TransformationCommand.ResetCamera)
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
            var isReset = button.Command is
                TransformationCommand.ResetObject or TransformationCommand.ResetCamera;
            var isEnabled = isGameActive && (!isAnimationActive || isReset);

            if (button.Update(mouse, previousMouse, isEnabled) && requestedCommand is null)
            {
                requestedCommand = button.Command;
            }
        }

        _previousMouse = mouse;
        _hasPreviousMouse = isGameActive;
        return requestedCommand;
    }

    public void SetActiveCommand(TransformationCommand? command)
    {
        foreach (var button in _buttons)
        {
            button.SetActive(command == button.Command);
        }
    }

    public void Draw(
        int viewportWidth,
        int viewportHeight,
        TransformationAnimator4D animator)
    {
        Layout(viewportWidth, viewportHeight);

        _spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.AlphaBlend,
            SamplerState.PointClamp,
            DepthStencilState.None,
            RasterizerState.CullNone);

        _spriteBatch.Draw(_pixel, Bounds, new Color(14, 19, 35));
        _spriteBatch.Draw(_pixel, new Rectangle(Bounds.X, 0, 2, Bounds.Height), new Color(65, 91, 135));

        DrawLabel("4D TRANSFORMS", Bounds.X + Padding, 8, new Color(225, 235, 255));
        DrawLabel(
            animator.IsActive ? $"Active: {animator.ActiveLabel}" : "Active: Ready",
            Bounds.X + Padding,
            27,
            animator.IsActive ? new Color(255, 207, 92) : new Color(122, 193, 164));

        var detail = animator.ActiveRotationPlane.HasValue
            ? $"Angle: {animator.CurrentRotationDegrees:0.0} / 90 deg"
            : animator.IsActive
                ? $"Progress: {animator.Progress * 100.0:0}%"
                : "One animation at a time";
        DrawLabel(detail, Bounds.X + Padding, 44, new Color(153, 171, 205));

        DrawLabel("ROTATION", Bounds.X + Padding, 64, new Color(93, 205, 255));
        DrawLabel("TRANSFORM", Bounds.X + Padding, 180, new Color(93, 205, 255));
        DrawLabel("SYSTEM", Bounds.X + Padding, 358, new Color(93, 205, 255));

        foreach (var button in _buttons)
        {
            DrawButton(button);
        }

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

        var contentWidth = Math.Max(2, width - (2 * Padding));
        var columnWidth = Math.Max(1, (contentWidth - ColumnGap) / 2);
        var left = Bounds.X + Padding;
        var right = left + columnWidth + ColumnGap;

        SetTwoColumnRow(0, 1, left, right, columnWidth, 82);
        SetTwoColumnRow(2, 3, left, right, columnWidth, 113);
        SetTwoColumnRow(4, 5, left, right, columnWidth, 144);
        SetTwoColumnRow(6, 7, left, right, columnWidth, 198);
        SetTwoColumnRow(8, 9, left, right, columnWidth, 229);
        SetTwoColumnRow(10, 11, left, right, columnWidth, 260);
        SetTwoColumnRow(12, 13, left, right, columnWidth, 291);
        SetTwoColumnRow(14, 15, left, right, columnWidth, 322);
        _buttons[16].SetBounds(new Rectangle(left, 376, contentWidth, ButtonHeight));
        _buttons[17].SetBounds(new Rectangle(left, 407, contentWidth, ButtonHeight));
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

    private void DrawButton(UiButton button)
    {
        var fill = !button.IsEnabled
            ? new Color(35, 41, 57)
            : button.IsActive
                ? new Color(104, 72, 173)
                : button.IsPressed
                    ? new Color(56, 117, 160)
                    : button.IsHovered
                        ? new Color(43, 87, 126)
                        : new Color(28, 47, 72);
        var border = button.IsActive
            ? new Color(255, 207, 92)
            : button.IsHovered
                ? new Color(93, 205, 255)
                : new Color(65, 91, 135);
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

        var size = _font.MeasureString(button.Label);
        var position = new Vector2(
            button.Bounds.Center.X - (size.X / 2.0f),
            button.Bounds.Center.Y - (size.Y / 2.0f));
        _spriteBatch.DrawString(_font, button.Label, position, textColor);
    }

    private void DrawLabel(string text, int x, int y, Color color)
    {
        _spriteBatch.DrawString(_font, text, new Vector2(x, y), color);
    }

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
