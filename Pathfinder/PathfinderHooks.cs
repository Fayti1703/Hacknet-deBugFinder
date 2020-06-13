using System;
using System.Linq;
using Hacknet;
using Hacknet.Effects;
using Hacknet.Gui;
using Microsoft.Xna.Framework;
using Pathfinder.Attribute;

using static Pathfinder.Attribute.PatchAttribute;
using static Pathfinder.DebugTag;

namespace Pathfinder {
	/// <summary>
	/// Function hooks for the Pathfinder mod system
	/// </summary>
	/// Place all functions to be hooked into Hacknet here
	public static class PathfinderHooks {

		[Patch("Hacknet.Utils.AppendToErrorFile", flags: InjectFlags.PassParametersVal)]
		public static void onDebugHook_appendToErrorFile(string text) {
			DebugLogger.Log(HacknetError, text);
		}

		[Patch("Hacknet.OS.Draw", 320, flags: InjectFlags.PassLocals, localsID: new[] {1})]
		public static void onDebugHook_osDrawCatch(ref Exception ex) {
			DebugLogger.Log(HacknetError, Utils.GenerateReportFromException(ex));
		}

		[Patch("Hacknet.OS.Update", 800, flags: InjectFlags.PassLocals, localsID: new[] {4})]
		public static void onDebugHook_osUpdateCatch(ref Exception ex) {
			DebugLogger.Log(HacknetError, Utils.GenerateReportFromException(ex));
		}

		[Patch("Hacknet.MissionFunctions.runCommand", flags: InjectFlags.PassParametersVal)]
		public static void onDebugHook_runFunction(int value, string name) {
			DebugLogger.Log(MissionFunction, $"Running Mission function '{name}' with val {value}");
		}

		[Patch("Hacknet.ComputerLoader.readMission", -4, flags: InjectFlags.PassLocals,
			localsID: new[] {2})]
		public static void onDebugHookMissionRead(ref ActiveMission mission) {
			DebugLogger.Log(MissionLoad, $"Loaded Mission '{mission.reloadGoalsSourceFile}'.");
			DebugLogger.Log(MissionLoad,
				$"startMission = {mission.startFunctionName.formatForLog()} / {mission.startFunctionValue}");
			DebugLogger.Log(MissionLoad,
				$"endMission = {mission.endFunctionName.formatForLog()} / {mission.endFunctionValue}");
		}

		#region Game Integration

		[Patch("Hacknet.ProgramRunner.ExecuteProgram", 13,
			flags: InjectFlags.PassParametersVal | InjectFlags.ModifyReturn | InjectFlags.PassLocals,
			localsID: new[] {1}
		)]
		public static bool onDebugHook_runProgram(
			ref bool disconnects, ref bool returnFlag, object osObj, string[] args
		) {
			if (!DebuggingCommands.isValidCommand(args[0])) return false;
			DebuggingCommands.runCommand(args[0], args.Skip(1).ToArray());
			disconnects = returnFlag = false;
			return true;
		}

		[Patch("Hacknet.MainMenu.DrawBackgroundAndTitle", 7,
			flags: InjectFlags.PassInvokingInstance | InjectFlags.ModifyReturn | InjectFlags.PassLocals,
			localsID: new[] { 0 }
		)]
		public static bool onDrawMainMenuTitles(MainMenu self, out bool result, ref Rectangle dest) {
			result = true;
			FlickeringTextEffect.DrawLinedFlickeringText(new Rectangle(180, 120, 340, 100), "HACKNET", 7f, 0.55f, self.titleFont, (object) null, self.titleColor, 2);
			string versionInfo = "OS" + (DLC1SessionUpgrader.HasDLC1Installed ? "+Labyrinths " : " ") + MainMenu.OSVersion + " ## deBugFinder v0.1";
			TextItem.doFontLabel(new Vector2(520f, 178f), versionInfo, GuiData.smallfont, self.titleColor * 0.5f, 600f, 26f);
			if (!Settings.IsExpireLocked) return true;
			TimeSpan timeSpan = Settings.ExpireTime - DateTime.Now;
			string text;
			if (timeSpan.TotalSeconds < 1.0)
			{
				text = LocaleTerms.Loc("TEST BUILD EXPIRED - EXECUTION DISABLED");
				result = false;
			}
			else
				text = "Test Build : Expires in " + timeSpan;
			TextItem.doFontLabel(new Vector2(180f, 105f), text, GuiData.smallfont, Color.Red * 0.8f, 600f, 26f);
			return true;
		}

		#endregion
	}
}
