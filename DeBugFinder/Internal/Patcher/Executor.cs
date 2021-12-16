#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DeBugFinder.Attribute;
using DeBugFinder.Util;
using JetBrains.Annotations;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Collections.Generic;
using static DeBugFinder.Internal.Patcher.InjectionParameterMismatch;
using MethodBody = Mono.Cecil.Cil.MethodBody;

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

					try {
						MethodDefinition resolvedMethod = gameAssembly.MainModule.ImportReference(meth).Resolve();
						InjectMethodCall(target, resolvedMethod, attrib);
					} catch(Exception e) {
						throw new PatchingException(attrib, e);
					}
				}
			} catch(Exception except) {
				if(attrib == null || except is PatchingException)
					throw;
				throw new PatchingException(attrib, except.Message);
				// Mono.Cecil.Inject Exceptions aren't serializable :(
				// throw new PatchingException(attrib, except);
			}
		}

		private static void InjectMethodCall(MethodDefinition targetMethod, MethodReference injectedMethod, PatchAttribute attr) {
			if(!injectedMethod.Resolve().IsStatic) throw new ArgumentException("Injected method must be static", nameof(injectedMethod));
			MethodBody targetBody = targetMethod.Body ?? throw new ArgumentException("Target method has no body", nameof(targetMethod));
			Collection<Instruction> insts = targetBody.Instructions;
			int targetInstructionIndex = attr.ILIndex;
			if(targetInstructionIndex < 0) {
				targetInstructionIndex = insts.Count + targetInstructionIndex - 1;
				if(targetInstructionIndex < 0)
					throw new InjectionException("Out of range IL index");
			}

			if(attr.AfterInstruction)
				targetInstructionIndex++;
			if(targetInstructionIndex >= insts.Count)
				throw new InjectionException("Out of range IL index");

			/* create instructions */
			bool modifyReturn = (attr.Flags & PatchAttribute.InjectFlags.ModifyReturn) != 0;
			int paramCursor = 0;
			List<Instruction> toEmit = new List<Instruction>();
			VariableDefinition? returnVariable = null;
			if((attr.Flags & PatchAttribute.InjectFlags.PassInvokingInstance) != 0) {
				if(targetMethod.IsStatic)
					throw new InjectionException("Cannot pass invoking instance of a static method");
				if(paramCursor == injectedMethod.Parameters.Count)
					throw new InjectionException("Supposed to pass invoking instance, but injection method does not have a self parameter");
				ParameterDefinition injectParam = injectedMethod.Parameters[paramCursor];
				if(!injectParam.ParameterType.Resolve().IsEquivalentTo(targetMethod.DeclaringType)) {
					throw new InjectionParameterMismatch(
						new SourcePass(SourceType.Self, targetMethod.DeclaringType),
						new ParamPass(paramCursor, injectParam)
					);
				}

				paramCursor++;
				toEmit.Add(Instruction.Create(OpCodes.Ldarg_0));
			}

			if(modifyReturn) {
				if(!injectedMethod.ReturnType.Resolve().IsEquivalentTo(injectedMethod.Module.ImportReference(typeof(bool)).Resolve()))
					throw new InjectionException("Supposed to modify return, but injection method does not return bool");
				if(!targetMethod.ReturnType.Resolve().IsEquivalentTo(injectedMethod.Module.ImportReference(typeof(void)).Resolve())) {
					if(paramCursor == injectedMethod.Parameters.Count)
						throw new InjectionException("Supposed to modify return, but injection method does not have a return type parameter");
					ParameterDefinition injectParameter = injectedMethod.Parameters[paramCursor];
					if(!injectParameter.ParameterType.Resolve().IsEquivalentTo(targetMethod.ReturnType.Resolve())) {
						throw new InjectionParameterMismatch(
							new SourcePass(SourceType.Return, targetMethod.ReturnType),
							new ParamPass(paramCursor, injectParameter)
						);
					}

					if(!injectParameter.ParameterType.IsByReference || !injectParameter.IsOut)
						throw new InjectionException($"`return` parameter #{paramCursor} is not out");
					paramCursor++;
					returnVariable = new VariableDefinition(targetMethod.ReturnType);
					toEmit.Add(Instruction.Create(OpCodes.Ldloca, returnVariable));
				}
			} else {
				if(!injectedMethod.ReturnType.Resolve().IsEquivalentTo(injectedMethod.Module.ImportReference(typeof(void)).Resolve()))
					throw new InjectionException("Not supposed to modify return value, but injection method returns non-void");
			}

			EmitParameterLoads(toEmit, targetMethod, injectedMethod, attr, ref paramCursor);

			int[]? localsToGrab = attr.LocalIDs;

			if(localsToGrab != null)
				EmitLocalLoads(toEmit, targetMethod, injectedMethod, localsToGrab, ref paramCursor);

			if(paramCursor != injectedMethod.Parameters.Count)
				throw new InjectionException(
					$"Injection method has too many parameters. Must be {paramCursor}, got {injectedMethod.Parameters.Count}");

			toEmit.Add(Instruction.Create(OpCodes.Call, targetMethod.Module.ImportReference(injectedMethod)));

			Instruction targetInstruction = insts[targetInstructionIndex];

			if(modifyReturn) {
				toEmit.Add(Instruction.Create(OpCodes.Brfalse, targetInstruction));
				if(returnVariable != null) {
					targetBody.Variables.Add(returnVariable);
					toEmit.Add(Instruction.Create(OpCodes.Ldloc, returnVariable));
				}

				toEmit.Add(Instruction.Create(OpCodes.Ret));
			}

			int emitCursor = targetInstructionIndex;
			/* TODO(Cecil): Fucking let me insert a range */
			foreach(Instruction inst in toEmit)
				insts.Insert(emitCursor++, inst);
		}

		private static void EmitLocalLoads(
			ICollection<Instruction> toEmit,
			MethodDefinition targetMethod,
			IMethodSignature injectedMethod,
			IEnumerable<int> localIDs,
			ref int paramCursor
		) {
			MethodBody targetBody = targetMethod.Body;
			foreach(int varID in localIDs) {
				if(varID >= targetBody.Variables.Count)
					throw new InjectionException($"Target method has {targetBody.Variables.Count} locals, therefore local #{varID} does not exist.");
				VariableDefinition local = targetBody.Variables[varID];
				if(paramCursor == injectedMethod.Parameters.Count)
					throw new InjectionException("Supposed to pass locals, but injection method does not have enough parameters");
				ParameterDefinition injectedParam = injectedMethod.Parameters[paramCursor];
				TypeReference localType = local.VariableType;
				TypeReference injectedType = injectedParam.ParameterType;
				if(
					!localType.Resolve().IsEquivalentTo(injectedType.Resolve()) ||
					localType.IsByReference && !injectedType.IsByReference
				) {
					throw new InjectionParameterMismatch(
						new SourcePass(localType, varID),
						new ParamPass(paramCursor, injectedParam),
						"is not by-val-or-ref-compatible with"
					);
				}

				if(injectedParam.ParameterType.IsByReference) /* also catches `out` */
					toEmit.Add(Instruction.Create(varID < 256 ? OpCodes.Ldloca_S : OpCodes.Ldloca, local));
				else {
					toEmit.Add(varID switch {
						0 => Instruction.Create(OpCodes.Ldloc_0),
						1 => Instruction.Create(OpCodes.Ldloc_1),
						2 => Instruction.Create(OpCodes.Ldloc_2),
						3 => Instruction.Create(OpCodes.Ldloc_3),
						_ => Instruction.Create(varID < 256 ? OpCodes.Ldloc_S : OpCodes.Ldloc, local)
					});
				}

				paramCursor++;
			}
		}

		private static void EmitParameterLoads(
			ICollection<Instruction> toEmit,
			MethodDefinition targetMethod,
			IMethodSignature injectedMethod,
			PatchAttribute attr,
			ref int paramCursor
		) {
			bool isStatic = targetMethod.IsStatic;
			bool paramVals = (attr.Flags & PatchAttribute.InjectFlags.PassParametersVal) != 0;
			bool paramRefs = (attr.Flags & PatchAttribute.InjectFlags.PassParametersRef) != 0;

			if(!paramVals && !paramRefs) return;
			foreach((int index, ParameterDefinition param) in targetMethod.Parameters.withIndex()) {
				TypeDefinition targetType = param.ParameterType.Resolve();
				if(paramVals) {
					if(paramCursor == injectedMethod.Parameters.Count)
						throw new InjectionException("Supposed to pass parameters, but injection method does not have enough parameters");


					ParameterDefinition injectedParam = injectedMethod.Parameters[paramCursor];
					if(
						!targetType.IsEquivalentTo(injectedParam.ParameterType.Resolve()) ||
						injectedParam.ParameterType.IsByReference != targetType.IsByReference ||
						injectedParam.IsOut != param.IsOut
					)
						throw new InjectionParameterMismatch(new SourcePass(param, index), new ParamPass(paramCursor, injectedParam));

					int realIndex = isStatic ? index : index + 1;
					toEmit.Add(realIndex switch {
						0 => Instruction.Create(OpCodes.Ldarg_0),
						1 => Instruction.Create(OpCodes.Ldarg_1),
						2 => Instruction.Create(OpCodes.Ldarg_2),
						3 => Instruction.Create(OpCodes.Ldarg_3),
						_ => Instruction.Create(realIndex < 256 ? OpCodes.Ldarg_S : OpCodes.Ldarg, param)
					});
					paramCursor++;
				}

				if(paramRefs) {
					if(paramCursor == injectedMethod.Parameters.Count)
						throw new InjectionException("Supposed to pass parameters, but injection method does not have enough parameters");

					ParameterDefinition injectedParam = injectedMethod.Parameters[paramCursor];
					if(!injectedParam.ParameterType.IsByReference)
						throw new InjectionException($"Parameter #{paramCursor} of injection method is not by-ref");
					if(!targetType.IsEquivalentTo(injectedParam.ParameterType.Resolve()) || param.IsOut && !injectedParam.IsOut) {
						throw new InjectionParameterMismatch(
							new SourcePass(param, index),
							new ParamPass(paramCursor, injectedParam),
							"is not by-ref-compatible with"
						);
					}

					int realIndex = isStatic ? index + 1 : index;
					if(targetType.IsByReference) { /* already a reference, just load plainly */
						toEmit.Add(realIndex switch {
							0 => Instruction.Create(OpCodes.Ldarg_0),
							1 => Instruction.Create(OpCodes.Ldarg_1),
							2 => Instruction.Create(OpCodes.Ldarg_2),
							3 => Instruction.Create(OpCodes.Ldarg_3),
							_ => Instruction.Create(realIndex < 256 ? OpCodes.Ldarg_S : OpCodes.Ldarg, param)
						});
					} else
						toEmit.Add(Instruction.Create(realIndex < 256 ? OpCodes.Ldarga_S : OpCodes.Ldarga, param));

					paramCursor++;
				}
			}
		}
	}
}
