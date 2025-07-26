using System;
using System.Collections.Generic;
using System.Linq;
using Cpu32Emulator.Models;
using Unicorn;
using Unicorn.M68k;

namespace Cpu32Emulator.Services
{
    /// <summary>
    /// Service that wraps the Unicorn CPU emulator engine
    /// </summary>
    public class UnicornEmulatorService : IDisposable
    {
        private Engine? _engine;
        private readonly List<MemoryRegion> _memoryRegions = new();
        private bool _disposed = false;

        /// <summary>
        /// Gets whether the emulator is initialized
        /// </summary>
        public bool IsInitialized => _engine != null;

        /// <summary>
        /// Gets the current exception information if any
        /// </summary>
        public string? LastException { get; private set; }

        /// <summary>
        /// Initializes the Unicorn engine with M68K architecture
        /// </summary>
        public void Initialize()
        {
            if (_engine != null)
                return;

            try
            {
                _engine = new Engine(Arch.M68K, Mode.M68K32);
                LastException = null;
            }
            catch (Exception ex)
            {
                LastException = $"Failed to initialize Unicorn engine: {ex.Message}";
                throw;
            }
        }

        /// <summary>
        /// Maps a memory region in the emulator
        /// </summary>
        public void MapMemoryRegion(MemoryRegion region)
        {
            if (_engine == null)
                throw new InvalidOperationException("Emulator not initialized");

            try
            {
                // Map the memory region
                var permissions = region.Type == MemoryRegionType.ROM 
                    ? Permissions.Read | Permissions.Exec 
                    : Permissions.Read | Permissions.Write | Permissions.Exec;

                _engine.MemoryMap(region.BaseAddress, region.Size, permissions);
                
                // Write the data to the mapped region
                _engine.MemoryWrite(region.BaseAddress, region.Data);
                
                _memoryRegions.Add(region);
                LastException = null;
            }
            catch (Exception ex)
            {
                LastException = $"Failed to map memory region: {ex.Message}";
                throw;
            }
        }

        /// <summary>
        /// Unmaps all memory regions
        /// </summary>
        public void UnmapAllMemory()
        {
            if (_engine == null)
                return;

            try
            {
                foreach (var region in _memoryRegions)
                {
                    _engine.MemoryUnmap(region.BaseAddress, region.Size);
                }
                _memoryRegions.Clear();
                LastException = null;
            }
            catch (Exception ex)
            {
                LastException = $"Failed to unmap memory: {ex.Message}";
            }
        }

        /// <summary>
        /// Gets the current CPU state from the emulator
        /// </summary>
        public CpuState GetCpuState()
        {
            if (_engine == null)
                throw new InvalidOperationException("Emulator not initialized");

            try
            {
                var state = new CpuState();

                // Read data registers
                for (int i = 0; i < 8; i++)
                {
                    uint value = _engine.RegisterRead((int)(M68kRegister.D0 + i));
                    state.SetDataRegister(i, value);
                }

                // Read address registers
                for (int i = 0; i < 7; i++)
                {
                    uint value = _engine.RegisterRead((int)(M68kRegister.A0 + i));
                    state.SetAddressRegister(i, value);
                }

                // Read special registers
                state.USP = _engine.RegisterRead((int)M68kRegister.A7);
                state.PC = _engine.RegisterRead((int)M68kRegister.PC);
                state.SR = _engine.RegisterRead((int)M68kRegister.SR);
                
                // Note: Unicorn M68K doesn't expose all CPU32 registers directly
                // We'll need to add CPU32 support later or use approximations
                state.CCR = (uint)(state.SR & 0xFF); // CCR is lower byte of SR
                state.SSP = state.USP; // Simplified for now
                state.VBR = 0; // Will need CPU32 support
                state.SFC = 0; // Will need CPU32 support
                state.DFC = 0; // Will need CPU32 support

                LastException = null;
                return state;
            }
            catch (Exception ex)
            {
                LastException = $"Failed to read CPU state: {ex.Message}";
                throw;
            }
        }

