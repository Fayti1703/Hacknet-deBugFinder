
using System;
using System.IO;
using System.Reflection;
using System.Xml;
using Hacknet;
using Hacknet.Effects;
using Hacknet.Gui;
using Microsoft.Xna.Framework;
using Pathfinder.Attribute;
using static Pathfinder.Attribute.PatchAttribute;

namespace Pathfinder {
	/// <summary>
	/// Function hooks for the Pathfinder mod system
	/// </summary>
	/// Place all functions to be hooked into Hacknet here
	public static class PathfinderHooks {

		[Patch("Hacknet.CustomTheme.LoadIntoOS",
			flags: InjectFlags.PassInvokingInstance | InjectFlags.PassParametersVal | InjectFlags.ModifyReturn
		)]
		public static bool onLoadThemeIntoOS(CustomTheme self, object osObj) {
			OS os = (OS) osObj;
			FieldInfo[] fields = typeof(CustomTheme).GetFields();
			foreach(FieldInfo field in fields) {
				FieldInfo osField = typeof(OS).GetField(field.Name);
				if(osField != null)
					osField.SetValue(os, field.GetValue(self));
			}
			return true;
		}

		[Patch("Hacknet.Utils.DeserializeObject",
			flags: InjectFlags.PassParametersVal | InjectFlags.ModifyReturn
		)]
		public static bool onDeserializeGenericObject(out object retVal, Stream xmlStream, Type targetType) {
			using XmlReader reader = XmlReader.Create(xmlStream);
			object instance = Activator.CreateInstance(targetType);
			XmlNamespaceManager nsManager = new XmlNamespaceManager(new NameTable());
			while(!reader.EOF) {
				if(!string.IsNullOrWhiteSpace(reader.Name)) {
					FieldInfo field = targetType.GetField(reader.Name);
					if(field != null) {
						object value = null;
						if(field.FieldType == typeof(Color))
							value = Utils.convertStringToColor(reader.ReadElementContentAsString());
						value ??= reader.ReadElementContentAs(field.FieldType, nsManager);
						field.SetValue(instance, value);
					}
				}
				reader.Read();
			}
			retVal = instance;
			return true;
		}

		[Patch("Hacknet.MainMenu.DrawBackgroundAndTitle",
			7,
			flags: InjectFlags.PassInvokingInstance | InjectFlags.ModifyReturn | InjectFlags.PassLocals,
			localsID: new[] { 0 }
		)]
		public static bool onDrawMainMenuTitles(MainMenu self, out bool result, ref Rectangle dest) {
			result = true;
			FlickeringTextEffect.DrawLinedFlickeringText(
				new Rectangle(180, 120, 340, 100),
				"HACKNET",
				7f,
				0.55f,
				self.titleFont,
				null,
				self.titleColor,
				2
				);
			string versionInfo = "OS" + (DLC1SessionUpgrader.HasDLC1Installed ? "+Labyrinths " : " ") +
				MainMenu.OSVersion + "$$ ThemeOptimized! $$";
			TextItem.doFontLabel(new Vector2(520f, 178f), versionInfo, GuiData.smallfont, self.titleColor * 0.5f, 600f, 26f);
			if(!Settings.IsExpireLocked) return true;
			TimeSpan expireTime = Settings.ExpireTime - DateTime.Now;
			string text;
			if(expireTime.TotalSeconds < 1.0) {
				text = LocaleTerms.Loc("TEXT BUILD EXPIRED - EXECUTION DISABLED");
				result = false;
			} else
				text = "Test Build : Expires in " + expireTime;

			TextItem.doFontLabel(new Vector2(180f, 105f), text, GuiData.smallfont, Color.Red * 0.8f, 600f, 26f);
			return true;
		}
	}
}
