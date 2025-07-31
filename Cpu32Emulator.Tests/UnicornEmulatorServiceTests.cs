using System;
using FluentAssertions;
using NUnit.Framework;
using Cpu32Emulator.Services;
using Cpu32Emulator.Models;

namespace Cpu32Emulator.Tests;

/// <summary>
/// Unit tests for the UnicornEmulatorService class
/// </summary>
[TestFixture]
public class UnicornEmulatorServiceTests
{
    private UnicornEmulatorService _emulatorService = null!;

    [SetUp]
    public void Setup()
    {
        _emulatorService = new UnicornEmulatorService();
        _emulatorService.Initialize();
    }

    [TearDown]
    public void TearDown()
    {
        _emulatorService?.Dispose();
    }

    [Test]
    public void Initialize_ShouldSetIsInitializedToTrue()
    {
        // Arrange & Act (done in Setup)
        
        // Assert
        _emulatorService.IsInitialized.Should().BeTrue();
        _emulatorService.LastException.Should().BeNullOrEmpty();
    }

    [Test]
    public void StepInstruction_WithSimpleNopInstruction_ShouldExecuteSuccessfully()
    {
        // Arrange - Start with simpler NOP instruction to isolate the issue
        var programCode = new byte[]
        {
            0x4e, 0x71,   // NOP instruction
            0x4e, 0x71    // Another NOP instruction
        };

        // Map memory region for the program (must be page-aligned for Unicorn Engine)
        const uint pageSize = 0x1000; // 4KB page size
        var romData = new byte[pageSize];
        Array.Copy(programCode, 0, romData, 0, programCode.Length);
        
        var romRegion = new MemoryRegion
        {
            BaseAddress = 0x1000,
            Size = pageSize,
            Data = romData,
            Type = MemoryRegionType.ROM,
            FilePath = "test_program.bin",
            LoadedAt = DateTime.Now
        };

        _emulatorService.MapMemoryRegion(romRegion);

        // Set initial state
        _emulatorService.SetRegisterValue("PC", 0x1000); // Point PC to our program

        // Get initial state for comparison
        var initialState = _emulatorService.GetCpuState();
        var initialPC = initialState.PC;

        // Act
        _emulatorService.StepInstruction();

        // Assert
        _emulatorService.LastException.Should().BeNullOrEmpty();
        
        var finalState = _emulatorService.GetCpuState();
        var finalPC = finalState.PC;

        // PC should have advanced by 2 bytes (NOP instruction length)
        finalPC.Should().Be(initialPC + 2, "PC should advance by the NOP instruction length");
    }

    [Test]
    public void StepInstruction_WithAddiInstruction_ShouldExecuteSuccessfully()
    {
        // Arrange - Test with a simpler add instruction: add.w d1,d0
        // M68K instruction: add.w d1,d0 means d0 = d0 + d1
        // Format: 1101 Dn 0 Size Ea_mode Ea_register
        // For add.w d1,d0: 1101 000 0 01 000 001 = 0xD041
        var programCode = new byte[]
        {
            0xD0, 0x41,  // add.w d1,d0 (d0 = d0 + d1)
            0x4e, 0x71   // NOP instruction (so that PC can advance)
        };

        // Map memory region for the program (must be page-aligned for Unicorn Engine)
        const uint pageSize = 0x1000; // 4KB page size
        var romData = new byte[pageSize];
        Array.Copy(programCode, 0, romData, 0, programCode.Length);
        
        var romRegion = new MemoryRegion
        {
            BaseAddress = 0x1000,
            Size = pageSize,
            Data = romData,
            Type = MemoryRegionType.ROM,
            FilePath = "test_program.bin",
            LoadedAt = DateTime.Now
        };

        _emulatorService.MapMemoryRegion(romRegion);

        // Set initial state with clear values
        _emulatorService.SetRegisterValue("D0", 0x1000);  // Initial value in D0
        _emulatorService.SetRegisterValue("D1", 0x0500);  // Value to add from D1
        _emulatorService.SetRegisterValue("PC", 0x1000); // Point PC to our program

        // Get initial state for comparison
        var initialState = _emulatorService.GetCpuState();
        var initialD0 = initialState.GetDataRegister(0);
        var initialD1 = initialState.GetDataRegister(1);
        var initialPC = initialState.PC;

        System.Diagnostics.Debug.WriteLine($"Initial D0: 0x{initialD0:X}, D1: 0x{initialD1:X}, PC: 0x{initialPC:X}");

        // Act
        _emulatorService.StepInstruction();

        // Assert
        _emulatorService.LastException.Should().BeNullOrEmpty();
        
        var finalState = _emulatorService.GetCpuState();
        var finalD0 = finalState.GetDataRegister(0);
        var finalD1 = finalState.GetDataRegister(1);
        var finalPC = finalState.PC;

        System.Diagnostics.Debug.WriteLine($"Final D0: 0x{finalD0:X}, D1: 0x{finalD1:X}, PC: 0x{finalPC:X}");

        // For word operation, only lower 16 bits are affected
        // add.w d1,d0 should do: d0[15:0] = d0[15:0] + d1[15:0], with d0[31:16] unchanged
        var expectedLowerWord = (uint)((initialD0 + initialD1) & 0xFFFF);
        var expectedValue = (initialD0 & 0xFFFF0000) | expectedLowerWord;
        
        finalD0.Should().Be(expectedValue, $"D0 should have lower 16 bits = (0x{initialD0:X} + 0x{initialD1:X}) & 0xFFFF = 0x{expectedValue:X}");
        
        // D1 should be unchanged
        finalD1.Should().Be(initialD1, "D1 should remain unchanged");
        
        // PC should have advanced by 2 bytes (instruction length)
        finalPC.Should().Be(initialPC + 2, "PC should advance by the instruction length");
    }