        /// <summary>
        /// Sets the CPU state in the emulator
        /// </summary>
        public void SetCpuState(CpuState state)
        {
            if (_engine == null)
                throw new InvalidOperationException("Emulator not initialized");

            try
            {
                // Write data registers
                for (int i = 0; i < 8; i++)
                {
                    _engine.RegisterWrite((int)(M68kRegister.D0 + i), state.GetDataRegister(i));
                }

                // Write address registers
                for (int i = 0; i < 7; i++)
                {
                    _engine.RegisterWrite((int)(M68kRegister.A0 + i), state.GetAddressRegister(i));
                }

                // Write special registers
                _engine.RegisterWrite((int)M68kRegister.A7, state.USP);
                _engine.RegisterWrite((int)M68kRegister.PC, state.PC);
                _engine.RegisterWrite((int)M68kRegister.SR, state.SR);

                LastException = null;
            }
            catch (Exception ex)
            {
                LastException = $"Failed to write CPU state: {ex.Message}";
                throw;
            }
        }

        /// <summary>
        /// Executes a single instruction
        /// </summary>
        public void StepInstruction()
        {
            if (_engine == null)
                throw new InvalidOperationException("Emulator not initialized");

            try
            {
                uint pc = _engine.RegisterRead((int)M68kRegister.PC);
                _engine.EmuStart(pc, 0, 0, 1); // Execute 1 instruction
                LastException = null;
            }
            catch (Exception ex)
            {
                LastException = $"Execution error: {ex.Message}";
                throw;
            }
        }

        /// <summary>
        /// Runs execution until a breakpoint or error
        /// </summary>
        public void Run(uint? endAddress = null, uint maxInstructions = 100000)
        {
            if (_engine == null)
                throw new InvalidOperationException("Emulator not initialized");

            try
            {
                uint pc = _engine.RegisterRead((int)M68kRegister.PC);
                uint end = endAddress ?? 0;
                _engine.EmuStart(pc, end, 0, maxInstructions);
                LastException = null;
            }
            catch (Exception ex)
            {
                LastException = $"Execution error: {ex.Message}";
                throw;
            }
        }

        /// <summary>
        /// Reads memory from the emulator
        /// </summary>
        public byte[] ReadMemory(uint address, uint size)
        {
            if (_engine == null)
                throw new InvalidOperationException("Emulator not initialized");

            try
            {
                var data = _engine.MemoryRead(address, size);
                LastException = null;
                return data;
            }
            catch (Exception ex)
            {
                LastException = $"Failed to read memory: {ex.Message}";
                throw;
            }
        }

        /// <summary>
        /// Writes memory to the emulator
        /// </summary>
        public void WriteMemory(uint address, byte[] data)
        {
            if (_engine == null)
                throw new InvalidOperationException("Emulator not initialized");

            // Check if this is a ROM region
            var region = _memoryRegions.FirstOrDefault(r => r.ContainsAddress(address));
            if (region?.Type == MemoryRegionType.ROM)
                throw new InvalidOperationException("Cannot write to ROM memory region");

            try
            {
                _engine.MemoryWrite(address, data);
                
                // Also update the memory region data if it exists
                if (region != null)
                {
                    uint offset = region.GetOffset(address);
                    Array.Copy(data, 0, region.Data, offset, Math.Min(data.Length, region.Data.Length - offset));
                }
                
                LastException = null;
            }
            catch (Exception ex)
            {
                LastException = $"Failed to write memory: {ex.Message}";
                throw;
            }
        }

        /// <summary>
        /// Gets all mapped memory regions
        /// </summary>
        public IReadOnlyList<MemoryRegion> GetMemoryRegions()
        {
            return _memoryRegions.AsReadOnly();
        }

        /// <summary>
        /// Finds the memory region containing the specified address
        /// </summary>
        public MemoryRegion? FindMemoryRegion(uint address)
        {
            return _memoryRegions.FirstOrDefault(r => r.ContainsAddress(address));
        }

        /// <summary>
        /// Resets the emulator to initial state
        /// </summary>
        public void Reset()
        {
            if (_engine == null)
                return;

            try
            {
                // Clear all registers
                for (int i = 0; i < 8; i++)
                {
                    _engine.RegisterWrite((int)(M68kRegister.D0 + i), 0);
                    if (i < 7)
                        _engine.RegisterWrite((int)(M68kRegister.A0 + i), 0);
                }

                _engine.RegisterWrite((int)M68kRegister.A7, 0);
                _engine.RegisterWrite((int)M68kRegister.PC, 0);
                _engine.RegisterWrite((int)M68kRegister.SR, 0x2700); // Supervisor mode, interrupts disabled

                LastException = null;
            }
            catch (Exception ex)
            {
                LastException = $"Failed to reset emulator: {ex.Message}";
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                UnmapAllMemory();
                _engine?.Dispose();
                _engine = null;
                _disposed = true;
            }
        }
    }
}
