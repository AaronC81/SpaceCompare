## SpaceCompare
> Yesterday I had 10 gigs free on C: and now I've got 3!? Where's it gone?
>
> *- me, often*

SpaceCompare is a Windows GUI tool which compares two [SpaceSniffer](http://www.uderzo.it/main_products/space_sniffer/) "Grouped by folder" snapshots and list which directories have increased in size.

SpaceCompare is still an early tool and is very buggy! Currently:

- All file sizes are given in bytes, making them hard to read
- Windows thinks the program has crashed while the file is loading

Both of these issues can be easily solved with time.

## Installation

SpaceCompare doesn't need to be installed; it can just be extracted from a ZIP and run in a folder with a few DLLs it needs.

1. Download a ZIP from the releases page
2. Extract it somewhere
3. Run `SpaceCompare.exe`

## Building

Clone this repo and use the `dotnet` command-line tool to handle running and building.