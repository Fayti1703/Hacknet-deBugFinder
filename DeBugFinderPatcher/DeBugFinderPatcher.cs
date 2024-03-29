#nullable enable

using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security;
using System.Security.Permissions;
using System.Security.Policy;
using System.Text;
using System.Text.RegularExpressions;
using Mono.Cecil;

namespace DeBugFinderPatcher {
	public enum AccessLevel {
		Private,
		Protected,
		Public,
		Internal,
		ProtectedInternal,
		PrivateProtected
	}

	public static class PatcherProgram {
		internal static int Main(string[] args) {
			char separator = Path.DirectorySeparatorChar;

			string? debugfinderPath = null;
			string? exePath = null;
			bool spitOutHacknetOnly = false;
			bool skipLaunchers = false;
			ArrayCursor<string> argsCursor = new ArrayCursor<string>(args);
			try {
				while(!argsCursor.AtEnd()) {
					string arg = argsCursor.GetCurrent()!;
					switch(arg) {
						/* the DeBugFinder.dll's directory */
						case "-debugfinderDir":
							argsCursor.MoveNext();
							if(argsCursor.AtEnd())
								throw new Exception($"Erroneous no-parameter '{arg}' option");
							debugfinderPath = argsCursor.GetCurrent() + separator;
							break;
						/* the Hacknet.exe's directory */
						case "-exeDir":
							argsCursor.MoveNext();
							if(argsCursor.AtEnd())
								throw new Exception($"Erroneous no-parameter '{arg}' option");
							exePath = argsCursor.GetCurrent() + separator;
							break;
						/* spit type access level modifications without injected code */
						case "-spit":
							spitOutHacknetOnly = true;
							break;
						/* don't mess with the shell scripts or MonoKickstart executable */
						case "-nolaunch":
							skipLaunchers = true;
							break;
					}

					argsCursor.MoveNext();
				}
			} catch(Exception e) {
				Console.WriteLine("Error parsing your arguments: {0}", e);
				return 125;
			}

			AssemblyDefinition gameAssembly;
			DirectoryInfo debugfinderDir = new DirectoryInfo(debugfinderPath ?? ".");
			DirectoryInfo exeDir = new DirectoryInfo(exePath ?? ".");
			try {
				if(!skipLaunchers && !spitOutHacknetOnly) {
					FileInfo shellLauncher = exeDir.GetFile("Hacknet");
					if(shellLauncher.Exists) {
						string launcherContent;
						using(FileStream input = shellLauncher.OpenRead()) {
							using StreamReader reader = new StreamReader(input, Encoding.UTF8);
							launcherContent = reader.ReadToEnd();
						}

						/* 1. Adds `TERM=xterm` so old Mono doesn't need read new terminfo files (it fails at startup in that case)
						 * 2. Changes the launched file name to from `Hacknet.bin.<whatever>` to `Hacknet-deBugFinder.bin.<whatever>`
						 * 3. Quotes the `$@` expansion so we don't word split arguments irrecoverably.
						 */
						launcherContent = Regex.Replace(launcherContent, "(\\s*)(.+?) [.]/Hacknet[.]bin(.+?) [$]@",
							match => {
								string indent = match.Groups[1].Captures[0].Value;
								string prefix = match.Groups[2].Captures[0].Value;
								string binType = match.Groups[3].Captures[0].Value;
								if(!prefix.Contains("TERM"))
									prefix = "TERM=xterm " + prefix;
								return $"{indent}{prefix} Hacknet-deBugFinder.bin{binType} \"$@\"";
							}
						);

						using(FileStream output = exeDir.GetFile("Hacknet-deBugFinder").OpenWrite()) {
							using StreamWriter writer = new StreamWriter(output, Encoding.UTF8);
							writer.Write(launcherContent);
						}
					}

					foreach(string extension in new[] { "x86", "x86_64", "osx" }) {
						FileInfo kickstartExe = exeDir.GetFile($"Hacknet.bin.{extension}");
						if(kickstartExe.Exists)
							kickstartExe.CopyTo(exeDir.GetFile($"Hacknet-deBugFinder.bin.{extension}").FullName, true);
					}
				}

				// Load Hacknet.exe's assembly
				gameAssembly = AssemblyDefinition.ReadAssembly(exeDir.GetFile("Hacknet.exe").FullName, new ReaderParameters(ReadingMode.Deferred));
			} catch(Exception ex) {
				HandleException("Failure at Assembly Loading:", ex);
				return 2;
			}

			if(gameAssembly == null) throw new InvalidDataException("Hacknet Assembly could not be found");
			gameAssembly.Name.Name = "Hacknet-deBugFinder";

			try {
				// Add DeBugFinder internal attribute hack
				gameAssembly.AddAssemblyAttribute<InternalsVisibleToAttribute>("DeBugFinder");

				// Run Patcher Tasks
				foreach(TypeTaskItem task in TaskReader.readTaskListFile(debugfinderDir.GetFile("PatcherCommands.xml").FullName))
					task.execute(gameAssembly.MainModule);
			} catch(Exception ex) {
				HandleException("Failure during Hacknet DeBugFinder Assembly Tweaks:", ex);
				gameAssembly.Write(exeDir.GetFile("Hacknet-deBugFinder.exe").FullName);
				return 3;
			}

			if(spitOutHacknetOnly) {
				Console.WriteLine("Spit -- writing " + debugfinderDir.GetFile("Hacknet-deBugFinder.exe").FullName);
				gameAssembly.Write(debugfinderDir.GetFile("Hacknet-deBugFinder.exe").FullName);
				return 0;
			}

			AppDomain? mainDllDomain = null;
			DirectoryInfo? tempDir = null;
			byte[] finalData;
			try {
				tempDir = CreateTemporaryDirectory();
				if(tempDir == null) throw new IOException("Failed to create temporary sandbox directory");

				mainDllDomain = AppDomain.CreateDomain("Main Patcher Domain", null,
					new AppDomainSetup {
						DisallowCodeDownload = true,
						PrivateBinPath = "",
						ApplicationBase = tempDir.FullName,
						ApplicationTrust = new ApplicationTrust {
							DefaultGrantSet = new PolicyStatement(new PermissionSet(PermissionState.None)), IsApplicationTrustedToRun = true
						}
					});

				/* load main dll's dependencies */
				mainDllDomain.LoadAssembly(Assembly.GetAssembly(typeof(AssemblyDefinition)));
				mainDllDomain.Load(File.ReadAllBytes(debugfinderDir.GetFile("DeBugFinder.dll").FullName));
				mainDllDomain.Load(File.ReadAllBytes(exeDir.GetFile("FNA.dll").FullName));
				mainDllDomain.LoadAssembly(Assembly.GetExecutingAssembly());

				byte[] gameAssemblyData;
				using(MemoryStream stream = new MemoryStream()) {
					gameAssembly.Write(stream);
					gameAssemblyData = stream.ToArray();
				}

				mainDllDomain.Load(gameAssemblyData);

				CrossAppDomainCall crosser = (CrossAppDomainCall) mainDllDomain.CreateInstanceAndUnwrap(
					typeof(CrossAppDomainCall).Assembly.FullName,
					typeof(CrossAppDomainCall).FullName
				);
				finalData = crosser.Run(gameAssemblyData);
			} catch(Exception ex) {
				HandleException("Failure during DeBugFinder.dll's Patch Execution:", ex);
				return 1;
			} finally {
				if(mainDllDomain != null)
					AppDomain.Unload(mainDllDomain);
				tempDir?.Delete();
			}

			Console.WriteLine("Writing " + exeDir.GetFile("Hacknet-deBugFinder.exe").FullName);
			using FileStream outputStream = exeDir.GetFile("Hacknet-deBugFinder.exe").OpenWrite();
			outputStream.Write(finalData, 0, finalData.Length);
			return 0;
		}

		public static DirectoryInfo? CreateTemporaryDirectory() {
			DirectoryInfo? tempDir = null;
			for(int i = 0; i < 2000; i++) {
				string file = Path.GetTempFileName();
				File.Delete(file);

				try {
					Directory.CreateDirectory(file);
				} catch(IOException) {
					continue;
				}

				tempDir = new DirectoryInfo(file);
				break;
			}

			return tempDir;
		}

		private static void HandleException(string message, Exception e) {
			Console.WriteLine(message);
			Console.WriteLine(e);
			Console.WriteLine("Press any enter to terminate...");
			Console.ReadLine();
		}
	}

	internal class CrossAppDomainCall : MarshalByRefObject {
		public byte[] Run(byte[] gameData) {
			Assembly mainDll = Assembly.Load("DeBugFinder");
			MethodInfo? executorMethod = mainDll.GetType("DeBugFinder.Internal.Patcher.Executor")
				?.GetMethod("Main", BindingFlags.Static | BindingFlags.NonPublic);
			if(executorMethod == null)
				throw new Exception("Could not find 'DeBugFinder.Internal.Patcher.Executor::Main'!");
			return (byte[]) executorMethod.Invoke(null, new object[] { gameData });
		}
	}
}
