using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
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
            var separator = Path.DirectorySeparatorChar;

            Console.WriteLine($"Executing Patcher { (args.Length > 0 ? $"with arguments:\n{{\n\t{string.Join(",\n\t", args)}\n}}" : "without arguments.") }");

            string pathfinderDir = null, exeDir = "";
            var index = 0;
            var spitOutHacknetOnly = false;
            var skipLaunchers = false;
            foreach (var arg in args)
            {
                if (arg.Equals("-debugfinderDir")) // the DeBugFinder.dll's directory
                    pathfinderDir = args[index + 1] + Path.DirectorySeparatorChar;
                if (arg.Equals("-exeDir")) // the Hacknet.exe's directory
                    exeDir = args[index + 1] + separator;
                spitOutHacknetOnly |= arg.Equals("-spit"); // spit modifications without injected code
                skipLaunchers |= arg.Equals("-nolaunch");
                index++;
            }

            AssemblyDefinition gameAssembly;
            try
            {
                if(!skipLaunchers) {
                   if (File.Exists(exeDir + "Hacknet"))
                   {
                        File.Copy(exeDir + "Hacknet", exeDir + "Hacknet-deBugFinder", true);

                        string txt = File.ReadAllText(exeDir + "Hacknet");
                        txt = txt.Replace("Hacknet", "Hacknet-deBugFinder");

                       File.WriteAllText(exeDir + "Hacknet-deBugFinder", txt);
                   }

                   foreach (string n in new[]{
                       exeDir + "Hacknet.bin.x86",
                       exeDir + "Hacknet.bin.x86_64",
                       exeDir + "Hacknet.bin.osx"
                    }) {
                       if (File.Exists(n))
                           File.Copy(n, exeDir + "Hacknet-deBugFinder.bin" + Path.GetExtension(n), true);
                   }
                }
                // Loads Hacknet.exe's assembly
                gameAssembly = LoadAssembly(exeDir + "Hacknet.exe");
            }
            catch (Exception ex)
            {
                HandleException("Failure at Assembly Loading:", ex);
                return 2;
            }

            if (gameAssembly == null) throw new InvalidDataException("Hacknet Assembly could not be found");

            try
            {
                // Adds DeBugFinder internal attribute hack
                gameAssembly.AddAssemblyAttribute<InternalsVisibleToAttribute>("DeBugFinder");
                // Removes internal visibility from types
                gameAssembly.RemoveInternals();

                // Run Patcher Tasks
                foreach(TypeTaskItem task in TaskReader.readTaskListFile(new FileInfo(pathfinderDir + "PatcherCommands.xml").FullName))
                {
                    task.execute(gameAssembly.MainModule);
                }

            }
            catch (Exception ex)
            {
                HandleException("Failure during Hacknet DeBugFinder Assembly Tweaks:", ex);
                gameAssembly.Write("Hacknet-deBugFinder.exe");
                return 3;
            }
            if(!spitOutHacknetOnly) {
                try
                {
                    using (var stream = new MemoryStream())
                    {
                        gameAssembly.Write(stream);
                        Assembly.Load(stream.GetBuffer());
                        var assm = Assembly.LoadFrom(
                            new FileInfo(string.IsNullOrEmpty(pathfinderDir) ? "DeBugFinder.dll" : pathfinderDir + "DeBugFinder.dll").FullName);
                        var t = assm.GetType("DeBugFinder.Internal.Patcher.Executor");
                        t.GetMethod("Main", BindingFlags.Static | BindingFlags.NonPublic)
                            .Invoke(null, new object[] { gameAssembly });
                    }
                }
                catch(Exception ex)
                {
                    HandleException("Failure during DeBugFinder.dll's Patch Execution:", ex);
                    gameAssembly.Write("HacknetPathfinder.exe");
                    return 1;
                }
            }

            gameAssembly.Write("Hacknet-deBugFinder.exe");
            return 0;
        }

        private static void HandleException(string message, Exception e)
        {
            Console.WriteLine(message);
            Console.WriteLine(e);
            Console.WriteLine("Press any enter to terminate...");
            Console.ReadLine();
        }

        private static AssemblyDefinition LoadAssembly(string fileName, ReaderParameters parameters = null)
        {
            parameters = parameters ?? new ReaderParameters(ReadingMode.Deferred);
            if (string.IsNullOrEmpty(fileName))
                throw new ArgumentException($"{nameof(fileName)} is null/empty");
            Stream stream = new FileStream(fileName, FileMode.Open, parameters.ReadWrite ? FileAccess.ReadWrite : FileAccess.Read, FileShare.Read);
            if (parameters.InMemory)
            {
                var memoryStream = new MemoryStream(stream.CanSeek ? ((int)stream.Length) : 0);
                using (stream)
                {
                    stream.CopyTo(memoryStream);
                }
                memoryStream.Position = 0L;
                stream = memoryStream;
            }
            ModuleDefinition result;
            try
            {
                result = ModuleDefinition.ReadModule(stream, parameters);
            }
            catch (Exception)
            {
                stream.Dispose();
                throw;
            }
            return result.Assembly;
        }
    }
}
