using System;
using System.Runtime.Serialization;
using DeBugFinder.Attribute;

namespace DeBugFinder.Internal.Patcher {
	[Serializable]
	public class PatchingException : Exception {
		
		protected PatchingException(SerializationInfo info, StreamingContext context) : base(info, context) {
			this.CausedBy = (PatchAttribute) info.GetValue("CausedByAttribute", typeof(PatchAttribute));
		}

		override public void GetObjectData(SerializationInfo info, StreamingContext context) {
			base.GetObjectData(info, context);
			info.AddValue("CausedByAttribute", this.CausedBy, typeof(PatchAttribute));
		}

		public PatchAttribute CausedBy {
			get;
		}
		
		public PatchingException(PatchAttribute patch, Exception inner) : base(formatPatch(patch), inner) {
			this.CausedBy = patch;
		}
		
		public PatchingException(PatchAttribute patch, string message, Exception inner) : base(formatPatch(patch, message), inner) {
			this.CausedBy = patch;
		}

		public PatchingException(PatchAttribute patch, string message) : base(formatPatch(patch, message)) {
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
