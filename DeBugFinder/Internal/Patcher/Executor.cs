#nullable enable
using System;
using System.Reflection;
using DeBugFinder.Attribute;
using DeBugFinder.Util;
using JetBrains.Annotations;
using Mono.Cecil;
using Mono.Cecil.Inject;

namespace DeBugFinder.Internal.Patcher
{
    internal static class Executor
    {
        [UsedImplicitly]
        internal static void Main(AssemblyDefinition gameAssembly)
        {

            // Retrieve the hook methods
            Type hooks = typeof(DeBugFinderHooks);
            PatchAttribute? attrib = null;
            try {
                foreach(MethodInfo meth in hooks.GetMethods()) {
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
                throw new PatchingException(attrib, except);
            }
        }

        private static void TryInject(this MethodDefinition def, MethodDefinition toInject, PatchAttribute attrib)
        {
            try
            {
                def.InjectWith(
                    toInject,
                    attrib.ILIndex,
                    null,
                    (InjectFlags)attrib.Flags,
                    attrib.After ? InjectDirection.After : InjectDirection.Before,
                    attrib.LocalIds);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error applying patch for '{attrib.MethodSig}'", ex);
            }
        }
    }
}
