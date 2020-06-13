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
