#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using DeBugFinder.Attribute;
using DeBugFinder.Util;
using JetBrains.Annotations;
using Mono.Cecil;
using Mono.Cecil.Inject;

namespace DeBugFinder.Internal.Patcher {
	internal static class Executor {
		[UsedImplicitly]
		internal static byte[] Main(byte[] gameAssemblyData) {
			MemoryStream input = new MemoryStream(gameAssemblyData);
			AssemblyDefinition gameAssembly = AssemblyDefinition.ReadAssembly(input);
			pMain(gameAssembly);
			using MemoryStream output = new MemoryStream();
			gameAssembly.Write(output);
			gameAssembly.Dispose();
			input.Dispose();
			return output.ToArray();
		}

		internal static void pMain(AssemblyDefinition gameAssembly) {
			// Retrieve the hook methods
			Type hooks = typeof(DeBugFinderHooks);
			PatchAttribute? attrib = null;
			try {
				foreach(MethodInfo meth in hooks.GetMethods(BindingFlags.Static | BindingFlags.Public)) {
					attrib = meth.GetFirstAttribute<PatchAttribute>();
					if(attrib == null) continue;

					TypeDefinition targetType = gameAssembly.MainModule.GetType(attrib.TargetType.FullName);
					MethodDefinition? target = targetType.Methods.FirstOrDefault(candidate => {
						if(candidate.Name != attrib.MethodName) return false;
						if(attrib.MethodArgs == null) return true;
						if(candidate.Parameters.Count != attrib.MethodArgs.Length)
							return false;
						return !candidate.Parameters.Where((param, i) =>
							param.ParameterType.Resolve() != candidate.Module.ImportReference(attrib.MethodArgs[i])
						).Any();
					});
					if(target == null) {
						Console.WriteLine($"Cannot find appropriate method to hook '{meth.Name}' into.");
						continue;
					}

					target.TryInject(
						gameAssembly.MainModule.ImportReference(meth).Resolve(),
						attrib
					);
				}
			} catch(Exception except) {
				if(attrib == null || except is PatchingException)
					throw;
				throw new PatchingException(attrib, except.Message);
				// Mono.Cecil.Inject Exceptions aren't serializable :(
				// throw new PatchingException(attrib, except);
			}
		}

		private static void TryInject(this MethodDefinition def, MethodDefinition toInject, PatchAttribute attrib) {
			def.InjectWith(
				toInject,
				attrib.ILIndex,
				null,
				(InjectFlags) attrib.Flags,
				attrib.AfterInstruction ? InjectDirection.After : InjectDirection.Before,
				attrib.LocalIDs
			);
		}
	}
}
