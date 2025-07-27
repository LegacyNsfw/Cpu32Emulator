namespace Cpu32Emulator.Models;

public record AppConfig
{
    public string? Environment { get; init; }
    public string? LastProjectPath { get; init; }
}
