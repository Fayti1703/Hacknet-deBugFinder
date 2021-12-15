using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml;
using DeBugFinder.Attribute;
using DeBugFinder.Util;
using Hacknet;
using Hacknet.Effects;
using Hacknet.Factions;
using Hacknet.Gui;
using Hacknet.Misc;
using Hacknet.Mission;
using Microsoft.Xna.Framework;
using static DeBugFinder.Attribute.PatchAttribute;
using static DeBugFinder.DebugTag;

#pragma warning disable 1591

namespace DeBugFinder {
	/// <summary>
	/// Function hooks for the Pathfinder hook system
	/// </summary>
	/// Place all functions to be hooked into Hacknet here
	public static class DeBugFinderHooks {
		public const string TitleScreenTag = "deBugFinder v0.5";

		[Patch(typeof(Utils), "AppendToErrorFile", flags: InjectFlags.PassParametersVal)]
		public static void onDebugHook_appendToErrorFile(string text) {
			DebugLogger.Log(HacknetError, text);
		}

		[Patch(typeof(OS), "Draw", ilIndex: 320, flags: InjectFlags.PassLocals, localIDs: new[] { 1 })]
		public static void onDebugHook_osDrawCatch(ref Exception ex) {
			DebugLogger.Log(HacknetError, Utils.GenerateReportFromException(ex));
		}

		[Patch(typeof(OS), "Update", ilIndex: 800, flags: InjectFlags.PassLocals, localIDs: new[] { 4 })]
		public static void onDebugHook_osUpdateCatch(ref Exception ex) {
			DebugLogger.Log(HacknetError, Utils.GenerateReportFromException(ex));
		}

		[Patch(typeof(MissionFunctions), "runCommand", flags: InjectFlags.PassParametersVal)]
		public static void onDebugHook_runFunction(int value, string name) {
			DebugLogger.Log(MissionFunction, $"Running Mission function '{name}' with val {value}");
		}

