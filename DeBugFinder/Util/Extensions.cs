using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hacknet;
using Mono.Cecil;

namespace DeBugFinder.Util {
	public static class Extensions {
		public static T GetFirstAttribute<InfoT, T>(this InfoT info, bool inherit = false)
			where InfoT : MemberInfo
			where T : System.Attribute
		{
			var attribs = info.GetCustomAttributes(typeof(T), inherit);
			return attribs.Length > 0 ? attribs[0] as T : null;
		}

		public static T GetFirstAttribute<T>(this MethodInfo info, bool inherit = false)
			where T : System.Attribute
			=> info.GetFirstAttribute<MethodInfo, T>(inherit);

		public static T GetFirstAttribute<T>(this FieldInfo info, bool inherit = false)
			where T : System.Attribute
			=> info.GetFirstAttribute<FieldInfo, T>(inherit);

		public static T GetFirstAttribute<T>(this Type info, bool inherit = false)
			where T : System.Attribute
			=> info.GetFirstAttribute<Type, T>(inherit);

		public static bool DLCchecked;

		public static void ThrowNoLabyrinths(this string input, bool inputOnly = false) {
			if(!CheckLabyrinths())
				throw new NotSupportedException("Labyrinths DLC not found.\n"
					+ (inputOnly
						? input
						: input + " requires Hacknet Labyrinths to be installed."));
		}

		public static bool CheckLabyrinths() {
			if(!DLCchecked) {
				DLC1SessionUpgrader.CheckForDLCFiles();
				DLCchecked = true;
			}

			return DLC1SessionUpgrader.HasDLC1Installed;
		}

		public static string RemoveExtended(this string str, int? startInd = null, int count = -1) {
			if(startInd == null
				|| startInd + (count > 0 ? count : 0) >= str.Length
				|| startInd - (count > 0 ? count : 0) <= -str.Length)
				return str;
			if(startInd < 0)
				startInd = str.Length + startInd.Value;
			if(count == -1)
				return str.Remove(startInd.Value);
			return str.Remove(startInd.Value, count);
		}

		public static string RemoveAll(this string str, string toRemove) => str.Replace(toRemove, string.Empty);

		public static string RemoveFirst(this string str, string toRemove) {
			var index = str.IndexOf(toRemove, StringComparison.Ordinal);
			return str.RemoveExtended(index < 0 ? (int?) null : index, toRemove.Length);
		}

		public static string RemoveLast(this string str, string toRemove) {
			var index = str.LastIndexOf(toRemove, StringComparison.Ordinal);
			return str.RemoveExtended(index < 0 ? (int?) null : index, toRemove.Length);
		}

		public static string BlankToNull(this string str)
			=> string.IsNullOrWhiteSpace(str) ? null : str;

		public static string ToAppendix(this string str, string prefix = ": ", string replacement = ".")
			=> string.IsNullOrEmpty(str) ? replacement : prefix + str;

		public static bool IsEmpty<T>(this ICollection<T> col)
			=> col.Count == 0;

		public static T LastOrNull<T>(this ICollection<T> col)
			=> col.IsEmpty() ? default : col.Last();


		public readonly struct Indexed<T> {
			public readonly int index;
			public readonly T value;

			public Indexed(int index, T value) {
				this.index = index;
				this.value = value;
			}

			public void Deconstruct(out int index, out T value) {
				index = this.index;
				value = this.value;
			}
		}

		public static IEnumerable<Indexed<T>> withIndex<T>(this IEnumerable<T> collection) {
			return collection.Select((value, index) => new Indexed<T>(index, value));
		}

		public static bool IsEquivalentTo(this TypeDefinition a, TypeDefinition b) {
			return a.FullName == b.FullName && a.Module.IsEquivalentTo(b.Module);
		}

		public static bool IsEquivalentTo(this ModuleDefinition a, ModuleDefinition b) {
			return a.Name == b.Name && a.Assembly.IsEquivalentTo(b.Assembly);
		}

		public static bool IsEquivalentTo(this AssemblyDefinition a, AssemblyDefinition b) {
			return a.FullName == b.FullName;
		}
	}
}
