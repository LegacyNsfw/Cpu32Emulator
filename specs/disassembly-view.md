# Disassembly View

We want two special behaviors that are not part of the original ScrollViewer / ListView implementation of the disassembly view:

* Scrolling between lines that are within the visible range should be smoothly animated.
* The instruction at the current PC address should be centered in the view when the PC register changes.

To support this:

* the disassembly view will display a series of images, called "tiles" that have the disassembly written onto them.

* tiles are generated on-demand, and are cached in memory for the lifetime of the application process. If memory usage becomes a problem, we'll add code to expire the least-recently used ones.

* the "model" data structure for a tile will contain, in addition to the image, the addresses of the first and last instructions in the image, and a dictionary whose keys are the addresses of each line of disassembly and whose values are the Y coordinates of the center of each address's region of the tile image.

* the disassembly view will maintain a linked list of tiles.

* when the PC register changes to an address in the currently displayed tile (or an adjacent tile) the view will smoothly scroll the new instruction to the center.

* when PC changes to an address outside the currently rendered tile, or an adjacent tile, the view won't animate. Instead it will just draw the destination tile (which may need to be generated first).

* if the current tile doesn't fill the entire viewport, the tile for the address range above or below it will be generated if necessary, and the adjacent tile will be added to the view, so that the user always sees a contiguous range of disassembly.
