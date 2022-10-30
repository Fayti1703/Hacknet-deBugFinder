namespace Pathfinder {
	public struct VerboseResult {
		/**
		 * Length as drawn by the game
		 */
		public float drawn;
		/**
		 * Length as measured by the game
		 */
		public float measured;
		/**
		 * Length without any corrective factors
		 */
		public float pure;
		/**
		 * Length as it **should** be, damn it!
		 */
		public float correct;

		public float firstLineY;

		/**
		 * Line count
		 */
		public int lineCount;
		
		public static VerboseResult operator +(VerboseResult a, VerboseResult b) {
			return new VerboseResult {
				drawn = a.drawn + b.drawn,
				measured = a.measured + b.measured,
				pure = a.pure + b.pure,
				correct = a.correct + b.correct,
				lineCount = a.lineCount + b.lineCount
			};
		}
	}
}
