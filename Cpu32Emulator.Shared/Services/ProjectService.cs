using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Cpu32Emulator.Models;

namespace Cpu32Emulator.Services
{
    /// <summary>
    /// Service for managing project operations (save, load, validation)
    /// </summary>
    public class ProjectService
    {
        private readonly FileService _fileService;
        private ProjectConfig? _currentProject;
        private string? _currentProjectPath;
        private bool _hasUnsavedChanges;

        /// <summary>
        /// Event raised when the current project changes
        /// </summary>
        public event EventHandler<ProjectChangedEventArgs>? ProjectChanged;

        /// <summary>
        /// Gets the current project configuration
        /// </summary>
        public ProjectConfig? CurrentProject => _currentProject;

        /// <summary>
        /// Gets the path of the current project file
        /// </summary>
        public string? CurrentProjectPath => _currentProjectPath;

        /// <summary>
        /// Gets whether there are unsaved changes
        /// </summary>
        public bool HasUnsavedChanges => _hasUnsavedChanges;

        /// <summary>
        /// Gets whether a project is currently loaded
        /// </summary>
        public bool IsProjectLoaded => _currentProject != null;

        public ProjectService(FileService fileService)
        {
            _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
        }

        /// <summary>
        /// Creates a new project
        /// </summary>
        public void NewProject(string projectName = "Untitled Project")
        {
            _currentProject = ProjectConfig.CreateNew(projectName);
            _currentProjectPath = null;
            _hasUnsavedChanges = true;

            OnProjectChanged(new ProjectChangedEventArgs(ProjectChangeType.Created, _currentProject));
        }

        /// <summary>
        /// Loads a project from file
        /// </summary>
        public async Task LoadProjectAsync(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Project file not found: {filePath}");

            var project = await _fileService.LoadProjectAsync(filePath);
            
            // Validate the project files
            var errors = _fileService.ValidateProjectFiles(project);
            if (errors.Count > 0)
            {
                throw new InvalidOperationException($"Project validation failed:\n{string.Join("\n", errors)}");
            }

            _currentProject = project;
            _currentProjectPath = filePath;
            _hasUnsavedChanges = false;

            OnProjectChanged(new ProjectChangedEventArgs(ProjectChangeType.Loaded, _currentProject));
        }

        /// <summary>
        /// Saves the current project
        /// </summary>
        public async Task SaveProjectAsync()
        {
            if (_currentProject == null)
                throw new InvalidOperationException("No project is loaded");

            if (string.IsNullOrEmpty(_currentProjectPath))
                throw new InvalidOperationException("Project path not set. Use SaveProjectAsAsync instead.");

            await _fileService.SaveProjectAsync(_currentProjectPath, _currentProject);
            _hasUnsavedChanges = false;

            OnProjectChanged(new ProjectChangedEventArgs(ProjectChangeType.Saved, _currentProject));
        }

        /// <summary>
        /// Saves the current project to a specific file
        /// </summary>
        public async Task SaveProjectAsAsync(string filePath)
        {
            if (_currentProject == null)
                throw new InvalidOperationException("No project is loaded");

            // Create backup if file already exists
            if (File.Exists(filePath))
            {
                _fileService.CreateBackup(filePath);
            }

            await _fileService.SaveProjectAsync(filePath, _currentProject);
            _currentProjectPath = filePath;
            _hasUnsavedChanges = false;

            OnProjectChanged(new ProjectChangedEventArgs(ProjectChangeType.Saved, _currentProject));
        }

        /// <summary>
        /// Closes the current project
        /// </summary>
        public void CloseProject()
        {
            var oldProject = _currentProject;
            _currentProject = null;
            _currentProjectPath = null;
            _hasUnsavedChanges = false;

            if (oldProject != null)
            {
                OnProjectChanged(new ProjectChangedEventArgs(ProjectChangeType.Closed, oldProject));
            }
        }

        /// <summary>
        /// Updates the ROM file configuration
        /// </summary>
        public void SetRomFile(string filePath, uint baseAddress)
        {
            if (_currentProject == null)
                throw new InvalidOperationException("No project is loaded");

            if (!File.Exists(filePath))
                throw new FileNotFoundException($"ROM file not found: {filePath}");

            _currentProject.RomFilePath = filePath;
            _currentProject.RomBaseAddress = baseAddress;
            MarkAsModified();
        }

        /// <summary>
        /// Updates the RAM file configuration
        /// </summary>
        public void SetRamFile(string filePath, uint baseAddress)
        {
            if (_currentProject == null)
                throw new InvalidOperationException("No project is loaded");

            if (!File.Exists(filePath))
                throw new FileNotFoundException($"RAM file not found: {filePath}");

            _currentProject.RamFilePath = filePath;
            _currentProject.RamBaseAddress = baseAddress;
            MarkAsModified();
        }

