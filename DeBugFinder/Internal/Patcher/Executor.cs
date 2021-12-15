#nullable enable
using System;
using System.IO;
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
					string? signature = attrib.MethodSig;
					if(signature == null) {
						Console.WriteLine($"Null method signature found, skipping {nameof(PatchAttribute)} on method.");
						continue;
					}

					MethodDefinition? method = gameAssembly.MainModule.GetType(attrib.TypeName)?.GetMethod(attrib.MethodName);
					if(method == null) {
						Console.WriteLine(
							$"Method signature '{signature}' could not be found, method hook patching failed, skipping {nameof(PatchAttribute)} on '{signature}'."
						);
						continue;
					}

					method.TryInject(
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
				attrib.After ? InjectDirection.After : InjectDirection.Before,
				attrib.LocalIds);
		}
	}
}