		[Patch(typeof(ComputerLoader), "readMission",
			ilIndex: -4,
			flags: InjectFlags.PassLocals,
			localIDs: new[] {2}
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

		[Patch(typeof(ActiveMission), "isComplete",
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

		[Patch(typeof(MailServer), "attemptCompleteMission",
			flags: InjectFlags.PassInvokingInstance | InjectFlags.PassParametersVal
		)]
		public static void onDebugHook_MS_attemptCompleteMission(MailServer self, ActiveMission mission) {
			if (mission.ShouldIgnoreSenderVerification || mission.email.sender == self.emailData[1]) return;
			DebugLogger.Log(SenderVerify, $"Mission '{mission.reloadGoalsSourceFile}' failed sender verification!");
			DebugLogger.Log(SenderVerify, "email says: " + self.emailData[1]);
			DebugLogger.Log(SenderVerify, "Mission says: " + mission.email.sender);
		}

		[Patch(typeof(SCHasFlags), "Check",
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

		[Patch(typeof(RunnableConditionalActions), "LoadIntoOS", flags: InjectFlags.PassParametersVal)]
		public static void onDebugHookRCA_LoadIntoOS(string filepath, object OSobj) {
			string truePath = LocalizedFileLoader.GetLocalizedFilepath(Utils.GetFileLoadPrefix() + filepath);
			DebugLogger.Log(ActionLoad, $"Loading Conditional Actions File {truePath.formatForLog()} into OS.");
		}

		[Patch(typeof(SerializableConditionalActionSet), "Deserialize",
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

		[Patch(typeof(SerializableAction), "Deserialize", flags: InjectFlags.PassParametersRef)]
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

		[Patch(typeof(DelayableActionSystem), "Update", flags: InjectFlags.ModifyReturn)]
		public static bool onDebugHook_DAS_Update() {
			return DebugLogger.isEnabled(DisableDelayProcessing);
		}

		[Patch(typeof(FastDelayableActionSystem), "Update", flags: InjectFlags.ModifyReturn)]
		public static bool onDebugHook_FDAS_Update() {
			return DebugLogger.isEnabled(DisableDelayProcessing);
		}

		[Patch(typeof(ExtensionTests), "TestExtensionForRuntime",
			ilIndex: -1,
			flags: InjectFlags.PassLocals,
			localIDs: new[] {1}
		)]
		public static void onDebugHook_testComplete(ref string retVal) {
			DebugLogger.Log(WriteReport, retVal);
		}

		[Patch(typeof(OS), "saveGame")]
		public static void onDebugHook_saveBeginThread() {
			DebugLogger.Log(SaveTrace, () => "Save thread triggered \n" + new StackTrace(3));
		}

		[Patch(typeof(OS), "threadedSaveExecute")]
		public static void onDebugHook_saveFromThread() {
			DebugLogger.Log(SaveTrace,
				() => "Threaded save execution triggered \n" + new StackTrace(3)
			);
		}

		[Patch(typeof(Computer), "crash", flags: InjectFlags.PassInvokingInstance)]
		public static void onDebugHook_Comp_Crash(Computer self) {
			DebugLogger.Log(ComputerCrash, () => $"'{self.idName}' crashed.");
		}

		[Patch(typeof(Computer), "bootupTick", ilIndex: -2, flags: InjectFlags.PassInvokingInstance)]
		public static void onDebugHook_Comp_BootupTick(Computer self) {
			DebugLogger.Log(ComputerCrash, () => $"'{self.idName}' rebooted.");
		}

		[Patch(typeof(Computer), "forkBombClients", flags: InjectFlags.PassInvokingInstance)]
		public static void onDebugHook_Comp_fbClients(Computer self) {
			DebugLogger.Log(ComputerCrash,
				() => self.os.ActiveHackers.Where(hacker => hacker.Value == self.ip)
					.Aggregate($"'{self.idName}' is forkbombing clients.",
						(acc, hacker) =>
							acc + "\nAffected node: " + Programs.getComputer(self.os, hacker.Key).idName
					)
			);
		}

		[Patch(typeof(HackerScriptExecuter), "executeThreadedScript", ilIndex: 40, flags: InjectFlags.PassLocals, localIDs: new[] { 3 })]
		public static void onDebugHook_HSE_CrashCheck(ref Computer _source) {
			Computer source = _source;
			DebugLogger.Log(ComputerCrash, () => "HackerScript from '" + source.idName + "' shutting down because host computer crashed.");
		}

		
		[Patch(typeof(ComputerLoader), "readMission", flags: InjectFlags.PassParametersVal)]
		public static void onDebugHook_MissionReadTrace(string filename) {
			DebugLogger.Log(MissionLoadTrace, () => 
				$"Mission Load '{filename}' Triggered:\n" + new StackTrace(3)
			);
		}

		[Patch(
			typeof(Computer), "GetCodePortNumberFromDisplayPort",
			flags: InjectFlags.ModifyReturn | InjectFlags.PassParametersVal | InjectFlags.PassInvokingInstance
		)]
		public static bool onDebugHook_CPNFromDP(Computer self, out int _codePort, int displayPort) {	
			int getCodePort() {
				if (self.PortRemapping == null)
					return displayPort;
				IEnumerator<KeyValuePair<int, int>> enumerator = self.PortRemapping
					.Where(mapping => mapping.Value == displayPort)
					.GetEnumerator();

				using (enumerator) {
					if (enumerator.MoveNext()) {
						return enumerator.Current.Key;
					}
				}

				return displayPort;
			}

			int codePort = getCodePort();
			
			DebugLogger.Log(PortUnmapping, $"Mapped display port {displayPort} to code port {codePort}");
			
			_codePort = codePort;
			return true;
		}

		[Patch(
			typeof(RunnableConditionalActions), "Update",
			ilIndex: 39,
			flags: InjectFlags.PassLocals,
			localIDs: new[] { 1, 2 }
		)]
		public static void onDebugHook_RCA_Update(ref SerializableConditionalActionSet setToTrigger, ref int actionIndex) {
			SerializableAction actionToTrigger = setToTrigger.Actions[actionIndex];
			DebugLogger.Log(ActionExec, $"Triggering '{actionToTrigger.GetType().Name}' Action from Condition hit.");
		}

		[Patch(
			typeof(FastDelayableActionSystem), "Update",
			ilIndex: 37,
			flags: InjectFlags.PassLocals,
			localIDs: new[] { 2 }
		)]
		public static void onDebugHook_FDAS_Update(ref SerializableAction actionToTrigger) {
			DebugLogger.Log(ActionExec, $"Triggering '{actionToTrigger.GetType().Name}' Action from FastDelayHost.");
		}
		
		[Patch(
			typeof(DelayableActionSystem), "Update",
			ilIndex: 47,
			flags: InjectFlags.PassLocals,
			localIDs: new[] { 4 }
		)]
		public static void onDebugHook_DAS_Update(ref string encryptedData) {
			if(!DebugLogger.isEnabled(ActionExec)) return; /* optimize */
			string[] decryptData = FileEncrypter.DecryptString(encryptedData, DelayableActionSystem.EncryptionPass);
			Stream dataStream = Utils.GenerateStreamFromString(decryptData[2]);
			XmlReader reader = XmlReader.Create(dataStream);
			SerializableAction actionToTrigger = SerializableAction.Deserialize(reader);
			reader.Close();
			DebugLogger.Log(ActionExec, $"Triggering '{actionToTrigger.GetType().Name}' Action from DelayHost.");
		}

		[Patch(
			typeof(CustomFactionAction), "Trigger",
			ilIndex: 4,
			flags: InjectFlags.PassInvokingInstance | InjectFlags.PassLocals,
			localIDs: new[] { 0 }
		)]
		public static void onDebugHook_CFA_Trigger(CustomFactionAction self, ref int actionIndex) {
			SerializableAction actionToTrigger = self.TriggerActions[actionIndex];
			DebugLogger.Log(ActionExec, $"Triggering '{actionToTrigger.GetType().Name}' Action from CustomFactionAction.");
		}
		
		[Patch(
			typeof(DelayableActionSystem), "AddAction",
			flags: InjectFlags.PassParametersVal
		)]
		public static void onDebugHook_DAS_AddAction(SerializableAction action, float delay) {
			DebugLogger.Log(ActionExec, $"Adding '{action.GetType().Name}' Action with {delay}s Delay to DelayHost.");
		}

		[Patch(
			typeof(FastDelayableActionSystem), "AddAction",
			flags: InjectFlags.PassParametersVal
		)]
		public static void onDebugHook_FDAS_AddAction(SerializableAction action, float delay) {
			DebugLogger.Log(ActionExec, $"Adding '{action.GetType().Name}' Action with {delay}s Delay to FastDelayHost.");
		}

		[Patch(
			typeof(SADeleteFile), "Trigger",
			ilIndex: 17,
			flags: InjectFlags.PassInvokingInstance | InjectFlags.ModifyReturn | InjectFlags.PassLocals,
			localIDs: new[] { 0, 1 }
		)]
		public static bool onDebugHook_SADF_Trigger(SADeleteFile self, ref OS os, ref Computer targetComputer) {
			try {
				Folder folderAtPath = Programs.getFolderAtPath(self.FilePath, os, targetComputer.files.root, true);
				if(folderAtPath == null) {
					DebugLogger.Log(DeleteFile, $"Couldn't find folder: '{self.FilePath}'");
					return true;
				}

				List<FileEntry> fileEntries = folderAtPath.files.FindAll(x => x.name == self.FileName);
				DebugLogger.Log(DeleteFile, $"Found {fileEntries.Count} files matching  '{self.FilePath}'/'{self.FileName}'");
				if(fileEntries.Count > 0) {
					folderAtPath.files.Remove(fileEntries.First());
					DebugLogger.Log(DeleteFile, "Removed one of them.");
				} else
					DebugLogger.Log(DeleteFile, "Removed nothing, since nothing was present.");
				List<FileEntry> postFileEntries = folderAtPath.files.FindAll(x => x.name == self.FileName);
				DebugLogger.Log(DeleteFile, $"POST: Found {postFileEntries.Count} files matching  '{self.FilePath}'/'{self.FileName}'");
			} catch(Exception e) {
				DebugLogger.Log(DeleteFile, $"Exception! {e}");
				throw;
			}

			return true;
		}

		#region Game Integration

		[Patch(typeof(ProgramRunner), "ExecuteProgram",
			ilIndex: 13,
			flags: InjectFlags.PassParametersVal | InjectFlags.ModifyReturn | InjectFlags.PassLocals,
			localIDs: new[] {1}
		)]
		public static bool onRunProgram(
			ref bool disconnects, ref bool returnFlag, object osObj, string[] args
		) {
			if (!DebuggingCommands.isValidCommand(args[0])) return false;
			DebuggingCommands.runCommand(args[0], args.Skip(1).ToArray());
			disconnects = returnFlag = false;
			return true;
		}

		[Patch(typeof(OS), "quitGame")]
		public static void onQuitGame() {
			NearbyNodeOffsetViewer.onSessionStop();
		}

		[Patch(typeof(OS), "Update", flags: InjectFlags.PassParametersVal)]
		public static void onUpdateGame(GameTime deltaT, bool unfocused, bool covered) {
			NearbyNodeOffsetViewer.onUpdate(deltaT);
		}

		[Patch(typeof(MainMenu), "DrawBackgroundAndTitle",
			ilIndex: 7,
			flags: InjectFlags.PassInvokingInstance | InjectFlags.ModifyReturn | InjectFlags.PassLocals,
			localIDs: new[] {0}
		)]
		public static bool onDrawMainMenuTitles(MainMenu self, out bool result, ref Rectangle dest) {
			result = true;
			FlickeringTextEffect.DrawLinedFlickeringText(new Rectangle(180, 120, 340, 100),
				"HACKNET",
				7f,
				0.55f,
				self.titleFont,
				null,
				self.titleColor
			);
			string versionInfo = "OS" + (DLC1SessionUpgrader.HasDLC1Installed ? "+Labyrinths " : " ") +
				MainMenu.OSVersion + " ## " + TitleScreenTag;
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
