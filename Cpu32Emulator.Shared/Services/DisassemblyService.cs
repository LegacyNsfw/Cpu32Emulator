using System;
using System.Collections.Generic;
using System.Linq;
using Cpu32Emulator.Models;

namespace Cpu32Emulator.Services
{
    /// <summary>
    /// Service for managing LST file parsing and disassembly display
    /// </summary>
    public class DisassemblyService
    {
        private List<LstEntry> _entries = new();
        private Dictionary<uint, LstEntry> _addressMap = new();
        private string? _loadedFilePath;

        /// <summary>
        /// Gets whether a disassembly file is loaded
        /// </summary>
        public bool IsLoaded => _entries.Count > 0;

        /// <summary>
        /// Gets the path of the currently loaded LST file
        /// </summary>
        public string? LoadedFilePath => _loadedFilePath;

        /// <summary>
        /// Gets all disassembly entries
        /// </summary>
        public IReadOnlyList<LstEntry> Entries => _entries.AsReadOnly();

        /// <summary>
        /// Gets the total number of entries
        /// </summary>
        public int EntryCount => _entries.Count;

        /// <summary>
        /// Loads disassembly entries from an LST file
        /// </summary>
        public void LoadEntries(List<LstEntry> entries, string filePath)
        {
            _entries = entries ?? throw new ArgumentNullException(nameof(entries));
            _loadedFilePath = filePath;
            RebuildAddressMap();
        }

        /// <summary>
        /// Clears all loaded entries
        /// </summary>
        public void Clear()
        {
            _entries.Clear();
            _addressMap.Clear();
            _loadedFilePath = null;
        }

        /// <summary>
        /// Finds an entry by address
        /// </summary>
        public LstEntry? FindEntryByAddress(uint address)
        {
            return _addressMap.TryGetValue(address, out var entry) ? entry : null;
        }

        /// <summary>
        /// Gets entries within a specific address range
        /// </summary>
        public List<LstEntry> GetEntriesInRange(uint startAddress, uint endAddress)
        {
            return _entries
                .Where(e => e.Address >= startAddress && e.Address <= endAddress)
                .OrderBy(e => e.Address)
                .ToList();
        }

        /// <summary>
        /// Gets entries around a specific address for display purposes
        /// </summary>
        public List<LstEntry> GetEntriesAroundAddress(uint address, int contextLines = 10)
        {
            var targetEntry = FindEntryByAddress(address);
            if (targetEntry == null)
            {
                // If exact address not found, find the closest one
                targetEntry = _entries
                    .Where(e => e.Address <= address)
                    .OrderByDescending(e => e.Address)
                    .FirstOrDefault();
            }

            if (targetEntry == null)
                return new List<LstEntry>();

            var targetIndex = _entries.IndexOf(targetEntry);
            var startIndex = Math.Max(0, targetIndex - contextLines);
            var endIndex = Math.Min(_entries.Count - 1, targetIndex + contextLines);
            var count = endIndex - startIndex + 1;

            return _entries.GetRange(startIndex, count);
        }

        /// <summary>
        /// Gets the next instruction entry after the given address
        /// </summary>
        public LstEntry? GetNextInstruction(uint address)
        {
            return _entries
                .Where(e => e.Address > address && e.IsInstruction)
                .OrderBy(e => e.Address)
                .FirstOrDefault();
        }

        /// <summary>
        /// Gets the previous instruction entry before the given address
        /// </summary>
        public LstEntry? GetPreviousInstruction(uint address)
        {
            return _entries
                .Where(e => e.Address < address && e.IsInstruction)
                .OrderByDescending(e => e.Address)
                .FirstOrDefault();
        }

        /// <summary>
        /// Finds all symbols (labels) in the disassembly
        /// </summary>
        public List<LstEntry> GetAllSymbols()
        {
            return _entries
                .Where(e => e.HasSymbol)
                .OrderBy(e => e.SymbolName)
                .ToList();
        }

