using CommunityToolkit.Mvvm.ComponentModel;

namespace Cpu32Emulator.Presentation;

public partial class DisassemblyLineViewModel : ObservableObject
{
    [ObservableProperty]
    private string address = string.Empty;

    [ObservableProperty]
    private string symbol = string.Empty;

    [ObservableProperty]
    private string instruction = string.Empty;

    [ObservableProperty]
    private bool isCurrentInstruction = false;

    [ObservableProperty]
    private bool hasBreakpoint = false;

    public DisassemblyLineViewModel(string address, string symbol, string instruction)
    {
        Address = address;
        Symbol = symbol;
        Instruction = instruction;
    }

    public void UpdateCurrentInstruction(uint currentPc)
    {
        // Parse address and compare with current PC
        if (uint.TryParse(Address.Replace("0x", ""), System.Globalization.NumberStyles.HexNumber, null, out uint lineAddress))
        {
            IsCurrentInstruction = lineAddress == currentPc;
        }
    }
}
