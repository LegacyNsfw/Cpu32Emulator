using Microsoft.UI.Xaml.Controls;
using System;
using System.Text.RegularExpressions;

namespace Cpu32Emulator.Presentation.Helpers;

/// <summary>
/// Helper class for managing hex value TextBox controls in dialogs
/// </summary>
public static class HexTextBoxHelper
{
    /// <summary>
    /// Sets up a TextBox for hex input with auto-selection of trailing zeros
    /// </summary>
    /// <param name="textBox">The TextBox to configure</param>
    /// <param name="initialValue">The initial hex value to display</param>
    public static void ConfigureHexTextBox(TextBox textBox, string initialValue)
    {
        textBox.Text = initialValue;
        
        // Set up the auto-selection behavior when the dialog loads
        textBox.Loaded += (sender, e) =>
        {
            AutoSelectHexDigits(textBox);
        };
        
        // Also handle when the TextBox gets focus
        textBox.GotFocus += (sender, e) =>
        {
            AutoSelectHexDigits(textBox);
        };
    }
    
    /// <summary>
    /// Automatically selects all hex digits after the "0x" prefix
    /// </summary>
    /// <param name="textBox">The TextBox containing the hex value</param>
    private static void AutoSelectHexDigits(TextBox textBox)
    {
        try
        {
            var text = textBox.Text;
            if (string.IsNullOrEmpty(text))
                return;
            
            // Check if text starts with "0x" or "0X"
            if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                // Select everything after "0x"
                var startIndex = 2; // Length of "0x"
                var length = text.Length - startIndex;
                
                if (length > 0)
                {
                    textBox.Select(startIndex, length);
                }
            }
            else
            {
                // If no "0x" prefix, select all text
                textBox.SelectAll();
            }
        }
        catch (Exception)
        {
            // If anything goes wrong, just select all text as fallback
            textBox.SelectAll();
        }
    }
    
    /// <summary>
    /// Creates a configured TextBox for hex input with the specified initial value
    /// </summary>
    /// <param name="initialValue">The initial hex value</param>
    /// <param name="placeholderText">Placeholder text for the TextBox</param>
    /// <returns>A configured TextBox</returns>
    public static TextBox CreateHexTextBox(string initialValue, string placeholderText = "Enter hex value")
    {
        var textBox = new TextBox
        {
            PlaceholderText = placeholderText
        };
        
        ConfigureHexTextBox(textBox, initialValue);
        return textBox;
    }
    
    /// <summary>
    /// Creates a configured TextBox for register value editing
    /// </summary>
    /// <param name="registerValue">The current register value</param>
    /// <returns>A configured TextBox</returns>
    public static TextBox CreateRegisterTextBox(string registerValue)
    {
        return CreateHexTextBox(registerValue, "Enter hex value (e.g., 0x12345678)");
    }
    
    /// <summary>
    /// Creates a configured TextBox for memory address editing
    /// </summary>
    /// <param name="address">The current address value</param>
    /// <returns>A configured TextBox</returns>
    public static TextBox CreateAddressTextBox(uint address)
    {
        var addressText = $"0x{address:X8}";
        return CreateHexTextBox(addressText, "Enter hex address (e.g., 0x00001000)");
    }
    
    /// <summary>
    /// Creates a configured TextBox for memory value editing
    /// </summary>
    /// <param name="value">The current memory value</param>
    /// <param name="width">The memory watch width for placeholder text</param>
    /// <returns>A configured TextBox</returns>
    public static TextBox CreateMemoryValueTextBox(string value, string width)
    {
        return CreateHexTextBox(value, $"Enter hex value ({width})");
    }
}