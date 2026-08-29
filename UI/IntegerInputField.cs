using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace HyperSpace.UI;

/// <summary>Minimal signed-integer field; validation remains with the owning settings.</summary>
public sealed class IntegerInputField
{
    public IntegerInputField(string initialText) => Text = initialText;

    public string Text { get; private set; }
    public Rectangle Bounds { get; private set; }
    public bool IsFocused { get; private set; }

    public void SetBounds(Rectangle bounds) => Bounds = bounds;
    public void SetText(string text) => Text = text;

    public bool Update(
        MouseState mouse,
        MouseState previousMouse,
        KeyboardState keyboard,
        KeyboardState previousKeyboard,
        bool enabled)
    {
        var clicked = mouse.LeftButton == ButtonState.Released &&
            previousMouse.LeftButton == ButtonState.Pressed;
        if (clicked)
        {
            IsFocused = enabled && Bounds.Contains(mouse.Position);
        }

        if (!enabled || !IsFocused)
        {
            return false;
        }

        foreach (var key in keyboard.GetPressedKeys())
        {
            if (previousKeyboard.IsKeyDown(key))
            {
                continue;
            }

            if (key == Keys.Enter)
            {
                IsFocused = false;
                return true;
            }

            if (key == Keys.Escape)
            {
                IsFocused = false;
                return false;
            }

            if (key == Keys.Back)
            {
                if (Text.Length > 0)
                {
                    Text = Text[..^1];
                }
                continue;
            }

            if ((key is Keys.OemMinus or Keys.Subtract) && Text.Length == 0)
            {
                Text = "-";
                continue;
            }

            var digit = DigitFor(key);
            if (digit.HasValue && Text.Length < 11)
            {
                Text += digit.Value;
            }
        }

        return false;
    }

    private static char? DigitFor(Keys key)
    {
        if (key >= Keys.D0 && key <= Keys.D9)
        {
            return (char)('0' + (key - Keys.D0));
        }

        if (key >= Keys.NumPad0 && key <= Keys.NumPad9)
        {
            return (char)('0' + (key - Keys.NumPad0));
        }

        return null;
    }
}
