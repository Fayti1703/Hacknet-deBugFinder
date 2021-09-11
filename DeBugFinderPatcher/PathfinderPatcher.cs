
#nullable enable

using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Mono.Cecil;

namespace DeBugFinderPatcher
{
    public enum AccessLevel
    {
        Private,
        Protected,
        Public,
        Internal,
        ProtectedInternal,
        PrivateProtected
    }
    
    public static class PatcherProgram
    {
        internal static int Main(string[] args)
        {
            char separator = Path.DirectorySeparatorChar;

            Console.WriteLine($"Executing Patcher { (args.Length > 0 ? $"with arguments:\n{{\n\t{string.Join(",\n\t", args)}\n}}" : "without arguments.") }");
            
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
                if(!skipLaunchers) {
                    FileInfo shellLauncher = exeDir.GetFile("Hacknet");
                    if(shellLauncher.Exists) {
                        string launcherContent;
                        using(FileStream input = shellLauncher.OpenRead()) {
                            using StreamReader reader = new StreamReader(input, Encoding.UTF8);
                            launcherContent = reader.ReadToEnd();
                        }
                        
                        launcherContent = launcherContent.Replace("Hacknet", "Hacknet-deBugFinder");
                        
                        using(FileStream output = exeDir.GetFile("Hacknet-deBugFilder").OpenWrite()) {
                            using StreamWriter writer = new StreamWriter(output, Encoding.UTF8);
                            writer.Write(launcherContent);
                        }
                    }

                    foreach(string extension in new[] {
                        "x86",
                        "x86_64",
                        "osx"
                    }) {
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

            if (gameAssembly == null) throw new InvalidDataException("Hacknet Assembly could not be found");
            gameAssembly.Name.Name = "Hacknet-deBugFinder";

            try
            {
                // Add DeBugFinder internal attribute hack
                gameAssembly.AddAssemblyAttribute<InternalsVisibleToAttribute>("DeBugFinder");
                // Remove internal visibility from types
                gameAssembly.RemoveInternals();

                // Run Patcher Tasks
                foreach(TypeTaskItem task in TaskReader.readTaskListFile(debugfinderDir.GetFile("PatcherCommands.xml").FullName)) 
                    task.execute(gameAssembly.MainModule);
            }
            catch (Exception ex)
            {
                HandleException("Failure during Hacknet DeBugFinder Assembly Tweaks:", ex);
                gameAssembly.Write(exeDir.GetFile("Hacknet-deBugFinder.exe").FullName);
                return 3;
            }

            if(spitOutHacknetOnly) {
                gameAssembly.Write(debugfinderDir.GetFile("Hacknet-deBugFinder.exe").FullName);
                return 0;
            }

            try {
                using(MemoryStream stream = new MemoryStream()) {
                    gameAssembly.Write(stream);
                    Assembly.Load(stream.GetBuffer());
                }

                Assembly mainDll = Assembly.LoadFrom(debugfinderDir.GetFile("DeBugFinder.dll").FullName);

                MethodInfo? executorMethod = mainDll.GetType("DeBugFinder.Internal.Patcher.Executor")
                    ?.GetMethod("Main", BindingFlags.Static | BindingFlags.NonPublic);
                
                if(executorMethod == null)
                    throw new Exception("Could not find 'DeBugFinder.Internal.Patcher.Executor::Main'!");
                
                executorMethod.Invoke(null, new object[] { gameAssembly });

            } catch(Exception ex) {
                HandleException("Failure during DeBugFinder.dll's Patch Execution:", ex);
                return 1;
            }
            
            Console.WriteLine("Writing " + exeDir.GetFile("Hacknet-deBugFinder.exe").FullName);
            gameAssembly.Write(exeDir.GetFile("Hacknet-deBugFinder.exe").FullName);
            return 0;
        }

        private static void HandleException(string message, Exception e)
        {
            Console.WriteLine(message);
            Console.WriteLine(e);
            Console.WriteLine("Press any enter to terminate...");
            Console.ReadLine();
        }
    }
}
