using System;

namespace Cpu32Emulator.Models
{
    /// <summary>
    /// Represents a watched memory location in the memory viewer
    /// </summary>
    public class WatchedMemory
    {
        public uint Address { get; set; }
        public DataWidth Width { get; set; }
        public string Label { get; set; } = string.Empty;
        public bool IsEditable { get; set; }

        /// <summary>
        /// Gets the size in bytes for this data width
        /// </summary>
        public int SizeInBytes => Width switch
        {
            DataWidth.Byte => 1,
            DataWidth.Word => 2,
            DataWidth.Long => 4,
            _ => throw new ArgumentOutOfRangeException()
        };

        /// <summary>
        /// Gets the display format string for this data width
        /// </summary>
        public string FormatString => Width switch
        {
            DataWidth.Byte => "X2",
            DataWidth.Word => "X4",
            DataWidth.Long => "X8",
            _ => throw new ArgumentOutOfRangeException()
        };

        /// <summary>
        /// Creates a new watched memory entry
        /// </summary>
        public static WatchedMemory Create(uint address, DataWidth width, string label = "")
        {
            return new WatchedMemory
            {
                Address = address,
                Width = width,
                Label = string.IsNullOrEmpty(label) ? $"0x{address:X8}" : label,
                IsEditable = true
            };
        }

        /// <summary>
        /// Creates a special RESET pseudo-address entry
        /// </summary>
        public static WatchedMemory CreateResetEntry(uint resetAddress)
        {
            return new WatchedMemory
            {
                Address = resetAddress,
                Width = DataWidth.Long,
                Label = "RESET",
                IsEditable = true
            };
        }

        public override string ToString()
        {
            return $"{Label}: 0x{Address:X8} ({Width})";
        }

        public override bool Equals(object? obj)
        {
            return obj is WatchedMemory other && 
                   Address == other.Address && 
                   Width == other.Width;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Address, Width);
        }
    }

    /// <summary>
    /// Represents the data width for memory operations
    /// </summary>
    public enum DataWidth
    {
        Byte,
        Word,
        Long
    }
}
