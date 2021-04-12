using System;
using System.IO;
using Hacknet;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Media;

namespace Pathfinder {
	public class CachedTheme : IDisposable {
		
		internal int refCount;
		private string path;
		
		public CustomTheme ThemeData {
			get;
		}

		public Texture2D BackgroundTexture {
			get;
		}

		private CachedTheme(CustomTheme data, string path, Texture2D background) {
			ThemeData = data;
			this.path = path;
			BackgroundTexture = background;
		}

		public void ApplyToOS(OS os) {
			ThemeManager.LastLoadedCustomTheme = ThemeData;
			ThemeManager.switchTheme(os, OSTheme.HacknetBlue);
			ThemeManager.switchThemeLayout(os, ThemeData.GetThemeForLayout());
			ThemeManager.backgroundImage = BackgroundTexture;
			ThemeManager.customBackgroundImageLoadPath = ThemeData.backgroundImagePath;
			ThemeManager.backgroundNeedsDisposal = false;
			ThemeData.LoadIntoOS(os);
			ThemeManager.currentTheme = OSTheme.Custom;
			activeTheme = this;
		}

		public void Dispose() {
			Console.WriteLine("[CACHE] Disposing '{0}'", path);
			if(this != activeTheme)
				BackgroundTexture?.Dispose();
			else
				ThemeManager.backgroundNeedsDisposal = true;
		}

		public static CachedTheme preload(string themeDataPath) {
			Console.WriteLine("[CACHE] Preloading '{0}'", themeDataPath);
			CachedTheme cache = load(themeDataPath);
			cache.refCount = 1;
			return cache;
		}

		public static CachedTheme postLoad(string themeDataPath) {
			Console.WriteLine("[CACHE] Postloading '{0}'", themeDataPath);
			CachedTheme cache = load(themeDataPath);
			cache.refCount = -1;
			return cache;
		}

		private static CachedTheme load(string themeDataPath) {
			CustomTheme data = CustomTheme.Deserialize(themeDataPath);
			string path = Utils.GetFileLoadPrefix() + data.backgroundImagePath;
			if (!File.Exists(path))
				path = "Content/" + data.backgroundImagePath;
			Texture2D background = null;
			if(!File.Exists(path)) 
				return new CachedTheme(data,  themeDataPath, null);
			
			try {
				using FileStream stream = File.OpenRead(path);
				background = Texture2D.FromStream(GuiData.spriteBatch.GraphicsDevice, stream);
			} catch(Exception) { }

			return new CachedTheme(data, themeDataPath, background);
		}

		internal static CachedTheme activeTheme;
	}
}
