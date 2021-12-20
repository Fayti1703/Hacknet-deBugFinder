# deBugFinder
Hacknet Extension debugging tool based on [Pathfinder](https://github.com/Arkhist/Hacknet-Pathfinder).

# Installation
(tbd)

# Usage

Eight commands have been added to the in-game terminal:
* `deccode`:
    Prints the internal code value used in DEC encryption for each string provided.
* `detags`: 
    Manage debugging tags. This command is split into various subcommands:
    * `detags` (no arguments): Lists the currently active debugging tags
    * `detags list`: Lists all available debugging tags
    * `detags <tag name> {on/true/add}`: Add the tag `<tag name>` to the active set.
    * `detags <tag name> {off/false/remove}`: Remove the tag `<tag name>` from the active set.
    * `detags <tag name> {get/status}`: Print the status of the tag `<tag name>`.
* `hublockdump`:
    Dumps the mission lock info from the currently connected node's first MissionListingServer, if any.
* `dumpfact`:
    Dumps the current faction information.
* `launchopt <launchopt> [on/off/toggle]`:
    Manipulates launch options. Available launch options:
    * `debug` (`-enabledebug`)
    * `fc` (`-enablefc`)
    * `web` (`-disableweb`)
    * `hex` (`-disablebackground`)
    * `nodepos` (no equivalent, previously unusable debug feature)
* `nodeoffset`:
    Control the Nearby Node Offset Viewer, aka the `positionNear` debugger. Subcommands:
    * `nodeoffset root-node`: Set the current node as the root node (`target` attribute)
    * `nodeoffset leaf-node`: Set the current node as the leaf node (the one `positionNear` would be on)
    * `nodeoffset start`: Begin the rotatening
    * `nodeoffset stop`: Stop the rotatening
    * `nodeoffset config <delay> <total> [extra] [max-pos] [min-pos]`: Configure the rotatening:
        * `<delay>`: How many milliseconds between updates. Double-precision floating-point
        * `<total>`: `total` attribute.
        * `[extra]`: `extraDistance` attribute. Defaults to `0`
        * `[max-pos]`: Maximum value for the `position` attribute. Defaults to `<total>`
        * `[min-pos]`: Minimum value for the `position` attribute. Defaults to `0`
    * `nodeoffset once <pos> <total> [extra]`
        * `<pos>`: `position` attribute.
        * `<total>`: `total` attribute.
        * `[extra]`: `extraDistance` attribute. Defaults to `0`.
    * `nodeoffset clear-debug`: Clear the "attempted positions" list, rendered with `launchopt nodepos on`
* `shutdown`:
    Quit the current session, as if you clicked "Exit to Menu" on the "Quit HacknetOS?" popup. Save before you do this.
* `vmexit`:
    Quits the game entirely. Yes, from directly within a session. **Save before you do this.**
    

## Debugging Tags

An explaination of all debugging tags:

### HacknetError
This tag logs most errors caught by Hacknet, most notably the `Hacknet.Util.AppendToErrorFile` function.
However, the exception catches in `Hacknet.OS:Draw` and `Hacknet.OS:Update` are also logged under this tag.

### MissionFunction
This tag logs all mission function runs.

### MissionLoad
This tag logs all (successfully) loaded missions. The output format is as follows:<br>
> Loaded Mission &lt;mission name&gt;<br>
> startMission = &lt;missionStart function&gt; / &lt;missionStart value&gt;<br>
> endMission = &lt;missionEnd function&gt; / &lt;missionEnd value&gt;

### MissionGoal
This tag logs unsuccessful mission completions caused by goals,
and the specific goal type causing them to fail.
Note that for missions with `activeCheck="true"`, a mission completion is attempted *every frame*, 
which **may result in some significant log spam**. Use with caution.

## MissionComplete
This tag logs *successful* mission completions. Unlike the above tag, it will only fire once per mission.

### SenderVerify
This tag logs unsuccessful mission completions caused by sender verification.<br>
Sender verification is a feature of `MailServer` that ensures only replies sent to 
the appropriate sender of the mission triggers a mission completion attempt.

### HasFlags
This tag logs checking of the `HasFlags` condition, the result of that check, and which flag caused it to fail.<br>
Note that condition checking is performed *every frame*, so this tag **results in significant log spam**.

### ActionLoad
This tag logs all action file loads. Only the file name itself is logged.

### ActionLoadDetail
This tag logs information about action loading, specifically the action elements themselves.

### ActionLoadDetailDetail
This tag logs information about condition and action loading, 
tracking the reader state every step of the deserialization 
(except for the part covered by `ActionLoadDetail`).
This, of course, **results in significant log spam** for any action file longer than a few lines.

### DisableDelayProcessing
This tag logs nothing. Instead, it prevents all (`Fast`)`DelayableActionSystem`s from updating. This includes
`IRCDaemon`s, `DHSDaemon`s and `FastActionHost`s.

### WriteReport
This tag logs all data that would be written to `report.txt`, provided that the write succeeds. It usually does, however, some
issues, like opening too many files in the mission parser, cause the report writes to fail.<br>
In order to properly use this tag, you may have to (ab)use the fact that the active set is not cleared between Hacknet sessions, only when you restart Hacknet itself.

### SaveTrace
This tag logs a complete stack trace of every single save attempt, 
both using the "normal" `Hacknet.OS:saveGame` method, which launches a new thread to handle the save, 
and direct execution of `Hacknet.OS:threadedSaveExecute`.<br>
Note that all calls to `Hacknet.OS:saveGame` cause two log messages to be produced. 
This is unavoidable without causing threading issues.<br>
In addition, capturing the stack trace is a rather slow operation, so **expect performance to suffer** if you save with this tag active.

### ComputerCrash
This tag logs whenever a computer crashes, a computer forkbombs its clients (Shell Tap),
a HackerScript dies due to a computer crashing or a computer boots back up.

### MissionLoadTrace
This tag logs a complete stack trace whenever a mission load begins.
Capturing the stack trace is a rather slow operation, so **expect performance to suffer** during mission loads with this tag active.

### PortUnmapping
This tag logs the result of the displayed port to "code port" conversion whenever it happens.

### ActionExec
This tag logs any and all actions that triggered to run or enqueued on a DelayHost.
Depending on how extensively an Extension uses Actions, **expect log spam**.

### DeleteFile
This tag logs some extra information about the `DeleteFile` action, mainly how many files matched the criteria and how many were found afterwards.
Yes, you can have more than one file with a given name. *Please* don't abuse this information.

### NodeLoad
This tag logs when what node is loaded.

### Flags
This tags logs any and all flag additions and removals.

### Music
This tag logs changes to the game's music. By which I mean *any* changes. 
Loading songs, transitioning, muting, stopping, playing...
