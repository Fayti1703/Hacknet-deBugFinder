using System;
using DeBugFinder.Attribute;

namespace DeBugFinder.Internal.Patcher {
	public class PatchingException : Exception {

		public PatchAttribute CausedBy {
			get;
		}
		
		public PatchingException(PatchAttribute patch, Exception inner) : base(formatPatch(patch), inner) {
			this.CausedBy = patch;
		}
		
		public PatchingException(PatchAttribute patch, string message, Exception inner) : base(formatPatch(patch, message), inner) {
			this.CausedBy = patch;
		}

		private static string formatPatch(PatchAttribute patch, string message = null) {
			string text = $"Error patching '{patch.MethodSig}'";
			
			if(message != null) 
				text += ":\n" + message;

			return text;
		}
	}
}
