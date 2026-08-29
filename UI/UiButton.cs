using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace HyperSpace.UI;

/// <summary>
/// Minimal immediate-mode button state: rectangle, hover, press, click and active.
/// A click is completed only when the left button is released over the same button.
/// </summary>
public sealed class UiButton
{
    public UiButton(string label, TransformationCommand command)
    {
        Label = label;
        Command = command;
    }

    public string Label { get; private set; }

    public TransformationCommand Command { get; }

    public Rectangle Bounds { get; private set; }

    public bool IsEnabled { get; private set; } = true;

    public bool IsHovered { get; private set; }

    public bool IsPressed { get; private set; }

    public bool IsActive { get; private set; }

    public void SetBounds(Rectangle bounds)
    {
        Bounds = bounds;
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
    }

    public void SetLabel(string label)
    {
        Label = label;
    }

    public bool Update(MouseState mouse, MouseState previousMouse, bool isEnabled)
    {
        IsEnabled = isEnabled;
        IsHovered = isEnabled && Bounds.Contains(mouse.X, mouse.Y);
        IsPressed = IsHovered && mouse.LeftButton == ButtonState.Pressed;

        return IsHovered &&
            mouse.LeftButton == ButtonState.Released &&
            previousMouse.LeftButton == ButtonState.Pressed &&
            Bounds.Contains(previousMouse.X, previousMouse.Y);
    }
}
