#nullable enable

using System;
using System.Linq;
using JetBrains.Annotations;

namespace DeBugFinder.Attribute {
	[MeansImplicitUse]
	[AttributeUsage(AttributeTargets.Method)]
	[Serializable]
	public class PatchAttribute : System.Attribute {
		[Flags]
		public enum InjectFlags {
			None = 0,
			PassInvokingInstance = 0x01,
			ModifyReturn = 0x02,
			PassParametersVal = 0x04,
			PassParametersRef = 0x08
		}

		public readonly Type TargetType;
		public readonly string MethodName;
		public readonly Type[]? MethodArgs;
		public readonly int ILIndex;
		public readonly bool AfterInstruction;
		public readonly InjectFlags Flags;
		public readonly int[]? LocalIDs;

		public PatchAttribute(
			Type targetType, string methodName, Type[]? methodArgs = null,
			int ilIndex = 0, bool afterInstruction = false,
			InjectFlags flags = 0, int[]? localIDs = null
		) {
			this.TargetType = targetType;
			this.MethodName = methodName;
			this.MethodArgs = methodArgs;
			this.ILIndex = ilIndex;
			this.AfterInstruction = afterInstruction;
			this.Flags = flags;
			this.LocalIDs = localIDs;
		}

		public string MethodSig {
			get {
				string output = $"{this.TargetType.FullName}::{this.MethodName}";
				if(this.MethodArgs != null)
					output += "(" + string.Join(", ", this.MethodArgs.Select(x => x.FullName)) + ")";
				return output;
			}
		}
	}
}
