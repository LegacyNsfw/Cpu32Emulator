using System;

namespace Cpu32Emulator.Models
{
    /// <summary>
    /// Represents a memory region (ROM or RAM) loaded in the emulator
    /// </summary>
    public class MemoryRegion
    {
        public string FilePath { get; set; } = string.Empty;
        public uint BaseAddress { get; set; }
        public uint Size { get; set; }
        public byte[] Data { get; set; } = Array.Empty<byte>();
        public MemoryRegionType Type { get; set; }
        public DateTime LoadedAt { get; set; }

        /// <summary>
        /// Gets the end address of this memory region (exclusive)
        /// </summary>
        public uint EndAddress => BaseAddress + Size;

        /// <summary>
        /// Checks if the given address is within this memory region
        /// </summary>
        public bool ContainsAddress(uint address)
        {
            return address >= BaseAddress && address < EndAddress;
        }

        /// <summary>
        /// Gets the offset within this region for the given address
        /// </summary>
        public uint GetOffset(uint address)
        {
            if (!ContainsAddress(address))
                throw new ArgumentOutOfRangeException(nameof(address), $"Address 0x{address:X8} is not within this memory region (0x{BaseAddress:X8}-0x{EndAddress:X8})");
            
            return address - BaseAddress;
        }

        /// <summary>
        /// Reads a byte from the specified address within this region
        /// </summary>
        public byte ReadByte(uint address)
        {
            uint offset = GetOffset(address);
            if (offset >= Data.Length)
                throw new ArgumentOutOfRangeException(nameof(address), "Address is beyond the loaded data");
            
            return Data[offset];
        }

        /// <summary>
        /// Writes a byte to the specified address within this region
        /// </summary>
        public void WriteByte(uint address, byte value)
        {
            if (Type == MemoryRegionType.ROM)
                throw new InvalidOperationException("Cannot write to ROM memory region");

            uint offset = GetOffset(address);
            if (offset >= Data.Length)
                throw new ArgumentOutOfRangeException(nameof(address), "Address is beyond the loaded data");
            
            Data[offset] = value;
        }

        /// <summary>
        /// Reads a word (16-bit) from the specified address within this region
        /// </summary>
        public ushort ReadWord(uint address)
        {
            byte high = ReadByte(address);
            byte low = ReadByte(address + 1);
            return (ushort)((high << 8) | low);
        }

        /// <summary>
        /// Writes a word (16-bit) to the specified address within this region
        /// </summary>
        public void WriteWord(uint address, ushort value)
        {
            WriteByte(address, (byte)(value >> 8));
            WriteByte(address + 1, (byte)(value & 0xFF));
        }

        /// <summary>
        /// Reads a long (32-bit) from the specified address within this region
        /// </summary>
        public uint ReadLong(uint address)
        {
            ushort high = ReadWord(address);
            ushort low = ReadWord(address + 2);
            return (uint)((high << 16) | low);
        }

        /// <summary>
        /// Writes a long (32-bit) to the specified address within this region
        /// </summary>
        public void WriteLong(uint address, uint value)
        {
            WriteWord(address, (ushort)(value >> 16));
            WriteWord(address + 2, (ushort)(value & 0xFFFF));
        }

        /// <summary>
        /// Creates a new memory region from a file
        /// </summary>
        public static MemoryRegion FromFile(string filePath, uint baseAddress, MemoryRegionType type)
        {
            if (!System.IO.File.Exists(filePath))
                throw new System.IO.FileNotFoundException($"Memory file not found: {filePath}");

            byte[] data = System.IO.File.ReadAllBytes(filePath);
            
            return new MemoryRegion
            {
                FilePath = filePath,
                BaseAddress = baseAddress,
                Size = (uint)data.Length,
                Data = data,
                Type = type,
                LoadedAt = DateTime.Now
            };
        }

        public override string ToString()
        {
            return $"{Type} Region: 0x{BaseAddress:X8}-0x{EndAddress:X8} ({Size} bytes) from {System.IO.Path.GetFileName(FilePath)}";
        }
    }

    /// <summary>
    /// Represents the type of memory region
    /// </summary>
    public enum MemoryRegionType
    {
        ROM,
        RAM
    }
}
