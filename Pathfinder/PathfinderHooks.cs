using System;
using System.Collections.Generic;
using System.Linq;
using Hacknet;
using Hacknet.Effects;
using Hacknet.Gui;
using Hacknet.Mission;
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

		[Patch("Hacknet.ActiveMission.isComplete",
			flags: InjectFlags.PassInvokingInstance | InjectFlags.PassParametersVal |
			       InjectFlags.ModifyReturn)]
		public static bool onDebugHookAM_isComplete(ActiveMission self, out bool ret, List<string> additionalDetails) {
			ret = true;
			foreach (MisisonGoal goal in self.goals.Where(goal => !goal.isComplete(additionalDetails))) {
				DebugLogger.Log(MissionComplete, $"A {goal.GetType().Name} goal prevented mission completion.");
				ret = false;
				break;
			}

			return true;
		}

		[Patch("Hacknet.MailServer.attemptCompleteMission",
			flags: InjectFlags.PassInvokingInstance | InjectFlags.PassParametersVal)]
		public static void onDebugHookMS_attemptCompleteMission(MailServer self, ActiveMission mission) {
			if (mission.ShouldIgnoreSenderVerification || mission.email.sender == self.emailData[1]) return;
			DebugLogger.Log(SenderVerify, $"Mission '{mission.reloadGoalsSourceFile}' failed sender verification!");
			DebugLogger.Log(SenderVerify, "email says: " + self.emailData[1]);
			DebugLogger.Log(SenderVerify, "Mission says: " + mission.email.sender);
		}

		[Patch("Hacknet.SCHasFlags.Check",
			flags: InjectFlags.ModifyReturn | InjectFlags.PassInvokingInstance | InjectFlags.PassParametersVal)]
		public static bool onDebugHookSCHF_Check(SCHasFlags self, out bool retVal, object os_obj) {
			var os = (OS) os_obj;
			if (string.IsNullOrWhiteSpace(self.requiredFlags)) {
				DebugLogger.Log(HasFlags, "HasFlags SUCCEEDED: no flags required. lol.");
				retVal = true;
				return true;
			}

			string[] flags = self.requiredFlags.Split(Utils.commaDelim, StringSplitOptions.RemoveEmptyEntries);
			DebugLogger.Log(HasFlags, "HasFlags checking against flags: " + string.Join(",", flags));
			foreach (string flag in flags.Where(flag => !os.Flags.HasFlag(flag))) {
				DebugLogger.Log(HasFlags, $"HasFlags FAILED: Flag {flag.formatForLog()} not present.");
				retVal = false;
				return true;
			}

			retVal = true;
			DebugLogger.Log(HasFlags, "HasFlags SUCCEEDED. Running actions.");
			return true;
		}

		[Patch("Hacknet.RunnableConditionalActions.LoadIntoOS", flags: InjectFlags.PassParametersVal)]
		public static void onDebugHookRCA_LoadIntoOS(string filepath, object OSobj) {
			string truePath = LocalizedFileLoader.GetLocalizedFilepath(Utils.GetFileLoadPrefix() + filepath);
			DebugLogger.Log(ActionLoad, $"Loading Conditional Actions File {truePath.formatForLog()} into OS.");
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
