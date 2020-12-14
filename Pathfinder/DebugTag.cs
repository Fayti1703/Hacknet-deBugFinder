using Hacknet;

namespace Pathfinder {
	public enum DebugTag {
		/**
		 * Intercepted Hacknet errors. This may come from a lot of places. <see cref="Hacknet.Utils.AppendToErrorFile" />
		 */
		HacknetError,
		/**
		 * Mission Function execution <see cref="Hacknet.MissionFunctions.runCommand"/>
		 */
		MissionFunction,
		/**
		 * Mission Loading <see cref="Hacknet.ComputerLoader.readMission"/>
		 */
		MissionLoad,
		/**
		 * Mission completion <see cref="Hacknet.ActiveMission.isComplete"/>
		 */
		MissionComplete,
		/**
		 * Email sender verification <see cref="Hacknet.MailServer.attemptCompleteMission"/>
		 */
		SenderVerify,
		/**
		 * HasFlags checking <see cref="Hacknet.SCHasFlags"/>
		 */
		HasFlags,
		/**
		 * Action Loading, basic logging <see cref="Hacknet.RunnableConditionalActions.LoadIntoOS"/>
		 */
		ActionLoad,
		/**
		 * Action Loading, more detailed logging <see cref="Hacknet.SerializableAction.Deserialize"/>
		 */
		ActionLoadDetail,
		/**
		 * Action Loading, logging to the log-spam <see cref="Hacknet.SerializableConditionalActionSet.Deserialize"/>
		 */
		ActionLoadDetailDetail,
		/**
		 * Disable delay processing. <see cref="Hacknet.DelayableActionSystem.Update"/>
		 */
		DisableDelayProcessing,
		/**
		 * Report writing. For catching too many open files 'n the like. <see cref="Hacknet.Misc.ExtensionTests.TestExtensionForRuntime" />
		 */
		WriteReport,
		/**
		 * Trace save writing. <see cref="Hacknet.OS.saveGame" /> <seealso cref="Hacknet.OS.threadedSaveExecute" />
		 */
		SaveTrace,
		/**
		 * Crashing and rebooting computers.
		 * <see cref="Hacknet.Computer.crash"/>
		 * <seealso cref="Hacknet.Computer.bootupTick"/>
		 * <seealso cref="Hacknet.Computer.forkBombClients"/>
		 * <seealso cref="Hacknet.HackerScriptExecuter.executeThreadedScript" why="HackerScripts die when their Computer crashes."/>
		 */
		ComputerCrash
	}
}