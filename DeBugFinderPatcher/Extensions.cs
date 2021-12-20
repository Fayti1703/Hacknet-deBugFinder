using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml;
using Mono.Cecil;

namespace DeBugFinderPatcher
{
    internal static class Extensions
    {
        internal static void MakeFieldAccess(this FieldDefinition f, AccessLevel access = AccessLevel.Internal)
        {
            switch (access)
            {
                case AccessLevel.Private:
                    f.IsPrivate = true; break;
                case AccessLevel.Protected:
                    f.IsFamily = true; break;
                case AccessLevel.Public:
                    f.IsPublic = true; break;
                case AccessLevel.Internal:
                    f.IsAssembly = true; break;
                case AccessLevel.ProtectedInternal:
                    f.IsFamilyOrAssembly = true; break;
                case AccessLevel.PrivateProtected:
                    f.IsFamilyAndAssembly = true; break;
            }
        }

        internal static void MakeMethodAccess(this MethodDefinition m, AccessLevel access = AccessLevel.Internal)
        {
            switch (access)
            {
                case AccessLevel.Private:
                    m.IsPrivate = true; break;
                case AccessLevel.Protected:
                    m.IsFamily = true; break;
                case AccessLevel.Public:
                    m.IsPublic = true; break;
                case AccessLevel.Internal:
                    m.IsAssembly = true; break;
                case AccessLevel.ProtectedInternal:
                    m.IsFamilyOrAssembly = true; break;
                case AccessLevel.PrivateProtected:
                    m.IsFamilyAndAssembly = true; break;
            }
        }

        internal static void MakeNestedAccess(this TypeDefinition t, AccessLevel access = AccessLevel.Internal)
        {
            switch (access)
            {
                case AccessLevel.Private:
                    t.IsNestedPrivate = true; break;
                case AccessLevel.Protected:
                    t.IsNestedFamily = true; break;
                case AccessLevel.Public:
                    t.IsNestedPublic = true; break;
                case AccessLevel.Internal:
                    t.IsNestedAssembly = true; break;
                case AccessLevel.ProtectedInternal:
                    t.IsNestedFamilyOrAssembly = true; break;
                case AccessLevel.PrivateProtected:
                    t.IsNestedFamilyAndAssembly = true; break;
            }
        }

        internal static void AddAssemblyAttribute<T>(this AssemblyDefinition ad, params object[] attribArgs)
        {
            Type[] paramTypes = attribArgs.Length > 0 ? new Type[attribArgs.Length] : Type.EmptyTypes;
            int index = 0;
            foreach (object param in attribArgs)
            {
                paramTypes[index] = param.GetType();
                index++;
            }
            MethodReference attribCtor = ad.MainModule.ImportReference(typeof(T).GetConstructor(paramTypes));
            CustomAttribute attrib = new CustomAttribute(attribCtor);
            foreach (object param in attribArgs)
            {
                attrib.ConstructorArguments.Add(
                    new CustomAttributeArgument(ad.MainModule.ImportReference(param.GetType()), param)
                );
            }
            ad.CustomAttributes.Add(attrib);
        }

        /* File System stuff */
        public static FileInfo GetFile(this DirectoryInfo containingDirectory, string fileName) {
            return new FileInfo(containingDirectory.FullName + Path.DirectorySeparatorChar + fileName);
        }


        public static string LineInfoForExcept(this IXmlLineInfo info)
        {
            return !info.HasLineInfo() ? "" : $" (Line {info.LineNumber}:{info.LinePosition})";
        }

        /* deconstruct adapters for this wierd combination of C# >= 7.0 and .NET = 4.0  */
        public static void Deconstruct<K, V>(this KeyValuePair<K, V> pair, out K key, out V value) {
            key = pair.Key;
            value = pair.Value;
        }

        public static void Deconstruct<T1, T2>(this Tuple<T1, T2> tuple, out T1 Item1, out T2 Item2) {
            Item1 = tuple.Item1;
            Item2 = tuple.Item2;
        }

        public static void LoadAssembly(this AppDomain target, Assembly localAssembly) {
            Uri uri2 = new Uri(localAssembly.CodeBase);
            if(!uri2.IsFile)
                throw new ArgumentException("Cannot grab local assembly from network path.", nameof(localAssembly));
            target.Load(File.ReadAllBytes(uri2.LocalPath));
        }
    }
}