        /// <summary>
        /// Updates the LST file configuration
        /// </summary>
        public void SetLstFile(string filePath)
        {
            if (_currentProject == null)
                throw new InvalidOperationException("No project is loaded");

            if (!File.Exists(filePath))
                throw new FileNotFoundException($"LST file not found: {filePath}");

            _currentProject.LstFilePath = filePath;
            MarkAsModified();
        }

        /// <summary>
        /// Updates the watched memory locations
        /// </summary>
        public void SetWatchedMemoryLocations(List<WatchedMemory> watchedLocations)
        {
            if (_currentProject == null)
                throw new InvalidOperationException("No project is loaded");

            _currentProject.WatchedMemoryLocations = watchedLocations
                .ConvertAll(w => WatchedMemoryConfig.FromWatchedMemory(w));
            MarkAsModified();
        }

        /// <summary>
        /// Updates the reset address
        /// </summary>
        public void SetResetAddress(uint resetAddress)
        {
            if (_currentProject == null)
                throw new InvalidOperationException("No project is loaded");

            _currentProject.ResetAddress = resetAddress;
            MarkAsModified();
        }

        /// <summary>
        /// Updates the saved CPU state
        /// </summary>
        public void SetSavedCpuState(CpuState cpuState)
        {
            if (_currentProject == null)
                throw new InvalidOperationException("No project is loaded");

            _currentProject.SavedCpuState = CpuStateConfig.FromCpuState(cpuState);
            MarkAsModified();
        }

        /// <summary>
        /// Gets the watched memory locations from the current project
        /// </summary>
        public List<WatchedMemory> GetWatchedMemoryLocations()
        {
            if (_currentProject == null)
                return new List<WatchedMemory>();

            return _currentProject.WatchedMemoryLocations
                .ConvertAll(w => w.ToWatchedMemory());
        }

        /// <summary>
        /// Gets the saved CPU state from the current project
        /// </summary>
        public CpuState? GetSavedCpuState()
        {
            return _currentProject?.SavedCpuState?.ToCpuState();
        }

        /// <summary>
        /// Validates the current project
        /// </summary>
        public List<string> ValidateCurrentProject()
        {
            if (_currentProject == null)
                return new List<string> { "No project is loaded" };

            var errors = _currentProject.Validate();
            errors.AddRange(_fileService.ValidateProjectFiles(_currentProject));
            return errors;
        }

        /// <summary>
        /// Gets recent project files (placeholder for future implementation)
        /// </summary>
        public List<string> GetRecentProjects()
        {
            // TODO: Implement recent projects tracking
            return new List<string>();
        }

        /// <summary>
        /// Checks if the current project can be saved
        /// </summary>
        public bool CanSave()
        {
            return _currentProject != null && !string.IsNullOrEmpty(_currentProjectPath);
        }

        /// <summary>
        /// Checks if the current project needs to be saved
        /// </summary>
        public bool NeedsSaving()
        {
            return _currentProject != null && _hasUnsavedChanges;
        }

        /// <summary>
        /// Gets a display name for the current project
        /// </summary>
        public string GetDisplayName()
        {
            if (_currentProject == null)
                return "No Project";

            var name = _currentProject.ProjectName;
            if (_hasUnsavedChanges)
                name += "*";

            return name;
        }

        /// <summary>
        /// Marks the current project as modified
        /// </summary>
        private void MarkAsModified()
        {
            if (_currentProject != null)
            {
                _currentProject.MarkAsModified();
                _hasUnsavedChanges = true;
                OnProjectChanged(new ProjectChangedEventArgs(ProjectChangeType.Modified, _currentProject));
            }
        }

        /// <summary>
        /// Raises the ProjectChanged event
        /// </summary>
        protected virtual void OnProjectChanged(ProjectChangedEventArgs e)
        {
            ProjectChanged?.Invoke(this, e);
        }
    }

    /// <summary>
    /// Event arguments for project change events
    /// </summary>
    public class ProjectChangedEventArgs : EventArgs
    {
        public ProjectChangeType ChangeType { get; }
        public ProjectConfig Project { get; }

        public ProjectChangedEventArgs(ProjectChangeType changeType, ProjectConfig project)
        {
            ChangeType = changeType;
            Project = project ?? throw new ArgumentNullException(nameof(project));
        }
    }

    /// <summary>
    /// Types of project changes
    /// </summary>
    public enum ProjectChangeType
    {
        Created,
        Loaded,
        Saved,
        Modified,
        Closed
    }
}