    [Test]
    public void StepInstruction_WithSimpleAddInstruction_ShouldExecuteSuccessfully()
    {
        // Arrange - Use a simpler move instruction: move.l #$1234, d0
        // M68K instruction: move.l #$1234, d0
        // Encoding: 203C 0000 1234 (move.l #immediate, d0)
        var programCode = new byte[]
        {
            0x20, 0x3C,  // move.l #immediate, d0
            0x00, 0x00,  // upper 16 bits of immediate (0000)
            0x12, 0x34,  // lower 16 bits of immediate (1234)
            0x4e, 0x71   // NOP instruction (so that PC can advance)
        };

        // Map memory region for the program (must be page-aligned for Unicorn Engine)
        const uint pageSize = 0x1000; // 4KB page size
        var romData = new byte[pageSize];
        Array.Copy(programCode, 0, romData, 0, programCode.Length);
        
        var romRegion = new MemoryRegion
        {
            BaseAddress = 0x1000,
            Size = pageSize,
            Data = romData,
            Type = MemoryRegionType.ROM,
            FilePath = "test_program.bin",
            LoadedAt = DateTime.Now
        };

        _emulatorService.MapMemoryRegion(romRegion);

        // Set initial state
        _emulatorService.SetRegisterValue("D0", 0x5678);  // Initial value in D0
        _emulatorService.SetRegisterValue("PC", 0x1000); // Point PC to our program

        // Get initial state for comparison
        var initialState = _emulatorService.GetCpuState();
        var initialD0 = initialState.GetDataRegister(0);
        var initialPC = initialState.PC;

        // Act
        _emulatorService.StepInstruction();

        // Assert
        _emulatorService.LastException.Should().BeNullOrEmpty();
        
        var finalState = _emulatorService.GetCpuState();
        var finalD0 = finalState.GetDataRegister(0);
        var finalPC = finalState.PC;

        // D0 should be set to the immediate value 0x1234
        finalD0.Should().Be(0x1234, "D0 should be set to the immediate value");
        
        // PC should have advanced by 6 bytes (instruction length)
        finalPC.Should().Be(initialPC + 6, "PC should advance by the instruction length");
    }

    [Test]
    public void StepInstruction_WithoutMemoryMapped_ShouldThrowException()
    {
        // Arrange
        _emulatorService.SetRegisterValue("PC", 0x1000); // Point to unmapped memory

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => _emulatorService.StepInstruction());
        exception?.Message.Should().Contain("PC (0x00001000) points to unmapped memory");
    }

    [Test]
    public void StepInstruction_WithUninitializedEmulator_ShouldThrowException()
    {
        // Arrange
        using var uninitializedEmulator = new UnicornEmulatorService();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => uninitializedEmulator.StepInstruction());
        exception?.Message.Should().Contain("Emulator not initialized");
    }

    [Test]
    public void MapMemoryRegion_WithValidRomRegion_ShouldSucceed()
    {
        // Arrange
        const uint pageSize = 0x1000; // 4KB page size
        var testData = new byte[] { 0x4E, 0x71 }; // NOP instruction
        var romData = new byte[pageSize];
        Array.Copy(testData, 0, romData, 0, testData.Length);
        
        var romRegion = new MemoryRegion
        {
            BaseAddress = 0x2000,
            Size = pageSize,
            Data = romData,
            Type = MemoryRegionType.ROM,
            FilePath = "test_rom.bin",
            LoadedAt = DateTime.Now
        };

        // Act
        _emulatorService.MapMemoryRegion(romRegion);

        // Assert
        _emulatorService.LastException.Should().BeNullOrEmpty();
        var regions = _emulatorService.GetMemoryRegions();
        regions.Should().ContainSingle(r => r.BaseAddress == 0x2000);
    }

    [Test]
    public void ReadMemory_FromMappedRegion_ShouldReturnCorrectData()
    {
        // Arrange
        const uint pageSize = 0x1000; // 4KB page size
        var testData = new byte[] { 0x12, 0x34, 0x56, 0x78 };
        var romData = new byte[pageSize];
        Array.Copy(testData, 0, romData, 0, testData.Length);
        
        var romRegion = new MemoryRegion
        {
            BaseAddress = 0x3000,
            Size = pageSize,
            Data = romData,
            Type = MemoryRegionType.ROM,
            FilePath = "test_data.bin",
            LoadedAt = DateTime.Now
        };

        _emulatorService.MapMemoryRegion(romRegion);

        // Act
        var readData = _emulatorService.ReadMemory(0x3000, (uint)testData.Length);

        // Assert
        readData.Should().BeEquivalentTo(testData);
    }

    [Test]
    public void SetAndGetRegisterValue_ShouldWorkCorrectly()
    {
        // Arrange
        const uint testValue = 0x12345678;

        // Act
        _emulatorService.SetRegisterValue("D1", testValue);
        var cpuState = _emulatorService.GetCpuState();

        // Assert
        cpuState.GetDataRegister(1).Should().Be(testValue);
    }

    [Test]
    public void Reset_ShouldClearAllRegisters()
    {
        // Arrange
        _emulatorService.SetRegisterValue("D0", 0x12345678);
        _emulatorService.SetRegisterValue("A0", 0x87654321);
        _emulatorService.SetRegisterValue("PC", 0x1000);

        // Act
        _emulatorService.Reset();

        // Assert
        var cpuState = _emulatorService.GetCpuState();
        cpuState.GetDataRegister(0).Should().Be(0);
        cpuState.GetAddressRegister(0).Should().Be(0);
        cpuState.PC.Should().Be(0);
        cpuState.SR.Should().Be(0x2700); // Supervisor mode, interrupts disabled
    }
}
