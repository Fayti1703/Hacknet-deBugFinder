# deBugFinder
Hacknet Extension debugging tool based on [Pathfinder](https://github.com/Arkhist/Hacknet-Pathfinder).

# Installation
(tbd)

# Usage

Two commands have been added to the in-game terminal:
* `deccode`:
    Prints the internal code value used in DEC encryption for each string provided.
* `detags`: 
    Manage debugging tags. This command is split into various subcommands:
    * `detags` (no arguments): Lists the currently active debugging tags
    * `detags list`: Lists all available debugging tags
    * `detags <tag name> {on/true/add}`: Add a tag to the active set.
    * `detags <tag name> {off/false/remove}`: Remove a tag from the active set.
    * `detags <tag name> {get/status}`: Prints the status of this tag
    

## Debugging Tags

An explaination of all debugging tags:
