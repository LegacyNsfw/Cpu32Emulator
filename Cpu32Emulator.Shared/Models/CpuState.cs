using System;

namespace Cpu32Emulator.Models
{
    /// <summary>
    /// Represents the state of the 68K CPU registers
    /// </summary>
    public class CpuState
    {
        // Data registers D0-D7
        public uint D0 { get; set; }
        public uint D1 { get; set; }
        public uint D2 { get; set; }
        public uint D3 { get; set; }
        public uint D4 { get; set; }
        public uint D5 { get; set; }
        public uint D6 { get; set; }
        public uint D7 { get; set; }

        // Address registers A0-A6
        public uint A0 { get; set; }
        public uint A1 { get; set; }
        public uint A2 { get; set; }
        public uint A3 { get; set; }
        public uint A4 { get; set; }
        public uint A5 { get; set; }
        public uint A6 { get; set; }

        // Special registers
        public uint USP { get; set; }  // User Stack Pointer (A7)
        public uint PC { get; set; }   // Program Counter
        public uint CCR { get; set; }  // Condition Code Register
        public uint SSP { get; set; }  // Supervisor Stack Pointer
        public uint SR { get; set; }   // Status Register
        public uint VBR { get; set; }  // Vector Base Register
        public uint SFC { get; set; }  // Source Function Code
        public uint DFC { get; set; }  // Destination Function Code

        /// <summary>
        /// Gets an array of all data register values
        /// </summary>
        public uint[] DataRegisters => new uint[] { D0, D1, D2, D3, D4, D5, D6, D7 };

        /// <summary>
        /// Gets an array of all address register values (A0-A6, excluding USP)
        /// </summary>
        public uint[] AddressRegisters => new uint[] { A0, A1, A2, A3, A4, A5, A6 };

        /// <summary>
        /// Sets a data register value by index (0-7)
        /// </summary>
        public void SetDataRegister(int index, uint value)
        {
            switch (index)
            {
                case 0: D0 = value; break;
                case 1: D1 = value; break;
                case 2: D2 = value; break;
                case 3: D3 = value; break;
                case 4: D4 = value; break;
                case 5: D5 = value; break;
                case 6: D6 = value; break;
                case 7: D7 = value; break;
                default: throw new ArgumentOutOfRangeException(nameof(index), "Data register index must be 0-7");
            }
        }

        /// <summary>
        /// Gets a data register value by index (0-7)
        /// </summary>
        public uint GetDataRegister(int index)
        {
            return index switch
            {
                0 => D0,
                1 => D1,
                2 => D2,
                3 => D3,
                4 => D4,
                5 => D5,
                6 => D6,
                7 => D7,
                _ => throw new ArgumentOutOfRangeException(nameof(index), "Data register index must be 0-7")
            };
        }

        /// <summary>
        /// Sets an address register value by index (0-6, excluding USP)
        /// </summary>
        public void SetAddressRegister(int index, uint value)
        {
            switch (index)
            {
                case 0: A0 = value; break;
                case 1: A1 = value; break;
                case 2: A2 = value; break;
                case 3: A3 = value; break;
                case 4: A4 = value; break;
                case 5: A5 = value; break;
                case 6: A6 = value; break;
                default: throw new ArgumentOutOfRangeException(nameof(index), "Address register index must be 0-6");
            }
        }

        /// <summary>
        /// Gets an address register value by index (0-6, excluding USP)
        /// </summary>
        public uint GetAddressRegister(int index)
        {
            return index switch
            {
                0 => A0,
                1 => A1,
                2 => A2,
                3 => A3,
                4 => A4,
                5 => A5,
                6 => A6,
                _ => throw new ArgumentOutOfRangeException(nameof(index), "Address register index must be 0-6")
            };
        }

        /// <summary>
        /// Creates a deep copy of the CPU state
        /// </summary>
        public CpuState Clone()
        {
            return new CpuState
            {
                D0 = D0, D1 = D1, D2 = D2, D3 = D3, D4 = D4, D5 = D5, D6 = D6, D7 = D7,
                A0 = A0, A1 = A1, A2 = A2, A3 = A3, A4 = A4, A5 = A5, A6 = A6,
                USP = USP, PC = PC, CCR = CCR, SSP = SSP, SR = SR, VBR = VBR, SFC = SFC, DFC = DFC
            };
        }
    }
}
