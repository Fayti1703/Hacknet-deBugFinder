using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Inject;
using Pathfinder.Attribute;
using Pathfinder.Util;

namespace Pathfinder.Internal.Patcher
{
    internal static class Executor
    {
        private struct MethodStore
        {
            public PatchAttribute Attribute;
            public MethodInfo Hook;
            public MethodDefinition Target;
            public MethodStore(MethodInfo hook, PatchAttribute attrib, MethodDefinition target) {
                Hook = hook;
                Attribute = attrib;
                Target = target;
            }
        }

        private static void AddOrCreateTo<T>(this Dictionary<string, List<T>> input, string key, T val)
        {
            if (!input.ContainsKey(key))
                input.Add(key, new List<T>());
            input[key].Add(val);
        }

        internal static void Main(AssemblyDefinition gameAssembly)
        {
            var injectedList = new List<string>();
            var depDict = new Dictionary<string, List<MethodStore>>();

            // Retrieve the hook methods
            var hooks = typeof(PathfinderHooks);
            MethodDefinition method;
            PatchAttribute attrib = null;
            string sig;
            try {
                foreach(var meth in hooks.GetMethods()) {
                    attrib = meth.GetFirstAttribute<PatchAttribute>();
                    if(attrib == null) continue;
                    sig = attrib.MethodSig;
                    if(sig == null) {
                        Console.WriteLine($"Null method signature found, skipping {nameof(PatchAttribute)} on method.");
                        continue;
                    }

                    method = gameAssembly.MainModule.GetType(attrib.TypeName)?.GetMethod(attrib.MethodName);
                    if(method == null) {
                        Console.WriteLine(
                            $"Method signature '{sig}' could not be found, method hook patching failed, skipping {nameof(PatchAttribute)} on '{sig}'."
                        );
                        continue;
                    }

                    if(attrib.DependentSig != null && !injectedList.Contains(attrib.DependentSig)) {
                        depDict.AddOrCreateTo(attrib.DependentSig, new MethodStore(meth, attrib, method));
                        continue;
                    }
                    
                    method.TryInject(
                        gameAssembly.MainModule.ImportReference(meth).Resolve(),
                        attrib
                    );

                    injectedList.Add(meth.Name);

                    if(depDict.TryGetValue(meth.Name, out var storeList)) {
                        foreach(var store in storeList)
                            store.Target.TryInject(
                                gameAssembly.MainModule.ImportReference(store.Hook).Resolve(),
                                store.Attribute
                            );
                        depDict.Remove(meth.Name);
                    }

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
                    attrib.Offset,
                    attrib.Tag,
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
