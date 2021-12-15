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
			None = 0x0,
			PassTag = 0x1,
			PassInvokingInstance = 0x2,
			ModifyReturn = 0x4,
			PassLocals = 0x8,
			PassFields = 0x10,
			PassParametersVal = 0x20,
			PassParametersRef = 0x40,
			PassStringTag = 0x80,
			All_Val = 0x3E,
			All_Ref = 0x5E
		}

		public readonly Type TargetType;
		public readonly string MethodName;
		public readonly Type[]? MethodArgs;
		public readonly int ILIndex;
		public readonly bool AfterInstruction;
		public readonly int Flags;
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
			this.Flags = (int)flags;
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
