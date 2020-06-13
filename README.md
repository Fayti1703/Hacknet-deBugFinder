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

### MissionComplete
This tag logs unsuccessful mission completions caused by goals,
and the specific goal type causing them to fail.
Note that for missions with `activeCheck="true"`, a mission completion is attempted *every frame*, 
which **may result in some significant log spam**. Use with caution.

### SenderVerify
This tag logs unsuccessful mission completions caused by sender verification.<br>
Sender verification is a feature of `MailServer` that ensures only replies sent to 
the appropriate sender of the mission triggers a mission completion attempt.

### HasFlags
This tag logs checking of the `HasFlags` condition, the result of that check, and which flag caused it to fail.<br>
Note that condition checking is performed *every frame*, so this tag **results in significant log spam**.

### ActionLoad
This tag logs all action file loads. Only the file name itself is logged.