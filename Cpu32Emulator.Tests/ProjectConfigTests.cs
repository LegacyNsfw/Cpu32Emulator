using NUnit.Framework;
using FluentAssertions;
using Cpu32Emulator.Models;

namespace Cpu32Emulator.Tests
{
    [TestFixture]
    public class ProjectConfigTests
    {
        [Test]
        public void SetLstFilePath_ShouldClearDumpFilePath()
        {
            // Arrange
            var config = ProjectConfig.CreateNew("Test Project");
            config.DumpFilePath = "test.dump.txt";

            // Act
            config.SetLstFilePath("test.lst");

            // Assert
            config.LstFilePath.Should().Be("test.lst");
            config.DumpFilePath.Should().BeNull();
        }

        [Test]
        public void SetDumpFilePath_ShouldClearLstFilePath()
        {
            // Arrange
            var config = ProjectConfig.CreateNew("Test Project");
            config.LstFilePath = "test.lst";

            // Act
            config.SetDumpFilePath("test.dump.txt");

            // Assert
            config.DumpFilePath.Should().Be("test.dump.txt");
            config.LstFilePath.Should().BeNull();
        }

        [Test]
        public void HasLstFile_ShouldReturnCorrectValue()
        {
            // Arrange
            var config = ProjectConfig.CreateNew("Test Project");

            // Act & Assert
            config.HasLstFile.Should().BeFalse();
            
            config.SetLstFilePath("test.lst");
            config.HasLstFile.Should().BeTrue();
            
            config.SetDumpFilePath("test.dump.txt");
            config.HasLstFile.Should().BeFalse();
        }

        [Test]
        public void HasDumpFile_ShouldReturnCorrectValue()
        {
            // Arrange
            var config = ProjectConfig.CreateNew("Test Project");

            // Act & Assert
            config.HasDumpFile.Should().BeFalse();
            
            config.SetDumpFilePath("test.dump.txt");
            config.HasDumpFile.Should().BeTrue();
            
            config.SetLstFilePath("test.lst");
            config.HasDumpFile.Should().BeFalse();
        }

        [Test]
        public void GetActiveAssemblyFilePath_ShouldReturnCorrectPath()
        {
            // Arrange
            var config = ProjectConfig.CreateNew("Test Project");

            // Act & Assert
            config.GetActiveAssemblyFilePath().Should().BeNull();
            
            config.SetLstFilePath("test.lst");
            config.GetActiveAssemblyFilePath().Should().Be("test.lst");
            
            config.SetDumpFilePath("test.dump.txt");
            config.GetActiveAssemblyFilePath().Should().Be("test.dump.txt");
        }

        [Test]
        public void ToString_ShouldIncludeAssemblyFileType()
        {
            // Arrange
            var config = ProjectConfig.CreateNew("Test Project");

            // Act & Assert
            config.ToString().Should().Contain("Assembly: None");
            
            config.SetLstFilePath("test.lst");
            config.ToString().Should().Contain("Assembly: LST");
            
            config.SetDumpFilePath("test.dump.txt");
            config.ToString().Should().Contain("Assembly: Dump");
        }

        [Test]
        public void Validate_ShouldCheckDumpFile()
        {
            // Arrange
            var config = ProjectConfig.CreateNew("Test Project");
            config.SetDumpFilePath("nonexistent.dump.txt");

            // Act
            var errors = config.Validate();

            // Assert
            errors.Should().ContainSingle(error => error.Contains("Dump file not found"));
        }
    }
}
