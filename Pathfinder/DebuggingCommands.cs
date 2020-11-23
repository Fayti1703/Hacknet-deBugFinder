using System;
using System.Collections.Generic;
using Hacknet;

namespace Pathfinder {
	public static class DebuggingCommands {

		private static Dictionary<string, Action<string[]>> commands = new Dictionary<string, Action<string[]>> {
			{ "detags", DebugLogger.HacknetInterface },
			{ "deccode", args => {
				foreach (string arg in args) {
					OS.currentInstance.write($"{arg} = " + FileEncrypter.GetPassCodeFromString(arg));
				}
			} },
			{ "hublockdump", args => {
				var os = OS.currentInstance;
				Computer conn = os.connectedComp;
				if (conn == null) {
					os.write("Not connected to a node.");
					return;
				}
				if (!(conn.getDaemon(typeof(MissionHubServer)) is MissionHubServer server)) {
					os.write("No MissionHubServer on the node.");
					return;
				}

				Dictionary<string, ActiveMission>.ValueCollection missions = server.listingMissions.Values;

				if (missions.Count == 0) {
					os.write("No missions on the hub.");
					return;
				}

				foreach (ActiveMission mission in missions) {
					string lockReason = null;
					if (mission.postingAcceptFlagRequirements != null) {
						foreach (string flag in mission.postingAcceptFlagRequirements) {
							if (os.Flags.HasFlag(flag)) continue;
							lockReason = "missing flag '" + flag + "'";
							break;
						}
					}
					if (
						lockReason == null &&
						os.currentFaction != null &&
						os.currentFaction.playerValue < mission.requiredRank
					)
						lockReason = "rank too low";
					os.write(
						$"Mission '{mission.postingTitle}' is {(lockReason == null ? "unlocked" : $"locked. ({lockReason})")}"
					);
				}
			} }
		};

		public static bool isValidCommand(string cmdName) {
			return commands.ContainsKey(cmdName);
		}

		public static void runCommand(string cmdName, string[] args) {
			commands[cmdName](args);
		}
	}
}
