using Cpu32Emulator.Models;
using Cpu32Emulator.Services;

namespace Cpu32Emulator.Tests;

[TestFixture]
public class DumpFileTests
{
    private FileService _fileService;
    private string _testDumpFilePath;

    [SetUp]
    public void Setup()
    {
        _fileService = new FileService();
        
        // Get the path to the test data directory relative to the current assembly
        var assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
        var assemblyDirectory = Path.GetDirectoryName(assemblyLocation);
        var testDataPath = Path.Combine(assemblyDirectory!, "test-data");
        _testDumpFilePath = Path.Combine(testDataPath, "example.dump.txt");
    }

    [Test]
    public void ParseDumpLine_ValidInstructionLine_ShouldParseCorrectly()
    {
        // Arrange
        var line = "  ff8000:\t4fef ffe8      \tlea %sp@(-24),%sp";

        // Act
        var result = AssemblyEntry.ParseDumpLine(line, 1, ".kernel_code");

        // Assert
        result.Should().NotBeNull();
        result!.Address.Should().Be(0xff8000u);
        result.HexBytes.Should().Be("4fef ffe8");
        result.Instruction.Should().Be("lea %sp@(-24),%sp");
        result.SegmentName.Should().Be(".kernel_code");
        result.LineNumber.Should().Be(1);
        result.SymbolName.Should().BeNull();
    }

    [Test]
    public void ParseDumpLine_InstructionWithSymbol_ShouldExtractSymbol()
    {
        // Arrange
        var line = "  ff8008:\t4eb9 00ff 8bdc \tjsr ff8bdc <ScratchWatchdog>";

        // Act
        var result = AssemblyEntry.ParseDumpLine(line, 1, ".kernel_code");

        // Assert
        result.Should().NotBeNull();
        result!.Address.Should().Be(0xff8008u);
        result.HexBytes.Should().Be("4eb9 00ff 8bdc");
        result.Instruction.Should().Be("jsr ff8bdc <ScratchWatchdog>");
        result.SymbolName.Should().Be("ScratchWatchdog");
    }

    [Test]
    public void ParseDumpLine_ShortInstruction_ShouldParseCorrectly()
    {
        // Arrange
        var line = "  ff8014:\t2040           \tmoveal %d0,%a0";

        // Act
        var result = AssemblyEntry.ParseDumpLine(line, 1, ".kernel_code");

        // Assert
        result.Should().NotBeNull();
        result!.Address.Should().Be(0xff8014u);
        result.HexBytes.Should().Be("2040");
        result.Instruction.Should().Be("moveal %d0,%a0");
    }

    [Test]
    public void ParseDumpLine_InvalidLine_ShouldReturnNull()
    {
        // Arrange
        var invalidLines = new[]
        {
            "This is not a dump line",
            "Disassembly of section .kernel_code:",
            "00ff8000 <KernelStart>:",
            "",
            null
        };

        // Act & Assert
        foreach (var line in invalidLines)
        {
            var result = AssemblyEntry.ParseDumpLine(line, 1, ".kernel_code");
            result.Should().BeNull($"Line should be invalid: {line}");
        }
    }

    [Test]
    public void ParseDumpLine_LineWithoutProperFormat_ShouldReturnNull()
    {
        // Arrange
        var line = "  ff8000: invalid format";

        // Act
        var result = AssemblyEntry.ParseDumpLine(line, 1, ".kernel_code");

        // Assert
        result.Should().BeNull();
    }

    [Test]
    public async Task LoadDumpFile_ValidFile_ShouldLoadCorrectly()
    {
        // Arrange
        if (!File.Exists(_testDumpFilePath))
        {
            Assert.Inconclusive($"Test dump file not found: {_testDumpFilePath}");
        }

        // Act
        var entries = await _fileService.LoadDumpFileAsync(_testDumpFilePath);

        // Assert
        entries.Should().NotBeNull();
        entries.Should().NotBeEmpty();

        // Check that we have entries from the .kernel_code section
        var kernelCodeEntries = entries.Where(e => e.SegmentName == ".kernel_code").ToList();
        kernelCodeEntries.Should().NotBeEmpty("Should have entries from .kernel_code section");

        // Verify the first entry matches what we expect
        var firstEntry = entries.FirstOrDefault(e => e.Address == 0xff8000);
        firstEntry.Should().NotBeNull("Should find the KernelStart entry");
        firstEntry!.HexBytes.Should().Be("4fef ffe8");
        firstEntry.Instruction.Should().Be("lea %sp@(-24),%sp");
    }

