using System;
using System.Collections.Generic;
using System.Linq;
using Cpu32Emulator.Models;
using Microsoft.Extensions.Logging;
using UnicornEngine;
using UnicornEngine.Const;

namespace Cpu32Emulator.Services
{
    /// <summary>
    /// Service that wraps the Unicorn CPU emulator engine
    /// </summary>
    public class UnicornEmulatorService : IDisposable
    {
        private Unicorn? _engine;
        private readonly List<MemoryRegion> _memoryRegions = new();
        private MemoryManagerService? _memoryManager;
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
        /// Sets the memory manager for enhanced memory operations
        /// </summary>
        public void SetMemoryManager(MemoryManagerService memoryManager)
        {
            _memoryManager = memoryManager ?? throw new ArgumentNullException(nameof(memoryManager));
        }

        /// <summary>
        /// Initializes the Unicorn engine with M68K architecture
        /// </summary>
        public void Initialize()
        {
            if (_engine != null)
                return;

            try
            {
                _engine = new Unicorn(Common.UC_ARCH_M68K, Common.UC_MODE_BIG_ENDIAN);
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
                    ? Common.UC_PROT_READ | Common.UC_PROT_EXEC 
                    : Common.UC_PROT_ALL;

                _engine.MemMap(region.BaseAddress, region.Size, permissions);
                
                // Write the data to the mapped region
                _engine.MemWrite(region.BaseAddress, region.Data);
                
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
                    _engine.MemUnmap(region.BaseAddress, region.Size);
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
        /// Unmaps a specific memory region at the given address
        /// </summary>
        public bool UnmapMemoryRegion(uint baseAddress)
        {
            if (_engine == null)
                return false;

            var region = _memoryRegions.FirstOrDefault(r => r.BaseAddress == baseAddress);
            if (region == null)
                return false;

            try
            {
                _engine.MemUnmap(region.BaseAddress, region.Size);
                _memoryRegions.Remove(region);
                LastException = null;
                return true;
            }
            catch (Exception ex)
            {
                LastException = $"Failed to unmap memory region: {ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// Checks if a memory region would overlap with existing regions
        /// </summary>
        public MemoryRegion? FindOverlappingRegion(uint baseAddress, uint size)
        {
            uint endAddress = baseAddress + size;
            return _memoryRegions.FirstOrDefault(r => 
                (baseAddress < r.BaseAddress + r.Size && endAddress > r.BaseAddress));
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
                    uint value = (uint)_engine.RegRead(M68k.UC_M68K_REG_D0 + i);
                    state.SetDataRegister(i, value);
                }

                // Read address registers
                for (int i = 0; i < 7; i++)
                {
                    uint value = (uint)_engine.RegRead(M68k.UC_M68K_REG_A0 + i);
                    state.SetAddressRegister(i, value);
                }

                // Read special registers
                state.USP = (uint)_engine.RegRead(M68k.UC_M68K_REG_A7);
                state.PC = (uint)_engine.RegRead(M68k.UC_M68K_REG_PC);
                state.SR = (uint)_engine.RegRead(M68k.UC_M68K_REG_SR);
                
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
                    _engine.RegWrite(M68k.UC_M68K_REG_D0 + i, state.GetDataRegister(i));
                }

                // Write address registers
                for (int i = 0; i < 7; i++)
                {
                    _engine.RegWrite(M68k.UC_M68K_REG_A0 + i, state.GetAddressRegister(i));
                }

                // Write special registers
                _engine.RegWrite(M68k.UC_M68K_REG_A7, state.USP);
                _engine.RegWrite(M68k.UC_M68K_REG_PC, state.PC);
                _engine.RegWrite(M68k.UC_M68K_REG_SR, state.SR);

                LastException = null;
            }
            catch (Exception ex)
            {
                LastException = $"Failed to write CPU state: {ex.Message}";
                throw;
            }
        }

        /// <summary>
        /// Sets a specific register value by name
        /// </summary>
        public void SetRegisterValue(string registerName, uint value)
        {
            if (_engine == null)
                throw new InvalidOperationException("Emulator not initialized");

            try
            {
                switch (registerName.ToUpperInvariant())
                {
                    // Data registers
                    case "D0": _engine.RegWrite(M68k.UC_M68K_REG_D0, value); break;
                    case "D1": _engine.RegWrite(M68k.UC_M68K_REG_D1, value); break;
                    case "D2": _engine.RegWrite(M68k.UC_M68K_REG_D2, value); break;
                    case "D3": _engine.RegWrite(M68k.UC_M68K_REG_D3, value); break;
                    case "D4": _engine.RegWrite(M68k.UC_M68K_REG_D4, value); break;
                    case "D5": _engine.RegWrite(M68k.UC_M68K_REG_D5, value); break;
                    case "D6": _engine.RegWrite(M68k.UC_M68K_REG_D6, value); break;
                    case "D7": _engine.RegWrite(M68k.UC_M68K_REG_D7, value); break;
                    
                    // Address registers
                    case "A0": _engine.RegWrite(M68k.UC_M68K_REG_A0, value); break;
                    case "A1": _engine.RegWrite(M68k.UC_M68K_REG_A1, value); break;
                    case "A2": _engine.RegWrite(M68k.UC_M68K_REG_A2, value); break;
                    case "A3": _engine.RegWrite(M68k.UC_M68K_REG_A3, value); break;
                    case "A4": _engine.RegWrite(M68k.UC_M68K_REG_A4, value); break;
                    case "A5": _engine.RegWrite(M68k.UC_M68K_REG_A5, value); break;
                    case "A6": _engine.RegWrite(M68k.UC_M68K_REG_A6, value); break;
                    case "A7":
                    case "USP": _engine.RegWrite(M68k.UC_M68K_REG_A7, value); break;
                    
                    // Special registers
                    case "PC": _engine.RegWrite(M68k.UC_M68K_REG_PC, value); break;
                    case "SR": _engine.RegWrite(M68k.UC_M68K_REG_SR, value); break;
                    
                    // CPU32 registers that aren't directly supported - store locally for now
                    case "SSP":
                    case "VBR":
                    case "SFC":
                    case "DFC":
                        // These would need CPU32 support or special handling
                        // For now, we'll just ignore them
                        break;
                        
                    default:
                        throw new ArgumentException($"Unknown register name: {registerName}");
                }

                LastException = null;
            }
            catch (Exception ex)
            {
                LastException = $"Failed to set register {registerName}: {ex.Message}";
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
                uint pc = (uint)_engine.RegRead(M68k.UC_M68K_REG_PC);
                
                // Check if PC points to mapped memory
                var region = FindMemoryRegion(pc);
                if (region == null)
                {
                    throw new InvalidOperationException($"PC (0x{pc:X8}) points to unmapped memory. No ROM/RAM loaded at this address.");
                }
                
                // Try to read the instruction bytes for debugging
                try
                {
                    var instructionBytes = ReadMemory(pc, 4); // Read up to 4 bytes
                    var hexBytes = string.Join(" ", instructionBytes.Select(b => b.ToString("X2")));
                    System.Diagnostics.Debug.WriteLine($"Attempting to execute instruction at 0x{pc:X8}: {hexBytes}");
                }
                catch (Exception readEx)
                {
                    throw new InvalidOperationException($"Cannot read instruction at PC (0x{pc:X8}): {readEx.Message}");
                }
                
                // Execute the instruction
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
                uint pc = (uint)_engine.RegRead(M68k.UC_M68K_REG_PC);
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
                // Check if address is in our mapped regions first
                bool isMapped = _memoryRegions.Any(r => r.ContainsAddress(address));
                
                // If we have a memory manager and the address isn't in our regions, check with it
                if (!isMapped && _memoryManager != null)
                {
                    if (!_memoryManager.IsAddressMapped(address))
                        throw new InvalidOperationException($"Address 0x{address:X8} is not mapped");
                }
                else if (!isMapped)
                {
                    throw new InvalidOperationException($"Address 0x{address:X8} is not mapped");
                }

                var data = new byte[size];
                _engine.MemRead(address, data);
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
                // If we have a memory manager, use it for validation
                if (_memoryManager != null)
                {
                    if (!_memoryManager.IsAddressMapped(address))
                        throw new InvalidOperationException($"Address 0x{address:X8} is not mapped");

                    if (!_memoryManager.CanWrite(address))
                        throw new InvalidOperationException($"Cannot write to address 0x{address:X8}");
                }

                _engine.MemWrite(address, data);
                
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
        /// Reads a byte from memory using the memory manager if available
        /// </summary>
        public byte ReadByte(uint address)
        {
            if (_memoryManager != null)
                return _memoryManager.ReadByte(address);
            
            return ReadMemory(address, 1)[0];
        }

        /// <summary>
        /// Writes a byte to memory using the memory manager if available
        /// </summary>
        public void WriteByte(uint address, byte value)
        {
            if (_memoryManager != null)
            {
                _memoryManager.WriteByte(address, value);
                return;
            }
            
            WriteMemory(address, new[] { value });
        }

        /// <summary>
        /// Reads a word (16-bit) from memory using the memory manager if available
        /// </summary>
        public ushort ReadWord(uint address)
        {
            if (_memoryManager != null)
                return _memoryManager.ReadWord(address);
            
            var data = ReadMemory(address, 2);
            return (ushort)((data[0] << 8) | data[1]);
        }

        /// <summary>
        /// Writes a word (16-bit) to memory using the memory manager if available
        /// </summary>
        public void WriteWord(uint address, ushort value)
        {
            if (_memoryManager != null)
            {
                _memoryManager.WriteWord(address, value);
                return;
            }
            
            WriteMemory(address, new[] { (byte)(value >> 8), (byte)(value & 0xFF) });
        }

        /// <summary>
        /// Reads a long (32-bit) from memory using the memory manager if available
        /// </summary>
        public uint ReadLong(uint address)
        {
            if (_memoryManager != null)
                return _memoryManager.ReadLong(address);
            
            var data = ReadMemory(address, 4);
            return (uint)((data[0] << 24) | (data[1] << 16) | (data[2] << 8) | data[3]);
        }

        /// <summary>
        /// Writes a long (32-bit) to memory using the memory manager if available
        /// </summary>
        public void WriteLong(uint address, uint value)
        {
            if (_memoryManager != null)
            {
                _memoryManager.WriteLong(address, value);
                return;
            }
            
            WriteMemory(address, new[] { 
                (byte)(value >> 24), 
                (byte)((value >> 16) & 0xFF), 
                (byte)((value >> 8) & 0xFF), 
                (byte)(value & 0xFF) 
            });
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
                    _engine.RegWrite(M68k.UC_M68K_REG_D0 + i, 0);
                    if (i < 7)
                        _engine.RegWrite(M68k.UC_M68K_REG_A0 + i, 0);
                }

                _engine.RegWrite(M68k.UC_M68K_REG_A7, 0);
                _engine.RegWrite(M68k.UC_M68K_REG_PC, 0);
                _engine.RegWrite(M68k.UC_M68K_REG_SR, 0x2700); // Supervisor mode, interrupts disabled

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
