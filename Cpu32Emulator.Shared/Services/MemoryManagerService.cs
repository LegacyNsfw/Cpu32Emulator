using System;
using System.Collections.Generic;
using System.Linq;
using Cpu32Emulator.Models;

namespace Cpu32Emulator.Services
{
    /// <summary>
    /// Service for managing memory regions and providing unified memory access
    /// </summary>
    public class MemoryManagerService
    {
        private readonly Dictionary<uint, MemoryRegion> _regions = new();
        private readonly List<MemoryRegion> _regionList = new();

        /// <summary>
        /// Event raised when a memory region is added or removed
        /// </summary>
        public event EventHandler<MemoryRegionChangedEventArgs>? MemoryRegionChanged;

        /// <summary>
        /// Gets all mapped memory regions
        /// </summary>
        public IReadOnlyList<MemoryRegion> Regions => _regionList.AsReadOnly();

        /// <summary>
        /// Gets the total number of mapped regions
        /// </summary>
        public int RegionCount => _regionList.Count;

        /// <summary>
        /// Adds a memory region to the manager
        /// </summary>
        public void AddRegion(MemoryRegion region)
        {
            if (region == null)
                throw new ArgumentNullException(nameof(region));

            // Check for overlapping regions
            var overlapping = _regionList.FirstOrDefault(r => 
                (region.BaseAddress < r.EndAddress && region.EndAddress > r.BaseAddress));
            
            if (overlapping != null)
            {
                throw new InvalidOperationException(
                    $"Memory region {region} overlaps with existing region {overlapping}");
            }

            _regions[region.BaseAddress] = region;
            _regionList.Add(region);
            _regionList.Sort((a, b) => a.BaseAddress.CompareTo(b.BaseAddress));

            OnMemoryRegionChanged(new MemoryRegionChangedEventArgs(MemoryRegionChangeType.Added, region));
        }

        /// <summary>
        /// Removes a memory region from the manager
        /// </summary>
        public bool RemoveRegion(uint baseAddress)
        {
            if (_regions.TryGetValue(baseAddress, out var region))
            {
                _regions.Remove(baseAddress);
                _regionList.Remove(region);
                OnMemoryRegionChanged(new MemoryRegionChangedEventArgs(MemoryRegionChangeType.Removed, region));
                return true;
            }
            return false;
        }

        /// <summary>
        /// Clears all memory regions
        /// </summary>
        public void ClearRegions()
        {
            var regionsToRemove = _regionList.ToList();
            _regions.Clear();
            _regionList.Clear();

            foreach (var region in regionsToRemove)
            {
                OnMemoryRegionChanged(new MemoryRegionChangedEventArgs(MemoryRegionChangeType.Removed, region));
            }
        }

        /// <summary>
        /// Finds the memory region containing the specified address
        /// </summary>
        public MemoryRegion? FindRegionContaining(uint address)
        {
            return _regionList.FirstOrDefault(r => r.ContainsAddress(address));
        }

        /// <summary>
        /// Checks if an address is mapped to any region
        /// </summary>
        public bool IsAddressMapped(uint address)
        {
            return FindRegionContaining(address) != null;
        }

        /// <summary>
        /// Reads a byte from memory
        /// </summary>
        public byte ReadByte(uint address)
        {
            var region = FindRegionContaining(address);
            if (region == null)
                throw new InvalidOperationException($"Address 0x{address:X8} is not mapped");

            return region.ReadByte(address);
        }

        /// <summary>
        /// Writes a byte to memory
        /// </summary>
        public void WriteByte(uint address, byte value)
        {
            var region = FindRegionContaining(address);
            if (region == null)
                throw new InvalidOperationException($"Address 0x{address:X8} is not mapped");

            region.WriteByte(address, value);
        }

        /// <summary>
        /// Reads a word (16-bit) from memory
        /// </summary>
        public ushort ReadWord(uint address)
        {
            var region = FindRegionContaining(address);
            if (region == null)
                throw new InvalidOperationException($"Address 0x{address:X8} is not mapped");

            // Check if the entire word is within the same region
            if (!region.ContainsAddress(address + 1))
                throw new InvalidOperationException($"Word at 0x{address:X8} spans multiple regions");

            return region.ReadWord(address);
        }

        /// <summary>
        /// Writes a word (16-bit) to memory
        /// </summary>
        public void WriteWord(uint address, ushort value)
        {
            var region = FindRegionContaining(address);
            if (region == null)
                throw new InvalidOperationException($"Address 0x{address:X8} is not mapped");

            // Check if the entire word is within the same region
            if (!region.ContainsAddress(address + 1))
                throw new InvalidOperationException($"Word at 0x{address:X8} spans multiple regions");

            region.WriteWord(address, value);
        }

        /// <summary>
        /// Reads a long (32-bit) from memory
        /// </summary>
        public uint ReadLong(uint address)
        {
            var region = FindRegionContaining(address);
            if (region == null)
                throw new InvalidOperationException($"Address 0x{address:X8} is not mapped");

            // Check if the entire long is within the same region
            if (!region.ContainsAddress(address + 3))
                throw new InvalidOperationException($"Long at 0x{address:X8} spans multiple regions");

            return region.ReadLong(address);
        }

        /// <summary>
        /// Writes a long (32-bit) to memory
        /// </summary>
        public void WriteLong(uint address, uint value)
        {
            var region = FindRegionContaining(address);
            if (region == null)
                throw new InvalidOperationException($"Address 0x{address:X8} is not mapped");

            // Check if the entire long is within the same region
            if (!region.ContainsAddress(address + 3))
                throw new InvalidOperationException($"Long at 0x{address:X8} spans multiple regions");

            region.WriteLong(address, value);
        }

