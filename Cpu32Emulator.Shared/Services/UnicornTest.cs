using UnicornEngine;
using UnicornEngine.Const;

public class UnicornTest
{
    public static void TestUnicorn()
    {
        byte[] code = File.ReadAllBytes(@"\GitHub\Cpu32Emulator\Cpu32Emulator.Tests\test-data\example.ram.bin");
        using (var uc = new Unicorn(Common.UC_ARCH_M68K, Common.UC_MODE_BIG_ENDIAN))
        {
            uc.AddCodeHook((uc, addr, size, userData) =>
            {
                string logMessage = string.Format("Executing {0} bytes at {1:X} (userData: {2})", size, addr, userData);
                Console.WriteLine(logMessage);
            }, 1, 0);

            uc.AddInterruptHook((uc, intNo, userData) =>
            {
                string logMessage = string.Format("Interrupt {0} triggered (userData: {1})", intNo, userData);
                Console.WriteLine(logMessage);
            });

            uc.AddSyscallHook((uc, userData) =>
            {
                string logMessage = string.Format("Syscall called (userData: {0})", userData);
                Console.WriteLine(logMessage);
            });

            uc.AddEventMemHook((Unicorn u, int eventType, long address, int size, long value, object userData) =>
            {
                string logMessage = string.Format("Memory event {0} at {1:X} with size {2} and value {3} (userData: {4})", eventType, address, size, value, userData);
                Console.WriteLine(logMessage);
                return true;
            }, Common.UC_HOOK_MEM_READ_UNMAPPED | Common.UC_HOOK_MEM_WRITE_UNMAPPED);

            // Write data registers
            for (int i = 0; i < 8; i++)
            {
                uc.RegWrite(M68k.UC_M68K_REG_D0 + i, 0);
            }

            // Write address registers
            for (int i = 0; i < 7; i++)
            {
                uc.RegWrite(M68k.UC_M68K_REG_A0 + i, 0);
            }

            // Write special registers
            uc.RegWrite(M68k.UC_M68K_REG_PC, 0x1000);
            uc.RegWrite(M68k.UC_M68K_REG_SR, 0);

            uc.MemMap(0xFF0000, code.Length, Common.UC_PROT_ALL);
            uc.MemWrite(0xFF0000, code);

            uc.RegWrite(M68k.UC_M68K_REG_PC, 0xFF8000);

            try
            {
                uc.EmuStart(0xFF8000, 0xFFFFFF, 0, 1);
                long pc = uc.RegRead(M68k.UC_M68K_REG_PC);
                uc.EmuStart(pc, 0xFFFFFF, 0, 1);
                pc = uc.RegRead(M68k.UC_M68K_REG_PC);
                uc.EmuStart(pc, 0xFFFFFF, 0, 1);
                pc = uc.RegRead(M68k.UC_M68K_REG_PC);
                uc.EmuStart(pc, 0xFFFFFF, 0, 1);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception: {ex.ToString()}");
            }
        }
    }
}