        /// <summary>
        /// Finds a symbol by name
        /// </summary>
        public LstEntry? FindSymbol(string symbolName)
        {
            if (string.IsNullOrWhiteSpace(symbolName))
                return null;

            return _entries.FirstOrDefault(e => 
                string.Equals(e.SymbolName, symbolName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Gets entries for a specific segment
        /// </summary>
        public List<LstEntry> GetEntriesForSegment(string segmentName)
        {
            if (string.IsNullOrWhiteSpace(segmentName))
                return new List<LstEntry>();

            return _entries
                .Where(e => string.Equals(e.SegmentName, segmentName, StringComparison.OrdinalIgnoreCase))
                .OrderBy(e => e.Address)
                .ToList();
        }

        /// <summary>
        /// Gets all unique segment names
        /// </summary>
        public List<string> GetSegmentNames()
        {
            return _entries
                .Select(e => e.SegmentName)
                .Distinct()
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .OrderBy(s => s)
                .ToList();
        }

        /// <summary>
        /// Gets statistics about the loaded disassembly
        /// </summary>
        public DisassemblyStatistics GetStatistics()
        {
            if (!IsLoaded)
                return new DisassemblyStatistics();

            var instructions = _entries.Where(e => e.IsInstruction).Count();
            var symbols = _entries.Where(e => e.HasSymbol).Count();
            var segments = GetSegmentNames().Count;
            var addressRange = _entries.Count > 0 
                ? (_entries.Max(e => e.Address) - _entries.Min(e => e.Address))
                : 0;

            return new DisassemblyStatistics
            {
                TotalEntries = _entries.Count,
                InstructionCount = instructions,
                SymbolCount = symbols,
                SegmentCount = segments,
                AddressRange = addressRange,
                MinAddress = _entries.Count > 0 ? _entries.Min(e => e.Address) : 0,
                MaxAddress = _entries.Count > 0 ? _entries.Max(e => e.Address) : 0
            };
        }

        /// <summary>
        /// Validates that the entry addresses are properly sorted
        /// </summary>
        public bool ValidateAddressOrder()
        {
            for (int i = 1; i < _entries.Count; i++)
            {
                if (_entries[i].Address < _entries[i - 1].Address)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Sorts entries by address if they're not already sorted
        /// </summary>
        public void EnsureSortedByAddress()
        {
            if (!ValidateAddressOrder())
            {
                _entries.Sort((a, b) => a.Address.CompareTo(b.Address));
                RebuildAddressMap();
            }
        }

        /// <summary>
        /// Rebuilds the internal address-to-entry mapping
        /// </summary>
        private void RebuildAddressMap()
        {
            _addressMap.Clear();
            foreach (var entry in _entries)
            {
                // Use the first entry for duplicate addresses
                if (!_addressMap.ContainsKey(entry.Address))
                {
                    _addressMap[entry.Address] = entry;
                }
            }
        }

        /// <summary>
        /// Gets a page of entries for display purposes
        /// </summary>
        public List<LstEntry> GetPage(int pageIndex, int pageSize)
        {
            if (pageIndex < 0 || pageSize <= 0)
                return new List<LstEntry>();

            var startIndex = pageIndex * pageSize;
            if (startIndex >= _entries.Count)
                return new List<LstEntry>();

            var count = Math.Min(pageSize, _entries.Count - startIndex);
            return _entries.GetRange(startIndex, count);
        }

        /// <summary>
        /// Calculates the total number of pages for the given page size
        /// </summary>
        public int GetPageCount(int pageSize)
        {
            if (pageSize <= 0 || _entries.Count == 0)
                return 0;

            return (int)Math.Ceiling((double)_entries.Count / pageSize);
        }
    }

    /// <summary>
    /// Statistics about a loaded disassembly
    /// </summary>
    public class DisassemblyStatistics
    {
        public int TotalEntries { get; set; }
        public int InstructionCount { get; set; }
        public int SymbolCount { get; set; }
        public int SegmentCount { get; set; }
        public uint AddressRange { get; set; }
        public uint MinAddress { get; set; }
        public uint MaxAddress { get; set; }

        public override string ToString()
        {
            return $"Entries: {TotalEntries}, Instructions: {InstructionCount}, Symbols: {SymbolCount}, " +
                   $"Segments: {SegmentCount}, Address Range: 0x{MinAddress:X8}-0x{MaxAddress:X8}";
        }
    }
}