        /// <summary>
        /// Reads a range of bytes from memory
        /// </summary>
        public byte[] ReadBytes(uint address, int count)
        {
            if (count <= 0)
                throw new ArgumentException("Count must be positive", nameof(count));

            var result = new byte[count];
            for (int i = 0; i < count; i++)
            {
                result[i] = ReadByte(address + (uint)i);
            }
            return result;
        }

        /// <summary>
        /// Writes a range of bytes to memory
        /// </summary>
        public void WriteBytes(uint address, byte[] data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            for (int i = 0; i < data.Length; i++)
            {
                WriteByte(address + (uint)i, data[i]);
            }
        }

        /// <summary>
        /// Gets memory statistics
        /// </summary>
        public MemoryStatistics GetStatistics()
        {
            if (_regionList.Count == 0)
                return new MemoryStatistics();

            var romRegions = _regionList.Where(r => r.Type == MemoryRegionType.ROM).ToList();
            var ramRegions = _regionList.Where(r => r.Type == MemoryRegionType.RAM).ToList();

            return new MemoryStatistics
            {
                TotalRegions = _regionList.Count,
                RomRegions = romRegions.Count,
                RamRegions = ramRegions.Count,
                TotalRomSize = romRegions.Sum(r => r.Size),
                TotalRamSize = ramRegions.Sum(r => r.Size),
                LowestAddress = _regionList.Min(r => r.BaseAddress),
                HighestAddress = _regionList.Max(r => r.EndAddress) - 1,
                TotalAddressSpaceUsed = _regionList.Sum(r => r.Size)
            };
        }

        /// <summary>
        /// Validates memory region integrity
        /// </summary>
        public List<string> ValidateMemoryIntegrity()
        {
            var errors = new List<string>();

            // Check for overlapping regions
            for (int i = 0; i < _regionList.Count; i++)
            {
                for (int j = i + 1; j < _regionList.Count; j++)
                {
                    var region1 = _regionList[i];
                    var region2 = _regionList[j];

                    if (region1.BaseAddress < region2.EndAddress && region1.EndAddress > region2.BaseAddress)
                    {
                        errors.Add($"Regions overlap: {region1} and {region2}");
                    }
                }
            }

            // Check for empty regions
            foreach (var region in _regionList)
            {
                if (region.Size == 0)
                {
                    errors.Add($"Empty region: {region}");
                }

                if (region.Data.Length != region.Size)
                {
                    errors.Add($"Region data size mismatch: {region}");
                }
            }

            return errors;
        }

        /// <summary>
        /// Gets a memory map representation
        /// </summary>
        public List<MemoryMapEntry> GetMemoryMap()
        {
            var map = new List<MemoryMapEntry>();

            foreach (var region in _regionList)
            {
                map.Add(new MemoryMapEntry
                {
                    StartAddress = region.BaseAddress,
                    EndAddress = region.EndAddress - 1,
                    Size = region.Size,
                    Type = region.Type,
                    Description = $"{region.Type} - {System.IO.Path.GetFileName(region.FilePath)}"
                });
            }

            return map;
        }

        /// <summary>
        /// Raises the MemoryRegionChanged event
        /// </summary>
        protected virtual void OnMemoryRegionChanged(MemoryRegionChangedEventArgs e)
        {
            MemoryRegionChanged?.Invoke(this, e);
        }
    }

    /// <summary>
    /// Event arguments for memory region changes
    /// </summary>
    public class MemoryRegionChangedEventArgs : EventArgs
    {
        public MemoryRegionChangeType ChangeType { get; }
        public MemoryRegion Region { get; }

        public MemoryRegionChangedEventArgs(MemoryRegionChangeType changeType, MemoryRegion region)
        {
            ChangeType = changeType;
            Region = region ?? throw new ArgumentNullException(nameof(region));
        }
    }

    /// <summary>
    /// Types of memory region changes
    /// </summary>
    public enum MemoryRegionChangeType
    {
        Added,
        Removed
    }

    /// <summary>
    /// Memory statistics
    /// </summary>
    public class MemoryStatistics
    {
        public int TotalRegions { get; set; }
        public int RomRegions { get; set; }
        public int RamRegions { get; set; }
        public uint TotalRomSize { get; set; }
        public uint TotalRamSize { get; set; }
        public uint LowestAddress { get; set; }
        public uint HighestAddress { get; set; }
        public uint TotalAddressSpaceUsed { get; set; }

        public override string ToString()
        {
            return $"Regions: {TotalRegions} (ROM: {RomRegions}, RAM: {RamRegions}), " +
                   $"Address Range: 0x{LowestAddress:X8}-0x{HighestAddress:X8}, " +
                   $"Total Size: {TotalAddressSpaceUsed} bytes";
        }
    }

    /// <summary>
    /// Memory map entry
    /// </summary>
    public class MemoryMapEntry
    {
        public uint StartAddress { get; set; }
        public uint EndAddress { get; set; }
        public uint Size { get; set; }
        public MemoryRegionType Type { get; set; }
        public string Description { get; set; } = string.Empty;

        public override string ToString()
        {
            return $"0x{StartAddress:X8}-0x{EndAddress:X8} ({Size} bytes) - {Description}";
        }
    }
}
