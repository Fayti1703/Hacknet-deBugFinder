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
		SenderVerify
	}
}
