using System;

namespace Cpu32Emulator.Models
{
    /// <summary>
    /// Represents a single entry from an LST or dump file
    /// </summary>
    public class AssemblyEntry
    {
        public string SegmentName { get; set; } = string.Empty;
        public uint Address { get; set; }
        public string? SymbolName { get; set; }
        public string Instruction { get; set; } = string.Empty;
        public int LineNumber { get; set; }
        
        /// <summary>
        /// Raw hex bytes from dump file (e.g., "4fef ffe8")
        /// </summary>
        public string? HexBytes { get; set; }

        /// <summary>
        /// Gets whether this entry has a symbol
        /// </summary>
        public bool HasSymbol => !string.IsNullOrWhiteSpace(SymbolName);

        /// <summary>
        /// Gets the display text for this entry
        /// </summary>
        public string DisplayText
        {
            get
            {
                var parts = new System.Collections.Generic.List<string>();
                
                if (HasSymbol)
                    parts.Add($"{SymbolName}:");
                
                parts.Add($"0x{Address:X8}");
                
                if (!string.IsNullOrWhiteSpace(Instruction))
                    parts.Add(Instruction);
                
                return string.Join(" ", parts);
            }
        }

        /// <summary>
        /// Parses an LST file line into an LstEntry
        /// Format: segment:address\tsymbol\tinstruction
        /// </summary>
        public static AssemblyEntry? ParseLstLine(string line, int lineNumber)
        {
            if (string.IsNullOrWhiteSpace(line))
                return null;

            try
            {
                // Split by colon first to get segment and rest
                var colonIndex = line.IndexOf(':');
                if (colonIndex == -1)
                    return null;

                var segmentName = line.Substring(0, colonIndex).Trim();
                var remainder = line.Substring(colonIndex + 1);

                // Split remainder by tabs
                var parts = remainder.Split('\t');
                if (parts.Length < 1)
                    return null;

                // Parse address (should be hex)
                var addressText = parts[0].Trim();
                if (!uint.TryParse(addressText, System.Globalization.NumberStyles.HexNumber, null, out uint address))
                    return null;

                var entry = new AssemblyEntry
                {
                    SegmentName = segmentName,
                    Address = address,
                    LineNumber = lineNumber
                };

                // Parse symbol (optional)
                if (parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1]))
                {
                    entry.SymbolName = parts[1].Trim();
                }

                // Parse instruction/comment (optional)
                if (parts.Length > 2 && !string.IsNullOrWhiteSpace(parts[2]))
                {
                    entry.Instruction = parts[2].Trim();
                }

                return entry;
            }
            catch
            {
                // If parsing fails, return null
                return null;
            }
        }

        /// <summary>
        /// Parses a dump file line into an AssemblyEntry
        /// Format: "  address:\thex_bytes\tinstruction"
        /// Example: "  ff8000:\t4fef ffe8      \tlea %sp@(-24),%sp"
        /// </summary>
        public static AssemblyEntry? ParseDumpLine(string line, int lineNumber, string currentSection = "")
        {
            if (string.IsNullOrWhiteSpace(line))
                return null;

            try
            {
                // Skip lines that don't start with spaces and hex address
                if (!line.StartsWith("  "))
                    return null;

                // Find the colon that separates address from the rest
                var colonIndex = line.IndexOf(':');
                if (colonIndex == -1)
                    return null;

                // Extract address (remove leading spaces)
                var addressText = line.Substring(0, colonIndex).Trim();
                if (!uint.TryParse(addressText, System.Globalization.NumberStyles.HexNumber, null, out uint address))
                    return null;

                // Get the rest after the colon
                var remainder = line.Substring(colonIndex + 1);
                
                // Split by tab to separate hex bytes from instruction
                var tabIndex = remainder.IndexOf('\t', 1); // Start from 1 to skip the first tab
                if (tabIndex == -1)
                    return null;

                // Extract hex bytes (between first tab and second tab)
                var hexBytes = remainder.Substring(0, tabIndex).Trim();
                
                // Extract instruction (after the hex bytes and variable spaces)
                var instruction = remainder.Substring(tabIndex + 1).Trim();

                var entry = new AssemblyEntry
                {
                    SegmentName = currentSection,
                    Address = address,
                    LineNumber = lineNumber,
                    HexBytes = hexBytes,
                    Instruction = instruction
                };

                // Check if the instruction contains a symbol reference (e.g., "jsr ff8bdc <ScratchWatchdog>")
                var symbolMatch = System.Text.RegularExpressions.Regex.Match(instruction, @"<([^>]+)>");
                if (symbolMatch.Success)
                {
                    entry.SymbolName = symbolMatch.Groups[1].Value;
                }

                return entry;
            }
            catch
            {
                // If parsing fails, return null
                return null;
            }
        }

        /// <summary>
        /// Checks if this entry represents a code instruction (vs data or comment)
        /// </summary>
        public bool IsInstruction
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Instruction))
                    return false;

                var instr = Instruction.Trim().ToUpperInvariant();
                
                // Simple heuristic: if it starts with common 68K mnemonics, it's an instruction
                return instr.StartsWith("MOVE") || instr.StartsWith("ADD") || instr.StartsWith("SUB") ||
                       instr.StartsWith("CMP") || instr.StartsWith("JMP") || instr.StartsWith("JSR") ||
                       instr.StartsWith("BRA") || instr.StartsWith("BEQ") || instr.StartsWith("BNE") ||
                       instr.StartsWith("BSR") || instr.StartsWith("RTS") || instr.StartsWith("NOP") ||
                       instr.StartsWith("CLR") || instr.StartsWith("TST") || instr.StartsWith("LEA") ||
                       instr.StartsWith("PEA") || instr.StartsWith("PUSH") || instr.StartsWith("POP") ||
                       instr.StartsWith("AND") || instr.StartsWith("OR") || instr.StartsWith("EOR") ||
                       instr.StartsWith("NOT") || instr.StartsWith("NEG") || instr.StartsWith("LSL") ||
                       instr.StartsWith("LSR") || instr.StartsWith("ASL") || instr.StartsWith("ASR") ||
                       instr.StartsWith("ROL") || instr.StartsWith("ROR") || instr.StartsWith("BTST") ||
                       instr.StartsWith("BSET") || instr.StartsWith("BCLR") || instr.StartsWith("BCHG");
            }
        }

        public override string ToString()
        {
            return DisplayText;
        }

        public override bool Equals(object? obj)
        {
            return obj is AssemblyEntry other && 
                   SegmentName == other.SegmentName && 
                   Address == other.Address;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(SegmentName, Address);
        }
    }
}
