using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using Cpu32Emulator.Models;

namespace Cpu32Emulator.Models
{
    /// <summary>
    /// Represents a rendered tile containing a section of disassembly instructions
    /// </summary>
    public class DisassemblyTile
    {
        /// <summary>
        /// The rendered image of this tile (legacy bitmap approach)
        /// </summary>
        public WriteableBitmap? TileImage { get; set; }

        /// <summary>
        /// Phase 3: The rendered XAML element of this tile (preferred approach)
        /// </summary>
        public FrameworkElement? TileElement { get; set; }

        /// <summary>
        /// The first instruction address in this tile
        /// </summary>
        public uint StartAddress { get; set; }

        /// <summary>
        /// The last instruction address in this tile
        /// </summary>
        public uint EndAddress { get; set; }

        /// <summary>
        /// Maps instruction addresses to their Y coordinates within the tile
        /// Used for precise scrolling calculations
        /// </summary>
        public Dictionary<uint, double> AddressYCoordinates { get; set; } = new();

        /// <summary>
        /// The total height of this tile in pixels
        /// </summary>
        public int TileHeight { get; set; }

        /// <summary>
        /// When this tile was last accessed, for LRU cache management
        /// </summary>
        public DateTime LastAccessed { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// The source entries used to generate this tile
        /// </summary>
        public List<LstEntry> SourceEntries { get; set; } = new();

        /// <summary>
        /// Gets whether this tile contains the specified address
        /// </summary>
        public bool ContainsAddress(uint address)
        {
            return address >= StartAddress && address <= EndAddress;
        }

        /// <summary>
        /// Updates the last accessed time for LRU tracking
        /// </summary>
        public void Touch()
        {
            LastAccessed = DateTime.UtcNow;
        }
    }
}
