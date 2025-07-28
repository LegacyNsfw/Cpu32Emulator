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
            AutoSelectTrailingZeros(textBox);
        };
        
        // Also handle when the TextBox gets focus
        textBox.GotFocus += (sender, e) =>
        {
            AutoSelectTrailingZeros(textBox);
        };
    }
    
    /// <summary>
    /// Automatically selects trailing zeros in a hex string (after 0x prefix)
    /// </summary>
    /// <param name="textBox">The TextBox containing the hex value</param>
    private static void AutoSelectTrailingZeros(TextBox textBox)
    {
        try
        {
            var text = textBox.Text;
            if (string.IsNullOrEmpty(text))
                return;
            
            // Find the pattern: 0x followed by zeros
            var match = Regex.Match(text, @"^(0x)?(0+)");
            if (match.Success)
            {
                var prefix = match.Groups[1].Success ? match.Groups[1].Value : "";
                var zeros = match.Groups[2].Value;
                
                if (zeros.Length > 0)
                {
                    var startIndex = prefix.Length;
                    var length = zeros.Length;
                    
                    // Select the trailing zeros
                    textBox.Select(startIndex, length);
                }
            }
            else
            {
                // If no 0x prefix but starts with zeros, select all leading zeros
                var zeroMatch = Regex.Match(text, @"^(0+)");
                if (zeroMatch.Success && zeroMatch.Groups[1].Value.Length > 0)
                {
                    textBox.Select(0, zeroMatch.Groups[1].Value.Length);
                }
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