using System;
using System.Collections.Generic;
using Hacknet;

namespace DeBugFinder {
	public static class DebuggingCommands {

		private static readonly Dictionary<string, Action<string[]>> commands = new Dictionary<string, Action<string[]>> {
			{ "detags", DebugLogger.HacknetInterface },
			{ "deccode", args => {
				foreach (string arg in args) 
					OS.currentInstance.write($"{arg} = " + FileEncrypter.GetPassCodeFromString(arg));
			} },
			{ "dumpfact", DumpFactions },
			{ "hublockdump", HubLockDump },
			{ "launchopt", ModifyLaunchOptions },
			{ "nodeoffset",  NearbyNodeOffsetViewer.HacknetInterface },
			{ "shutdown", _ => {
				OS.currentInstance.quitGame(null, null);
			} },
			{ "vmexit", _ => {
				MusicManager.stop();
				Game1.threadsExiting = true;
				Game1.getSingleton().Exit();
			} }
		};

		private static void ModifyLaunchOptions(string[] args) {
			const string usage = "launchopt <debug/fc/web/hex/nodepos> [on/off/toggle]";

			bool doValue(ref bool val) {
				if(args.Length < 2) return val;
				return val = args[1] switch {
					"on" => true,
					"off" => false,
					"toggle" => !val,
					_ => val
				};
			}

			OS os = OS.currentInstance;
			if(args.Length < 1) {
				os.write("Syntax Error. Syntax: " + usage);
				return;
			}

			string optName;
			bool res;

			switch(args[0]) {
				case "debug":
					optName = "Debug Commands";
					res = doValue(ref Settings.debugCommandsEnabled);
					OS.DEBUG_COMMANDS = res;
					break;
				case "fc":
					optName = "Force Complete";
					res = doValue(ref Settings.forceCompleteEnabled);
					break;
				case "web":
					optName = "Web Renderer";
					res = doValue(ref WebRenderer.Enabled);
					break;
				case "hex":
					optName = "Hex Background";
					res = doValue(ref Settings.DrawHexBackground);
					break;
				case "nodepos":
					optName = "PositionNear Debugging";
					res = doValue(ref Settings.debugDrawEnabled);
					break;
				default:
					os.write($"Unknown launch option: '{args[0]}'");
					os.write(usage);
					return;
			}

			os.write($"{optName} is {(args.Length < 2 ? "" : "now ")}{(res ? "on" : "off")}");
		}

		private static void HubLockDump(string[] args) {
			OS os = OS.currentInstance;
			Computer conn = os.connectedComp;
			if(conn == null) {
				os.write("Not connected to a node.");
				return;
			}

			if(!(conn.getDaemon(typeof(MissionHubServer)) is MissionHubServer server)) {
				os.write("No MissionHubServer on the node.");
				return;
			}

			Dictionary<string, ActiveMission>.ValueCollection missions = server.listingMissions.Values;

			if(missions.Count == 0) {
				os.write("No missions on the hub.");
				return;
			}

			foreach(ActiveMission mission in missions) {
				string lockReason = null;
				if(mission.postingAcceptFlagRequirements != null) {
					foreach(string flag in mission.postingAcceptFlagRequirements) {
						if(os.Flags.HasFlag(flag)) continue;
						lockReason = "missing flag '" + flag + "'";
						break;
					}
				}

				if(lockReason == null && os.currentFaction != null && os.currentFaction.playerValue < mission.requiredRank) lockReason = "rank too low";
				os.write($"Mission '{mission.postingTitle}' is {(lockReason == null ? "unlocked" : $"locked. ({lockReason})")}");
			}
		}

		private static void DumpFactions(string[] args) {
			OS os = OS.currentInstance;
			Faction curFact = os.currentFaction;
			os.write(curFact == null
				? "No current faction."
				: $@"Current faction: 
name       = {curFact.name}
idName     = {curFact.idName}
rank       = {curFact.playerValue}
maxRank    = {curFact.getMaxRank()}
neededRank = {curFact.neededValue}");
		}

		public static bool isValidCommand(string cmdName) {
			return commands.ContainsKey(cmdName);
		}

		public static void runCommand(string cmdName, string[] args) {
			commands[cmdName](args);
		}
	}
}
