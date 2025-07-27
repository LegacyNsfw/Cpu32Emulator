using CommunityToolkit.Mvvm.ComponentModel;
using Cpu32Emulator.Services;

namespace Cpu32Emulator.Presentation;

public enum MemoryWatchWidth
{
    Byte = 1,
    Word = 2,
    Long = 4
}

public partial class MemoryWatchViewModel : ObservableObject
{
    [ObservableProperty]
    private string address = string.Empty;

    [ObservableProperty]
    private string value = string.Empty;

    [ObservableProperty]
    private string width = string.Empty;

    [ObservableProperty]
    private bool canWrite = false;

    [ObservableProperty]
    private bool isSpecialAddress = false;

    private uint _numericAddress;
    private MemoryWatchWidth _watchWidth;

    public MemoryWatchViewModel(string displayAddress, uint numericAddress, MemoryWatchWidth watchWidth, bool isSpecial = false)
    {
        Address = displayAddress;
        _numericAddress = numericAddress;
        _watchWidth = watchWidth;
        Width = _watchWidth switch
        {
            MemoryWatchWidth.Byte => "BYTE",
            MemoryWatchWidth.Word => "WORD",
            MemoryWatchWidth.Long => "LONG",
            _ => "BYTE"
        };
        IsSpecialAddress = isSpecial;
        Value = "0x00000000";
    }

    public uint GetNumericAddress() => _numericAddress;
    public MemoryWatchWidth GetWidth() => _watchWidth;

    public void SetAddress(uint newAddress)
    {
        _numericAddress = newAddress;
        Address = IsSpecialAddress ? Address : $"0x{newAddress:X8}";
    }

    public void SetWidth(MemoryWatchWidth newWidth)
    {
        _watchWidth = newWidth;
        Width = _watchWidth switch
        {
            MemoryWatchWidth.Byte => "BYTE",
            MemoryWatchWidth.Word => "WORD",
            MemoryWatchWidth.Long => "LONG",
            _ => "BYTE"
        };
    }

    public void RefreshValue(MemoryManagerService memoryManager)
    {
        try
        {
            if (!memoryManager.IsAddressMapped(_numericAddress))
            {
                Value = "UNMAPPED";
                CanWrite = false;
                return;
            }

            CanWrite = memoryManager.CanWrite(_numericAddress);

            switch (_watchWidth)
            {
                case MemoryWatchWidth.Byte:
                    var byteValue = memoryManager.ReadByte(_numericAddress);
                    Value = $"0x{byteValue:X2}";
                    break;
                case MemoryWatchWidth.Word:
                    var wordValue = memoryManager.ReadWord(_numericAddress);
                    Value = $"0x{wordValue:X4}";
                    break;
                case MemoryWatchWidth.Long:
                    var longValue = memoryManager.ReadLong(_numericAddress);
                    Value = $"0x{longValue:X8}";
                    break;
            }
        }
        catch (Exception)
        {
            Value = "ERROR";
            CanWrite = false;
        }
    }

    public bool TrySetValue(string valueText, MemoryManagerService memoryManager)
    {
        try
        {
            if (!CanWrite) return false;

            // Parse hex or decimal value
            uint numericValue;
            if (valueText.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                if (!uint.TryParse(valueText[2..], System.Globalization.NumberStyles.HexNumber, null, out numericValue))
                    return false;
            }
            else
            {
                if (!uint.TryParse(valueText, out numericValue))
                    return false;
            }

            switch (_watchWidth)
            {
                case MemoryWatchWidth.Byte:
                    if (numericValue > 0xFF) return false;
                    memoryManager.WriteByte(_numericAddress, (byte)numericValue);
                    break;
                case MemoryWatchWidth.Word:
                    if (numericValue > 0xFFFF) return false;
                    memoryManager.WriteWord(_numericAddress, (ushort)numericValue);
                    break;
                case MemoryWatchWidth.Long:
                    memoryManager.WriteLong(_numericAddress, numericValue);
                    break;
            }

            RefreshValue(memoryManager);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
