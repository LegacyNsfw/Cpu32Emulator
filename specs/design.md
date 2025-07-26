# CPU32 Emulator Design

## Platform And Libraries

This application uses the following technologies:

* The application is written in C#
* The application uses .Net 8
* The application uses the [Uno Platform](https://platform.uno/) UI library and application framework, version 5.6.37.
* The application uses the [Unicorn CPU emulator](https://www.unicorn-engine.org/), version 2.1.3, with C# bindings.

## Disassembly

This application does not contain a disassembler, however one might be added in the future. For now, it depends on .LST files, such as those produced by IDA Pro.
