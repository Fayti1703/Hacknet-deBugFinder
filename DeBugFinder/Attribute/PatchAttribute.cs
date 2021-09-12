#nullable enable

using System;
using JetBrains.Annotations;

namespace DeBugFinder.Attribute
{
    [MeansImplicitUse]
    [AttributeUsage(AttributeTargets.Method)]
    public class PatchAttribute : System.Attribute
    {
        [Flags]
        public enum InjectFlags
        {
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

        public readonly string? MethodSig;
        public readonly int ILIndex;
        public readonly int Flags;
        public readonly bool After;
        public readonly int[]? LocalIds;

        public PatchAttribute(string? sig, int ilIndex = 0, object? tag = null, InjectFlags flags = 0, bool before = false, int[]? localsID = null)
        {
            this.MethodSig = sig;
            this.ILIndex = ilIndex;
            this.Flags = (int)flags;
            this.After = before;
            this.LocalIds = localsID;
        }

        public string TypeName => MethodSig!.Remove(MethodSig.LastIndexOf('.'));
        public string MethodName => MethodSig!.Substring(MethodSig.LastIndexOf('.') + 1);
    }
}
