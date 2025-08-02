# Support for dump files

## Context

Dump files contain much the same information as .LST files, but are easier to generate for custom code because the GNU toolchain contains support for generating them. For example:

`m68k-elf-objdump -D -x -r Kernel-P01.elf > Kernel-P01.dump.txt`

Therefore, the emulator app should support dump files in addition to LST files.

## Format

The interesting portion of the dump file follows a line that starts with "Disassembly of section"

After that line, there will be lines with a format like this:

two spaces, hexadecimal address, colon, tab, 1-3 byte pairs, variable number of spaces, assembly instructions

For example:

```
  ff8000:	4fef ffe8      	lea %sp@(-24),%sp
  ff8004:	007c 0700      	oriw #1792,%sr
  ff8008:	4eb9 00ff 8bdc 	jsr ff8bdc <ScratchWatchdog>
  ff800e:	203c 00ff f606 	movel #16774662,%d0
  ff8014:	2040           	moveal %d0,%a0
  ff8016:	4210           	clrb %a0@
```
## Design Notes

The UI needs "Load .dump.txt" and "Reload .dump.txt" buttons similar to the existing "Load .LST" and "Reload .LST" buttons.

The implementation of those buttons for dump files should be similar to the implementations of the existing buttons for LST files.

The AssemblyEntry class will need a new ParseDumpLine function, analogous to the existing ParseLstLine function.

The AssemblyEntry class should get a new HexHexBytes property to contain the string that will hold the raw bytes from the dump file. We'll add that to the UI later.

