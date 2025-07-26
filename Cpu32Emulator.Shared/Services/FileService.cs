using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Cpu32Emulator.Models;

namespace Cpu32Emulator.Services
{
    /// <summary>
    /// Service for handling file operations (ROM, RAM, LST, project files)
    /// </summary>
    public class FileService
    {
        /// <summary>
        /// Loads a ROM file from disk
        /// </summary>
        public async Task<MemoryRegion> LoadRomFileAsync(string filePath, uint baseAddress)
        {
            return await Task.Run(() => LoadRomFile(filePath, baseAddress));
        }

        /// <summary>
        /// Loads a ROM file from disk (synchronous)
        /// </summary>
        public MemoryRegion LoadRomFile(string filePath, uint baseAddress)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"ROM file not found: {filePath}");

            return MemoryRegion.FromFile(filePath, baseAddress, MemoryRegionType.ROM);
        }

        /// <summary>
        /// Loads a RAM file from disk
        /// </summary>
        public async Task<MemoryRegion> LoadRamFileAsync(string filePath, uint baseAddress)
        {
            return await Task.Run(() => LoadRamFile(filePath, baseAddress));
        }

        /// <summary>
        /// Loads a RAM file from disk (synchronous)
        /// </summary>
        public MemoryRegion LoadRamFile(string filePath, uint baseAddress)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"RAM file not found: {filePath}");

            return MemoryRegion.FromFile(filePath, baseAddress, MemoryRegionType.RAM);
        }

        /// <summary>
        /// Loads an LST file and parses it into entries
        /// </summary>
        public async Task<List<LstEntry>> LoadLstFileAsync(string filePath)
        {
            return await Task.Run(() => LoadLstFile(filePath));
        }

        /// <summary>
        /// Loads an LST file and parses it into entries (synchronous)
        /// </summary>
        public List<LstEntry> LoadLstFile(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"LST file not found: {filePath}");

            var entries = new List<LstEntry>();
            var lines = File.ReadAllLines(filePath);

            for (int i = 0; i < lines.Length; i++)
            {
                var entry = LstEntry.ParseLine(lines[i], i + 1);
                if (entry != null)
                {
                    entries.Add(entry);
                }
            }

            return entries;
        }

        /// <summary>
        /// Saves a project configuration to disk
        /// </summary>
        public async Task SaveProjectAsync(string filePath, ProjectConfig project)
        {
            await Task.Run(() => SaveProject(filePath, project));
        }

        /// <summary>
        /// Saves a project configuration to disk (synchronous)
        /// </summary>
        public void SaveProject(string filePath, ProjectConfig project)
        {
            project.MarkAsModified();

            var options = new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true
            };

            var json = System.Text.Json.JsonSerializer.Serialize(project, options);
            File.WriteAllText(filePath, json);
        }

        /// <summary>
        /// Loads a project configuration from disk
        /// </summary>
        public async Task<ProjectConfig> LoadProjectAsync(string filePath)
        {
            return await Task.Run(() => LoadProject(filePath));
        }

        /// <summary>
        /// Loads a project configuration from disk (synchronous)
        /// </summary>
        public ProjectConfig LoadProject(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Project file not found: {filePath}");

            var json = File.ReadAllText(filePath);
            var options = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var project = System.Text.Json.JsonSerializer.Deserialize<ProjectConfig>(json, options);
            if (project == null)
                throw new InvalidOperationException("Failed to deserialize project file");

            return project;
        }

        /// <summary>
        /// Validates that all files referenced in a project exist
        /// </summary>
        public List<string> ValidateProjectFiles(ProjectConfig project)
        {
            var errors = new List<string>();

            if (!string.IsNullOrEmpty(project.RomFilePath) && !File.Exists(project.RomFilePath))
                errors.Add($"ROM file not found: {project.RomFilePath}");

            if (!string.IsNullOrEmpty(project.RamFilePath) && !File.Exists(project.RamFilePath))
                errors.Add($"RAM file not found: {project.RamFilePath}");

            if (!string.IsNullOrEmpty(project.LstFilePath) && !File.Exists(project.LstFilePath))
                errors.Add($"LST file not found: {project.LstFilePath}");

            return errors;
        }

        /// <summary>
        /// Gets the size of a file in bytes
        /// </summary>
        public long GetFileSize(string filePath)
        {
            if (!File.Exists(filePath))
                return 0;

            var fileInfo = new FileInfo(filePath);
            return fileInfo.Length;
        }

        /// <summary>
        /// Gets file information for display purposes
        /// </summary>
        public FileInfo GetFileInfo(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"File not found: {filePath}");

            return new FileInfo(filePath);
        }

        /// <summary>
        /// Creates a backup copy of a file
        /// </summary>
        public void CreateBackup(string filePath)
        {
            if (!File.Exists(filePath))
                return;

            string backupPath = $"{filePath}.backup_{DateTime.Now:yyyyMMdd_HHmmss}";
            File.Copy(filePath, backupPath);
        }

        /// <summary>
        /// Gets common file filters for file dialogs
        /// </summary>
        public static class FileFilters
        {
            public const string RomFiles = "ROM Files (*.bin)|*.bin|All Files (*.*)|*.*";
            public const string RamFiles = "RAM Files (*.ram.bin)|*.ram.bin|Binary Files (*.bin)|*.bin|All Files (*.*)|*.*";
            public const string LstFiles = "LST Files (*.lst)|*.lst|All Files (*.*)|*.*";
            public const string ProjectFiles = "Project Files (*.json)|*.json|All Files (*.*)|*.*";
            public const string AllFiles = "All Files (*.*)|*.*";
        }

        /// <summary>
        /// Gets the appropriate file extension for a file type
        /// </summary>
        public static string GetDefaultExtension(string fileType)
        {
            return fileType.ToLowerInvariant() switch
            {
                "rom" => ".bin",
                "ram" => ".ram.bin",
                "lst" => ".lst",
                "project" => ".json",
                _ => ".bin"
            };
        }

        /// <summary>
        /// Checks if a file path is valid
        /// </summary>
        public static bool IsValidFilePath(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return false;

            try
            {
                var fullPath = Path.GetFullPath(filePath);
                return !string.IsNullOrWhiteSpace(Path.GetFileName(fullPath));
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Makes a file path relative to a base directory
        /// </summary>
        public static string MakeRelativePath(string basePath, string targetPath)
        {
            if (string.IsNullOrWhiteSpace(basePath) || string.IsNullOrWhiteSpace(targetPath))
                return targetPath;

            try
            {
                var baseUri = new Uri(Path.GetFullPath(basePath) + Path.DirectorySeparatorChar);
                var targetUri = new Uri(Path.GetFullPath(targetPath));
                return baseUri.MakeRelativeUri(targetUri).ToString().Replace('/', Path.DirectorySeparatorChar);
            }
            catch
            {
                return targetPath;
            }
        }
    }
}
