using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Cpu32Emulator.Models
{
    /// <summary>
    /// Represents a project configuration that can be saved/loaded
    /// </summary>
    public class ProjectConfig
    {
        public string ProjectName { get; set; } = string.Empty;
        public string? RomFilePath { get; set; }
        public uint RomBaseAddress { get; set; }
        public string? RamFilePath { get; set; }
        public uint RamBaseAddress { get; set; }
        public string? LstFilePath { get; set; }
        public string? DumpFilePath { get; set; }
        public List<WatchedMemoryConfig> WatchedMemoryLocations { get; set; } = new();
        public uint ResetAddress { get; set; }
        public CpuStateConfig? SavedCpuState { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastModifiedAt { get; set; }

        /// <summary>
        /// Creates a new empty project configuration
        /// </summary>
        public static ProjectConfig CreateNew(string projectName = "Untitled Project")
        {
            var now = DateTime.Now;
            return new ProjectConfig
            {
                ProjectName = projectName,
                CreatedAt = now,
                LastModifiedAt = now
            };
        }

        /// <summary>
        /// Updates the last modified timestamp
        /// </summary>
        public void MarkAsModified()
        {
            LastModifiedAt = DateTime.Now;
        }

        /// <summary>
        /// Sets the LST file path and clears the dump file path
        /// </summary>
        public void SetLstFilePath(string? lstFilePath)
        {
            LstFilePath = lstFilePath;
            DumpFilePath = null;
            MarkAsModified();
        }

        /// <summary>
        /// Sets the dump file path and clears the LST file path
        /// </summary>
        public void SetDumpFilePath(string? dumpFilePath)
        {
            DumpFilePath = dumpFilePath;
            LstFilePath = null;
            MarkAsModified();
        }

        /// <summary>
        /// Gets the active assembly file path (either LST or dump)
        /// </summary>
        public string? GetActiveAssemblyFilePath()
        {
            return LstFilePath ?? DumpFilePath;
        }

        /// <summary>
        /// Returns true if a dump file is currently configured
        /// </summary>
        public bool HasDumpFile => !string.IsNullOrEmpty(DumpFilePath);

        /// <summary>
        /// Returns true if an LST file is currently configured
        /// </summary>
        public bool HasLstFile => !string.IsNullOrEmpty(LstFilePath);

        /// <summary>
        /// Validates the project configuration
        /// </summary>
        public List<string> Validate()
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(ProjectName))
                errors.Add("Project name is required");

            if (!string.IsNullOrEmpty(RomFilePath) && !System.IO.File.Exists(RomFilePath))
                errors.Add($"ROM file not found: {RomFilePath}");

            if (!string.IsNullOrEmpty(RamFilePath) && !System.IO.File.Exists(RamFilePath))
                errors.Add($"RAM file not found: {RamFilePath}");

            if (!string.IsNullOrEmpty(LstFilePath) && !System.IO.File.Exists(LstFilePath))
                errors.Add($"LST file not found: {LstFilePath}");

            if (!string.IsNullOrEmpty(DumpFilePath) && !System.IO.File.Exists(DumpFilePath))
                errors.Add($"Dump file not found: {DumpFilePath}");

            return errors;
        }

        public override string ToString()
        {
            var assemblyFile = HasLstFile ? "LST" : HasDumpFile ? "Dump" : "None";
            return $"Project: {ProjectName} (Assembly: {assemblyFile}, Modified: {LastModifiedAt:yyyy-MM-dd HH:mm:ss})";
        }
    }

    /// <summary>
    /// Serializable configuration for watched memory locations
    /// </summary>
    public class WatchedMemoryConfig
    {
        public uint Address { get; set; }
        public DataWidth Width { get; set; }
        public string Label { get; set; } = string.Empty;

        /// <summary>
        /// Converts to WatchedMemory model
        /// </summary>
        public WatchedMemory ToWatchedMemory()
        {
            return new WatchedMemory
            {
                Address = Address,
                Width = Width,
                Label = Label,
                IsEditable = true
            };
        }

        /// <summary>
        /// Creates from WatchedMemory model
        /// </summary>
        public static WatchedMemoryConfig FromWatchedMemory(WatchedMemory watchedMemory)
        {
            return new WatchedMemoryConfig
            {
                Address = watchedMemory.Address,
                Width = watchedMemory.Width,
                Label = watchedMemory.Label
            };
        }
    }

    /// <summary>
    /// Serializable configuration for CPU state
    /// </summary>
    public class CpuStateConfig
    {
        public uint D0 { get; set; }
        public uint D1 { get; set; }
        public uint D2 { get; set; }
        public uint D3 { get; set; }
        public uint D4 { get; set; }
        public uint D5 { get; set; }
        public uint D6 { get; set; }
        public uint D7 { get; set; }
        public uint A0 { get; set; }
        public uint A1 { get; set; }
        public uint A2 { get; set; }
        public uint A3 { get; set; }
        public uint A4 { get; set; }
        public uint A5 { get; set; }
        public uint A6 { get; set; }
        public uint USP { get; set; }
        public uint PC { get; set; }
        public uint CCR { get; set; }
        public uint SSP { get; set; }
        public uint SR { get; set; }
        public uint VBR { get; set; }
        public uint SFC { get; set; }
        public uint DFC { get; set; }

        /// <summary>
        /// Converts to CpuState model
        /// </summary>
        public CpuState ToCpuState()
        {
            return new CpuState
            {
                D0 = D0, D1 = D1, D2 = D2, D3 = D3, D4 = D4, D5 = D5, D6 = D6, D7 = D7,
                A0 = A0, A1 = A1, A2 = A2, A3 = A3, A4 = A4, A5 = A5, A6 = A6,
                USP = USP, PC = PC, CCR = CCR, SSP = SSP, SR = SR, VBR = VBR, SFC = SFC, DFC = DFC
            };
        }

        /// <summary>
        /// Creates from CpuState model
        /// </summary>
        public static CpuStateConfig FromCpuState(CpuState cpuState)
        {
            return new CpuStateConfig
            {
                D0 = cpuState.D0, D1 = cpuState.D1, D2 = cpuState.D2, D3 = cpuState.D3,
                D4 = cpuState.D4, D5 = cpuState.D5, D6 = cpuState.D6, D7 = cpuState.D7,
                A0 = cpuState.A0, A1 = cpuState.A1, A2 = cpuState.A2, A3 = cpuState.A3,
                A4 = cpuState.A4, A5 = cpuState.A5, A6 = cpuState.A6,
                USP = cpuState.USP, PC = cpuState.PC, CCR = cpuState.CCR, SSP = cpuState.SSP,
                SR = cpuState.SR, VBR = cpuState.VBR, SFC = cpuState.SFC, DFC = cpuState.DFC
            };
        }
    }
}
