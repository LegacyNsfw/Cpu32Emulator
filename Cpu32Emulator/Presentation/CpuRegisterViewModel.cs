using CommunityToolkit.Mvvm.ComponentModel;

namespace Cpu32Emulator.Presentation;

public partial class CpuRegisterViewModel : ObservableObject
{
    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private string value = string.Empty;

    [ObservableProperty]
    private bool hasChanged = false;

    private uint _previousValue;
    private uint _currentValue;

    public CpuRegisterViewModel(string name, uint initialValue)
    {
        Name = name;
        _currentValue = initialValue;
        _previousValue = initialValue;
        Value = $"0x{initialValue:X8}";
    }

    public void UpdateValue(uint newValue)
    {
        _previousValue = _currentValue;
        _currentValue = newValue;
        Value = $"0x{newValue:X8}";
        HasChanged = _currentValue != _previousValue;
    }

    public uint GetNumericValue() => _currentValue;

    public void SetNumericValue(uint value)
    {
        UpdateValue(value);
    }
}
