# CPU32 Emulator Requirements

## Overview

This product enables the user to load an Motorola 68330 or 68332 firmware image file (with a .bin extension), and optional a RAM image file (with a .ram.bin extension). The user can then choose an address at which to begin execution, and then they can step through the code, one instruction at a time.

## User Interface

The UI consists primarily of four elements:

### Top Pane

The top pane of the application shows the disassembly window. This pane automatically resizes with the application window, to make the disassembly window as large as possible while keeping the contents of the register pane tall enough to show all registers.

The first release of the application, which we're working on now, does not support syntax highlighting, but that may be added in a future version.

### Bottom Left - Register Pane

The left side of the bottom half of the application shows rows of CPU registers, with the register name on the left and the register value on the right. 

Double-clicking on a register allows the user to enter the value for that register, in hex or decimal. When the user edits the PC register, the disassembly window jumps to that location. The

The registers are:

* D0-D7
* A0-A6
* USP (User Stack Pointer), also known as A7
* PC (Program Counter)
* CCR (Condition Code Register)
* SSP (Supervisor Stack Pointer)
* SR (Status Register)
* VBR (Vector Base Register)
* SFC
* DFC

Registers are displayed in hexadecimal. 

### Bottom Right - Memory Pane

This pane is analogous to the "watch" window in a debugging IDE.

The right side of the bottom half of the application shows RAM, also in rows, with the address on the left and the value on the right. 

Double-clicking or double-tapping on the left side of a row brings up a dialog box that allows the user to specify the address to watch, and the width of the data to watch (byte, word, or long). 

Double-clicking or double-tapping on the right side of a row brings up a dialog box that allows the user to enter the value to store at the corresponding address (limited by the data width chosen in the previously mentioned dialog box.)

If an address is in the memory range of a RAM file, the user will be able to edit the value. If an address is in the memory range of a ROM file, the value will not be editable.

Values in memory are displayed in hexadecimal. 

The top row in this pane will have the pseudo-address "RESET" and the user will be able to edit this value. Double-clicking on the left side will set the PC register to this address.

### Status Bar

A status bar will be located at the bottom edge. If an exception happens, it will turn red and will display the name of the exception.

### Menu Bar

The left edge of the application shows a collapsible vertical navigation bar, with the following elements:

#### File

When this is clicked, a menu slides out from the left, which allows the user to:
* Create a new project
* Load a project from a file
* Save the current project to the file from which it was loaded (or prompt the user to choose a save location and file name)
* Save the current project to a different file.

The project file specifies a ROM file, RAM file, LST file, and the list of watched memory locations, and is saved as a JSON file.

A "Load ROM" button allows the user to choose a firmware file. After choosing a file, the application will prompt the user to choose the starting address, to position the file in memory.

A "Reload ROM" button will reload the most recently loaded ROM file, at the same address that was previously chosen.

A "Load RAM" button works the same way, but in this case the memory range will be treated as RAM.

A "Reload RAM" button will reload the most recently loaded RAM file, at the same address that was previously chosen.

The .bin files are just raw binary data. When they are opened, the contents will be passed to the Unicorn emulator with the base address provided by the user and the length defined by the file itself.

A "Load .LST" button allows the user to choose a .LST file, which will be shown in the main window. The .LST file format is simple, each line consists of the following:
* segment name
* colon
* hexadecimal address
* tab
* symbol name (optional)
* tab
* assembly instruction, data, or comment

A "Reload .LST" button will reload the most recently loaded LST file.

#### Settings

 When this is clicked a settings dialog will appear. The list of application settings has not yet been chosen.

## Miscellaneous

### Platforms

While Uno supports cross-platform development, this application is only intended for use on desktop computers - mobile and tablet devices /might/ also work, but no effort will be invested to make them work well.

Very few touch-screen interactions will be supported - only the ones described here, and those that come 'for free' in UI elements provided by the UI library or operating system.

### Hotkeys

Hotkeys are:

* F5 - run until a breakpoint is encountered
* F9 - toggle breakpoint
* F10 - step over (if a JSR is encountered, emulation stop when the instruction after the JSR is about to be executed)
* F11 - step into (if a JSR is encountered, it will be executed the the user will be shown the instruction that it jumped to)

Each time an instruction is executed, the register view and watch view will be updated.

After F5 or F10 are pressed, the application will loop through these steps:

* Execute the next instruction
* Update the register view and watch views
* If the instruction created an exception, update the status bar
* If the instruction did not create an exception, smoothly scroll the disassembly window so that the next instruction is in the middle of the pane.

After executing one iteration of the loop, the application will

### Memory Mapping

The application does not impose any constraints on the memory of the device being emulated. Users can choose the location and size of ROM and RAM when they select the ROM and RAM files.

### CPU Emulation Details

This project /will eventually/ support the CPU32 version of the Motorola 68000 instruction set. 

For the near term, it will be just use Unicorn's 68040 M68K support, which does not yet include some CPU32 instructions. But we are going to create a pull request for Unicorn to add full CPU32 support.