    [Test]
    public async Task LoadDumpFile_NonExistentFile_ShouldThrowException()
    {
        // Arrange
        var nonExistentPath = "non-existent-file.dump";

        // Act & Assert
        var act = async () => await _fileService.LoadDumpFileAsync(nonExistentPath);
        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Test]
    public void LoadDumpFile_Synchronous_ShouldWorkCorrectly()
    {
        // Arrange
        if (!File.Exists(_testDumpFilePath))
        {
            Assert.Inconclusive($"Test dump file not found: {_testDumpFilePath}");
        }

        // Act
        var entries = _fileService.LoadDumpFile(_testDumpFilePath);

        // Assert
        entries.Should().NotBeNull();
        entries.Should().NotBeEmpty();
    }

    [Test]
    public async Task LoadDumpFile_ShouldExtractCorrectSections()
    {
        // Arrange
        if (!File.Exists(_testDumpFilePath))
        {
            Assert.Inconclusive($"Test dump file not found: {_testDumpFilePath}");
        }

        // Act
        var entries = await _fileService.LoadDumpFileAsync(_testDumpFilePath);

        // Assert
        var sections = entries.Select(e => e.SegmentName).Distinct().ToList();
        sections.Should().Contain(".kernel_code", "Should contain .kernel_code section");
        
        // Verify that entries are properly assigned to their sections
        foreach (var entry in entries)
        {
            entry.SegmentName.Should().NotBeNullOrEmpty("All entries should have a segment name");
        }
    }

    [Test]
    public async Task LoadDumpFile_ShouldParseSymbolsCorrectly()
    {
        // Arrange
        if (!File.Exists(_testDumpFilePath))
        {
            Assert.Inconclusive($"Test dump file not found: {_testDumpFilePath}");
        }

        // Act
        var entries = await _fileService.LoadDumpFileAsync(_testDumpFilePath);

        // Assert
        var entriesWithSymbols = entries.Where(e => !string.IsNullOrEmpty(e.SymbolName)).ToList();
        entriesWithSymbols.Should().NotBeEmpty("Should find entries with symbols");

        // Check for a known symbol
        var scratchWatchdogEntry = entries.FirstOrDefault(e => e.SymbolName == "ScratchWatchdog");
        scratchWatchdogEntry.Should().NotBeNull("Should find ScratchWatchdog symbol reference");
    }

    [Test]
    public async Task LoadDumpFile_ShouldMaintainCorrectAddressOrder()
    {
        // Arrange
        if (!File.Exists(_testDumpFilePath))
        {
            Assert.Inconclusive($"Test dump file not found: {_testDumpFilePath}");
        }

        // Act
        var entries = await _fileService.LoadDumpFileAsync(_testDumpFilePath);

        // Assert
        entries.Should().HaveCountGreaterThan(1, "Need multiple entries to test ordering");
        
        // Check that addresses are in order within each section
        var groupedBySections = entries.GroupBy(e => e.SegmentName);
        foreach (var section in groupedBySections)
        {
            var sectionEntries = section.OrderBy(e => e.LineNumber).ToList();
            for (int i = 1; i < sectionEntries.Count; i++)
            {
                sectionEntries[i].Address.Should().BeGreaterOrEqualTo(sectionEntries[i - 1].Address,
                    $"Addresses should be in order within section {section.Key}");
            }
        }
    }

    [Test]
    public void ParseDumpLine_EdgeCases_ShouldHandleCorrectly()
    {
        // Test various edge cases for the parser
        
        // Case 1: Line with extra spaces
        var line1 = "  ff8000:\t4fef ffe8        \t   lea %sp@(-24),%sp   ";
        var result1 = AssemblyEntry.ParseDumpLine(line1, 1, ".test");
        result1.Should().NotBeNull();
        result1!.HexBytes.Should().Be("4fef ffe8");
        result1.Instruction.Should().Be("lea %sp@(-24),%sp");

        // Case 2: Single byte instruction
        var line2 = "  ff8016:\t42           \tclrb %a0@";
        var result2 = AssemblyEntry.ParseDumpLine(line2, 1, ".test");
        result2.Should().NotBeNull();
        result2!.HexBytes.Should().Be("42");

        // Case 3: Very long hex bytes
        var line3 = "  ff8008:\t4eb9 00ff 8bdc aa bb cc \tjsr ff8bdc <TestFunction>";
        var result3 = AssemblyEntry.ParseDumpLine(line3, 1, ".test");
        result3.Should().NotBeNull();
        result3!.HexBytes.Should().Be("4eb9 00ff 8bdc aa bb cc");
    }
}
