
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
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

		private static readonly Dictionary<string, CachedTheme> themeCache = new Dictionary<string, CachedTheme>();

		[Patch(
			"Hacknet.SASwitchToTheme.Trigger",
			flags: InjectFlags.PassInvokingInstance
		)]
		public static void onThemeSwitchTrigger(SASwitchToTheme self) {
			if(Enum.TryParse(self.ThemePathOrName, out OSTheme _))
				return;
			/* If already triggered (self.Delay ==~ 1.0f), don't increment refcount */
			if(Math.Abs(self.Delay + 1f) < 0.1f) return;
			if(!themeCache.TryGetValue(self.ThemePathOrName, out CachedTheme cache)) {
				cache = CachedTheme.preload(self.ThemePathOrName);
				themeCache.Add(self.ThemePathOrName, cache);
			} else {
				cache.refCount++;
				if(cache.refCount == 0)
					cache.refCount = 1;
			}
		}

		[Patch(
			"Hacknet.ThemeManager.switchTheme",
			flags: InjectFlags.PassParametersVal | InjectFlags.ModifyReturn
		)]
		public static bool onThemeSwap(object osObject, string customThemePath) {
			OS os = (OS) osObject;
			if(!themeCache.TryGetValue(customThemePath, out CachedTheme cache)) {
				cache = CachedTheme.postLoad(customThemePath);
				themeCache.Add(customThemePath, cache);
			}
			cache.ApplyToOS(os);
			os.RefreshTheme();
			return true;
		}

		private static void cacheClean(string themePath, bool refDown) {
			if(!themeCache.TryGetValue(themePath, out CachedTheme cache)) return;
			if(refDown) cache.refCount--;
			if(cache.refCount > 0) return;
			themeCache.Remove(themePath);
			cache.Dispose();
		}

		[Patch(
			"Hacknet.Effects.ActiveEffectsUpdater.CompleteThemeSwap",
			flags: InjectFlags.PassInvokingInstance | InjectFlags.PassParametersVal | InjectFlags.ModifyReturn
		)]
		public static bool onCompleteThemeSwap(ActiveEffectsUpdater self, object osObject) {
			OS os = (OS) osObject;
			bool bypass = false;
			if(self.newThemePath != null) {
				ThemeManager.setThemeOnComputer(os.thisComputer, self.newThemePath);
				ThemeManager.switchTheme(os, self.newThemePath);
				cacheClean(self.newThemePath,  refDown: true);
				CachedTheme cache = themeCache[self.newThemePath];
				cache.refCount--;
				if(cache.refCount <= 0) {
					themeCache.Remove(self.newThemePath);
					cache.Dispose();
				}
				bypass = true;
			}
			
			cacheClean(self.oldThemePath, refDown: false);

			return bypass;
		}

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
