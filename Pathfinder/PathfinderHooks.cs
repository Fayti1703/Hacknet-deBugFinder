using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Hacknet;
using Hacknet.Effects;
using Hacknet.Gui;
using Hacknet.Mission;
using Microsoft.Xna.Framework;
using Pathfinder.Attribute;
using Pathfinder.Util;
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

		[Patch("Hacknet.ComputerLoader.readMission",
			-4,
			flags: InjectFlags.PassLocals,
			localsID: new[] {2}
		)]
		public static void onDebugHook_MissionRead(ref ActiveMission mission) {
			DebugLogger.Log(MissionLoad, $"Loaded Mission '{mission.reloadGoalsSourceFile}'.");
			DebugLogger.Log(MissionLoad,
				$"startMission = {mission.startFunctionName.formatForLog()} / {mission.startFunctionValue}"
			);
			DebugLogger.Log(MissionLoad,
				$"endMission = {mission.endFunctionName.formatForLog()} / {mission.endFunctionValue}"
			);
		}

		[Patch("Hacknet.ActiveMission.isComplete",
			flags: InjectFlags.PassInvokingInstance | InjectFlags.PassParametersVal |
			InjectFlags.ModifyReturn
		)]
		public static bool onDebugHook_AM_isComplete(ActiveMission self, out bool ret, List<string> additionalDetails) {
			ret = true;
			foreach (MisisonGoal goal in self.goals.Where(goal => !goal.isComplete(additionalDetails))) {
				DebugLogger.Log(MissionComplete, $"A {goal.GetType().Name} goal prevented mission completion.");
				ret = false;
				break;
			}

			return true;
		}

		[Patch("Hacknet.MailServer.attemptCompleteMission",
			flags: InjectFlags.PassInvokingInstance | InjectFlags.PassParametersVal
		)]
		public static void onDebugHook_MS_attemptCompleteMission(MailServer self, ActiveMission mission) {
			if (mission.ShouldIgnoreSenderVerification || mission.email.sender == self.emailData[1]) return;
			DebugLogger.Log(SenderVerify, $"Mission '{mission.reloadGoalsSourceFile}' failed sender verification!");
			DebugLogger.Log(SenderVerify, "email says: " + self.emailData[1]);
			DebugLogger.Log(SenderVerify, "Mission says: " + mission.email.sender);
		}

		[Patch("Hacknet.SCHasFlags.Check",
			flags: InjectFlags.ModifyReturn | InjectFlags.PassInvokingInstance | InjectFlags.PassParametersVal
		)]
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

		[Patch("Hacknet.SerializableConditionalActionSet.Deserialize",
			flags: InjectFlags.PassParametersRef | InjectFlags.ModifyReturn
		)]
		public static bool onDebugHook_SCAS_Deserialize(
			out SerializableConditionalActionSet retVal, ref XmlReader rdr
		) {
			static bool innerWhileCondition(XmlReader reader, string endKeyName) {
				DebugLogger.Log(ActionLoadDetailDetail,
					$"Looping over elements: {reader.toLogString()} / endKeyName = {endKeyName}"
				);
				if (reader.EOF) {
					DebugLogger.Log(ActionLoadDetailDetail, "Reader hit EOF.");
					return false;
				}

				if (reader.Name != endKeyName) return true;
				if (reader.IsStartElement()) return true;
				DebugLogger.Log(ActionLoadDetailDetail, "Found end key name.");
				return false;
			}

			SerializableConditionalActionSet actionSet = retVal = new SerializableConditionalActionSet();
			retVal.Condition = SerializableCondition.Deserialize(rdr,
				(reader, endKeyName) => {
					/* first read loop */
					while (true) {
						DebugLogger.Log(ActionLoadDetailDetail, $"Looking for first action: {reader.toLogString()}");
						if (reader.EOF) break;
						if (reader.NodeType == XmlNodeType.Comment || reader.NodeType == XmlNodeType.Whitespace) {
							DebugLogger.Log(ActionLoadDetailDetail, "Ignoring comment/whitespace node");
							reader.Read();
							continue;
						}

						break;
					}

					while (innerWhileCondition(reader, endKeyName)) {
						SerializableAction action = SerializableAction.Deserialize(reader);
						actionSet.Actions.Add(action);
						do {
							DebugLogger.Log(ActionLoadDetailDetail,
								$"Preparing for next element pre: {reader.toLogString()}"
							);
							reader.Read();
							DebugLogger.Log(ActionLoadDetailDetail,
								$"Preparing for next element post: {reader.toLogString()}"
							);
						} while (reader.NodeType == XmlNodeType.Whitespace || reader.NodeType == XmlNodeType.Comment);
					}
				}
			);
			return true;
		}

		[Patch("Hacknet.SerializableAction.Deserialize", flags: InjectFlags.PassParametersRef)]
		public static void onDebugHook_SA_Deserialize(ref XmlReader rdr) {
			var acceptables = new HashSet<string> {
				"LoadMission",
				"RunFunction",
				"AddAsset",
				"AddMissionToHubServer",
				"RemoveMissionFromHubServer",
				"AddThreadToMissionBoard",
				"AddIRCMessage",
				"AddConditionalActions",
				"CopyAsset",
				"CrashComputer",
				"DeleteFile",
				"LaunchHackScript",
				"SwitchToTheme",
				"StartScreenBleedEffect",
				"CancelScreenBleedEffect",
				"AppendToFile",
				"KillExe",
				"ChangeAlertIcon",
				"HideNode",
				"GivePlayerUserAccount",
				"ChangeIP",
				"ChangeNetmapSortMethod",
				"SaveGame",
				"HideAllNodes",
				"ShowNode",
				"SetLock"
			};
			while (true) {
				if (rdr.EOF) {
					DebugLogger.Log(ActionLoadDetail, "Reader reached end of file");
					break;
				}

				if (rdr.IsStartElement()) {
					string elName = rdr.Name;
					DebugLogger.Log(ActionLoadDetail, $"Found new {rdr.NodeType}: {elName}");
					if (acceptables.Contains(elName)) {
						DebugLogger.Log(ActionLoadDetail, "Acceptable element! Delegating back to Hacknet...");
						return;
					}
					DebugLogger.Log(ActionLoadDetail, "That element is unrecognized.");
				} else
					DebugLogger.Log(ActionLoadDetail, $"Now within {rdr.NodeType}: {rdr.Name}");

				rdr.Read();
			}
		}

		[Patch("Hacknet.DelayableActionSystem.Update", flags: InjectFlags.ModifyReturn)]
		public static bool onDebugHook_DAS_Update() {
			return DebugLogger.isEnabled(DisableDelayProcessing);
		}

		[Patch("Hacknet.FastDelayableActionSystem.Update", flags: InjectFlags.ModifyReturn)]
		public static bool onDebugHook_FDAS_Update() {
			return DebugLogger.isEnabled(DisableDelayProcessing);
		}

		[Patch("Hacknet.Misc.ExtensionTests.TestExtensionForRuntime",
			-1,
			flags: InjectFlags.PassLocals,
			localsID: new[] {1}
		)]
		public static void onDebugHook_testComplete(ref string retVal) {
			DebugLogger.Log(WriteReport, retVal);
		}

		[Patch("Hacknet.OS.saveGame")]
		public static void onDebugHook_saveBeginThread() {
			DebugLogger.Log(SaveTrace, () => "Save thread triggered \n" + new System.Diagnostics.StackTrace(3));
		}

		[Patch("Hacknet.OS.threadedSaveExecute")]
		public static void onDebugHook_saveFromThread() {
			DebugLogger.Log(SaveTrace,
				() => "Threaded save execution triggered \n" + new System.Diagnostics.StackTrace(3)
			);
		}

		[Patch("Hacknet.Computer.crash", flags: InjectFlags.PassInvokingInstance)]
		public static void onDebugHook_Comp_Crash(Computer self) {
			DebugLogger.Log(ComputerCrash, () => $"'{self.idName}' crashed.");
		}

		[Patch("Hacknet.Computer.bootupTick", -2, flags: InjectFlags.PassInvokingInstance)]
		public static void onDebugHook_Comp_BootupTick(Computer self) {
			DebugLogger.Log(ComputerCrash, () => $"'{self.idName}' rebooted.");
		}

		[Patch("Hacknet.Computer.forkBombClients", flags: InjectFlags.PassInvokingInstance)]
		public static void onDebugHook_Comp_fbClients(Computer self) {
			DebugLogger.Log(ComputerCrash,
				() => self.os.ActiveHackers.Where(hacker => hacker.Value == self.ip)
					.Aggregate($"'{self.idName}' is forkbombing clients.",
						(acc, hacker) =>
							acc + "\nAffected node: " + Programs.getComputer(self.os, hacker.Key).idName
					)
			);
		}

		[Patch("Hacknet.HackerScriptExecuter.executeThreadedScript", 40, flags: InjectFlags.PassLocals, localsID: new[] { 3 })]
		public static void onDebugHook_HSE_CrashCheck(ref Computer _source) {
			Computer source = _source;
			DebugLogger.Log(ComputerCrash, () => "HackerScript from '" + source.idName + "' shutting down because host computer crashed.");
		}

		#region Game Integration

		[Patch("Hacknet.ProgramRunner.ExecuteProgram",
			13,
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

		[Patch("Hacknet.OS.quitGame")]
		public static void onQuitGame() {
			NearbyNodeOffsetViewer.onSessionStop();
		}

		[Patch("Hacknet.OS.Update", flags: InjectFlags.PassParametersVal)]
		public static void onUpdateGame(GameTime deltaT, bool unfocused, bool covered) {
			NearbyNodeOffsetViewer.onUpdate(deltaT);
		}
		
		[Patch("Hacknet.ComputerLoader.readMission", flags: InjectFlags.PassParametersVal)]
		public static void onDebugHook_MissionReadTrace(string filename) {
			DebugLogger.Log(MissionLoadTrace, () => 
				$"Mission Load '{filename}' Triggered:\n" + new System.Diagnostics.StackTrace(3)
			);
		}

		[Patch("Hacknet.MainMenu.DrawBackgroundAndTitle",
			7,
			flags: InjectFlags.PassInvokingInstance | InjectFlags.ModifyReturn | InjectFlags.PassLocals,
			localsID: new[] {0}
		)]
		public static bool onDrawMainMenuTitles(MainMenu self, out bool result, ref Rectangle dest) {
			result = true;
			FlickeringTextEffect.DrawLinedFlickeringText(new Rectangle(180, 120, 340, 100),
				"HACKNET",
				7f,
				0.55f,
				self.titleFont,
				(object) null,
				self.titleColor,
				2
			);
			string versionInfo = "OS" + (DLC1SessionUpgrader.HasDLC1Installed ? "+Labyrinths " : " ") +
				MainMenu.OSVersion + " ## deBugFinder v0.1";
			TextItem.doFontLabel(new Vector2(520f, 178f),
				versionInfo,
				GuiData.smallfont,
				self.titleColor * 0.5f,
				600f,
				26f
			);
			if (!Settings.IsExpireLocked) return true;
			TimeSpan timeSpan = Settings.ExpireTime - DateTime.Now;
			string text;
			if (timeSpan.TotalSeconds < 1.0) {
				text = LocaleTerms.Loc("TEST BUILD EXPIRED - EXECUTION DISABLED");
				result = false;
			} else
				text = "Test Build : Expires in " + timeSpan;

			TextItem.doFontLabel(new Vector2(180f, 105f), text, GuiData.smallfont, Color.Red * 0.8f, 600f, 26f);
			return true;
		}

		#endregion
	}
}
