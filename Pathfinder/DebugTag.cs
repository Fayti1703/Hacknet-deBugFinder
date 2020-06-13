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
		MissionFunction
	}
}
