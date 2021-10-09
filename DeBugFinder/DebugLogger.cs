using System;
using System.Collections.Generic;
using System.Linq;
using Hacknet;
using JetBrains.Annotations;
using static DeBugFinder.DebugTag;

namespace DeBugFinder {
	public static class DebugLogger {

		public static readonly HashSet<DebugTag> enabledTags = new HashSet<DebugTag> {
			HacknetError
		};

		public static bool isEnabled(DebugTag tag) {
			return enabledTags.Contains(tag);
		}

		private static OS os => OS.currentInstance;

		public static void Log(DebugTag tag, string what) {
			if (!enabledTags.Contains(tag)) return;
			string output = $"[{tag}] {what}";
			Console.WriteLine(output);
			os?.write(output);
		}

		public static void Log(DebugTag tag, Func<string> producer) {
			if (!enabledTags.Contains(tag)) return;
			string output = $"[{tag}] {producer()}";
			Console.WriteLine(output);
			os.write(output);
		}

		public static string formatForLog(this string input) {
			return input == null ? "(null)" : $"'{input}'";
		}

		private static string normalizeTagName(string input) {
			return string.Join("",
				input.Split(new[] {
					'_'
				}, StringSplitOptions.RemoveEmptyEntries).Select(
					e => e.Substring(0, 1).ToUpperInvariant() + e.Substring(1)
				)
			);
		}

		private enum FormatOps {
			ADD,
			REMOVE,
			CHECK
		}

		private static string formatMsg(FormatOps op, string which, bool success) {
			string toFrom;
			string opStr;
			string failPostFix;
			switch (op) {
				case FormatOps.ADD:
					toFrom = "to";
					opStr = success ? "Added tag" : "Failed to add tag";
					failPostFix = "Perhaps it is already on?";
					break;
				case FormatOps.REMOVE:
					toFrom = "from";
					opStr = success ? "Removed tag" : "Failed to remove tag";
					failPostFix = "Perhaps it is already off?";
					break;
				case FormatOps.CHECK:
					return $"Tag {which} is{(success ? "" : "NOT")} in active set.";
				default:
					throw new ArgumentOutOfRangeException(nameof(op), op, "Enum fail!");
			}

			return $"{opStr} {which} {toFrom} active set.{(success ? "" : " " + failPostFix)}";
		}

		private static IEnumerable<IGrouping<int, T>> Partition<T>(this IEnumerable<T> target, int partitionSize) {
			return target.Select((x, i) => new {x, i}). // add index
				GroupBy(e => e.i / partitionSize, e => e.x); // group by index
		}

		[CanBeNull]
		public static string TryParseTag(string input, out DebugTag output, out string trueName) {
			trueName = normalizeTagName(input);
			if(Enum.TryParse(trueName, ignoreCase: true, out output))
				return null;
			string msg = $"The tag {trueName} was not found.";
			if(trueName != input)
				msg += $" (Normalized from your input of '{input}')";
			return msg;
		}

		public static void HacknetInterface(string[] args) {
			switch (args.Length) {
				case 0: {
					string stringTags = enabledTags.Count == 0
						? "<NONE>"
						: string.Join(",\n",
							enabledTags.Partition(16).Select(
								inner => string.Join(", ", inner)
							)
						);
					os.write("Enabled debug tags: " + stringTags);
					break;
				}
				case 1:
					if (args[0].ToLowerInvariant() == "list") {
						string allTags = string.Join(",", Enum.GetNames(typeof(DebugTag)));
						os.write("Available debug tags: " + allTags);
						break;
					}

					os.write("Usage: `detags [<tag> <on/off/get>]` or `detags list`");
					break;
				default:
					string errMsg = TryParseTag(args[0], out DebugTag target, out string tagName);
					if(errMsg != null) {
						os.write(errMsg);
						return;
					}

					switch (args[1]) {
						case "on":
						case "true":
						case "add":
							os.write(formatMsg(FormatOps.ADD, tagName, enabledTags.Add(target)));
							break;
						case "off":
						case "false":
						case "remove":
							os.write(formatMsg(FormatOps.REMOVE, tagName, enabledTags.Remove(target)));
							break;
						case "get":
						case "status":
							os.write(formatMsg(FormatOps.CHECK, tagName, enabledTags.Contains(target)));
							break;
						default:
							os.write("Invalid operation. If you think this is in error, edit source code.");
							break;
					}

					break;
			}
		}
	}
}
