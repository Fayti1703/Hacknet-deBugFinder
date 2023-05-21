using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hacknet;
using Microsoft.Xna.Framework;

namespace DeBugFinder {
	public static class ThemeVarManager {

		private static readonly HashSet<string> themeVars;

		static ThemeVarManager() {
			themeVars = new HashSet<string>(
				typeof(CustomTheme).GetFields().Select(x => x.Name)
					.Intersect(typeof(OS).GetFields().Select(x => x.Name))
			);
		}

		private static bool themeVarNameGuard(string varName) {
			if(themeVars.Contains(varName))
				return true;
			OS.currentInstance.write($"'{varName}' is not a valid theme variable name.");
			return false;
		}

		public static void SetThemeVarCommand(string[] argv) {
			if(argv.Length < 1) {
				OS.currentInstance.write("Syntax Error. Syntax: setthemevar <theme var name> <value...>");
				return;
			}
			string varName = argv[0];
			if(!themeVarNameGuard(varName)) return;
			FieldInfo field = typeof(OS).GetField(varName);
			if(field.FieldType == typeof(Color)) {
				if(argv.Length < 4) {
					OS.currentInstance.write($"Syntax Error. Syntax: setthemevar {varName} <r> <g> <b> [a=255]");
					return;
				}

				Color value;
				try {
					value = new Color {
						R = byte.Parse(argv[1]),
						G = byte.Parse(argv[2]),
						B = byte.Parse(argv[3]),
						A = argv.Length > 4 ? byte.Parse(argv[4]) : (byte) 255
					};
				} catch(FormatException) {
					OS.currentInstance.write("Semantical error: Invalid number format.");
					return;
				} catch(OverflowException) {
					OS.currentInstance.write("Semantical error: Number out of range.");
					return;
				}
				field.SetValue(OS.currentInstance, value);
			} else if(field.FieldType == typeof(string)) {
				if(argv.Length < 2) {
					OS.currentInstance.write($"Syntax Error. Syntax: setthemevar {varName} <string...>");
					return;
				}
				field.SetValue(OS.currentInstance, string.Join(" ", argv.Skip(1)));
			} else if(field.FieldType == typeof(bool)) {
				if(argv.Length < 2 || argv[1] != "true" && argv[1] != "false") {
					OS.currentInstance.write($"Syntax Error. Syntax: setthemevar {varName} <true/false>");
					return;
				}
				field.SetValue(OS.currentInstance, argv[1] == "true");
			} else
				OS.currentInstance.write($"Implementation error: Cannot set field of the {field.FieldType.FullName} type, no parser defined.");
		}

		public static void GetThemeVarCommand(string[] argv) {
			if(argv.Length < 1) {
				OS.currentInstance.write("Syntax Error. Syntax: getthemevar <theme var name>");
				return;
			}

			string varName = argv[0];
			if(!themeVarNameGuard(varName)) return;

			FieldInfo field = typeof(OS).GetField(varName);
			if(field.FieldType == typeof(Color)) {
				Color value = (Color) field.GetValue(OS.currentInstance);
				OS.currentInstance.write($"'{varName}' = Color({value.R}, {value.G}, {value.B}, {value.A})");
			} else if(field.FieldType == typeof(string)) {
				string value = (string) field.GetValue(OS.currentInstance);
				OS.currentInstance.write($"'{varName} = ({value.Length})\"{value}\"");
			} else if(field.FieldType == typeof(bool)) {
				bool value = (bool) field.GetValue(OS.currentInstance);
				OS.currentInstance.write($"{varName} = {(value ? "true" : "false")}");
			} else {
				OS.currentInstance.write($"{varName} = [value of the {field.FieldType.FullName} type]");
			}
		}
	}
}
