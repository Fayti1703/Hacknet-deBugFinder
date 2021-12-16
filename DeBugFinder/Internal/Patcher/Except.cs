using System;
using System.Runtime.Serialization;
using System.Text;
using DeBugFinder.Attribute;
using Mono.Cecil;

namespace DeBugFinder.Internal.Patcher {

	[Serializable]
	internal class InjectionParameterMismatch : InjectionException {

		internal enum SourceType {
			Self,
			Return,
			Parameter,
			Local
		}

		internal readonly struct SourcePass {
			public readonly SourceType type;
			public readonly TypeReference typeRef;
			public readonly ParameterDefinition paramDef;
			public readonly int index;

			public SourcePass(SourceType type, TypeReference @ref) {
				this.type = type;
				this.typeRef = @ref;
				this.paramDef = default;
				this.index = 0;
			}
			
			public SourcePass(TypeReference @ref, int localIndex) {
				this.type = SourceType.Local;
				this.typeRef = @ref;
				this.paramDef = default;
				this.index = localIndex;
			}

			public SourcePass(ParameterDefinition def, int index) {
				this.type = SourceType.Parameter;
				this.typeRef = default;
				this.paramDef = def;
				this.index = index;
			}
		}

		internal readonly struct ParamPass {
			public readonly int index;
			public readonly ParameterDefinition param;
			public ParamPass(int index, ParameterDefinition param) {
				this.index = index;
				this.param = param;
			}
		}

		public readonly SourceType sourceType;
		public readonly int sourceIndex;
		public readonly int targetIndex;
		public readonly string baseMessage;

		public InjectionParameterMismatch(SourcePass source, ParamPass targetParam, string message = "does not match")
			: base(FormatMessage(source, targetParam, message)) 
		{
			this.sourceType = source.type;
			this.sourceIndex = source.index;
			this.targetIndex = targetParam.index;
			this.baseMessage = message;
		}

		protected InjectionParameterMismatch(SerializationInfo info, StreamingContext context) : base(info, context) {
			this.sourceType = (SourceType) info.GetValue("SourceType", typeof(SourceType));
			this.sourceIndex = info.GetInt32("SourceIndex");
			this.targetIndex = info.GetInt32("TargetIndex");
			this.baseMessage = info.GetString("BaseMessage");
		}

		override public void GetObjectData(SerializationInfo info, StreamingContext context) {
			base.GetObjectData(info, context);
			info.AddValue("SourceType", this.sourceType, typeof(SourceType));
			info.AddValue("SourceIndex", this.sourceIndex);
			info.AddValue("TargetIndex", this.targetIndex);
			info.AddValue("BaseMessage", this.baseMessage);
		}

		public static string FormatMessage(SourcePass source, ParamPass targetParam, string message) {
			StringBuilder msgb = new StringBuilder("Injection method parameter #").Append(targetParam.index).Append(' ');
			AppendParamTypeName(msgb, targetParam.param);
			msgb.Append(' ').Append(message).Append(' ');
			switch(source.type) {
				case SourceType.Self:
					msgb.Append("declarator ");
					break;
				case SourceType.Return:
					msgb.Append("return");
					break;
				case SourceType.Parameter:
					msgb.Append("parameter #");
					msgb.Append(source.index);
					break;
				case SourceType.Local:
					msgb.Append("local #");
					msgb.Append(source.index);
					break;
			}

			msgb.Append(' ');
			AppendSourceName(msgb, source);
			msgb.Append(" of target method.");
			return msgb.ToString();
		}

		private static void AppendSourceName(StringBuilder msgb, SourcePass source) {
			if(source.type == SourceType.Parameter)
				AppendParamTypeName(msgb, source.paramDef);
			else
				AppendTypeName(msgb, source.typeRef);
		}

		public static void AppendTypeName(StringBuilder msgb, TypeReference type) {
			msgb.Append("{ [").Append(type.Resolve().Module.Name).Append("] ").Append(type.FullName).Append(" }");
		}

		public static void AppendParamTypeName(StringBuilder msgb, ParameterDefinition param) {
			TypeReference type = param.ParameterType;
			msgb.Append("{ [").Append(type.Resolve().Module.Name).Append("] ").Append(type.FullName);
			if(param.IsOut)
				msgb.Append("=");
			msgb.Append(" }");
		}
	}

	[Serializable]
	internal class InjectionException : Exception {
		public InjectionException(string message) : base(message) { }

		protected InjectionException(SerializationInfo info, StreamingContext context) : base(info, context) {
			/* nothing extra to re-create */
		}
	}

	[Serializable]
	public class PatchingException : Exception {
		protected PatchingException(SerializationInfo info, StreamingContext context) : base(info, context) {
			this.CausedBy = (PatchAttribute) info.GetValue("CausedByAttribute", typeof(PatchAttribute));
		}

		override public void GetObjectData(SerializationInfo info, StreamingContext context) {
			base.GetObjectData(info, context);
			info.AddValue("CausedByAttribute", this.CausedBy, typeof(PatchAttribute));
		}

		public readonly PatchAttribute CausedBy;

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
