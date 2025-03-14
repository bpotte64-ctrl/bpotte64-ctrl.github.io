using System;
using System.IO;
using System.Threading.Tasks;
using System.Runtime.InteropServices.JavaScript;
using System.Reflection;
using System.IO.Compression;
using Mono.Cecil;
using MonoMod;
using MonoMod.RuntimeDetour.HookGen;

public partial class Patcher
{

    [JSExport]
    internal static async Task<bool> PatchCeleste(bool installEverest)
    {
        try
        {
            if (File.Exists("/libsdl/CustomCeleste.dll"))
            {
                Console.WriteLine("CustomCeleste.dll found, skipping patcher");
                return true;
            }

            Patcher patcher;
            if (File.Exists("/libsdl/Celeste.dll"))
            {
                patcher = new("/libsdl/Celeste.dll");
            }
            else if (File.Exists("/libsdl/Celeste.exe"))
            {
                patcher = new("/libsdl/Celeste.exe");
                patcher.installEverest = installEverest;
            }
            else
            {
                throw new Exception("Celeste.dll or Celeste.exe not found!");
            }

            Console.WriteLine($"Patching Assembly {patcher._path}");
            patcher.patch();
            patcher.write("/libsdl/CustomCeleste.dll");
            return true;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine("Error in PatchCeleste()!");
            Console.Error.WriteLine(e);
            return false;
        }
    }

    [JSExport]
    internal static async Task<bool> ExtractEverest()
    {
        try
        {
            string everestPath = "/libsdl/Celeste/Everest/";
            Directory.CreateDirectory(everestPath);
            using (ZipArchive archive = ZipFile.OpenRead("/libsdl/everest.zip"))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (entry.FullName.EndsWith("/")) continue;
                    string path = everestPath + entry.FullName.Substring(entry.FullName.IndexOf('/') + 1);
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    entry.ExtractToFile(path, true);
                }
            }

            File.Delete("/libsdl/everest.zip");
            return true;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine("Error in ExtractEverest()!");
            Console.Error.WriteLine(e);
            return false;
        }
    }

    public ModuleDefinition Module;
    public string _path;
    public bool installEverest = false;

    public Patcher(string path)
    {
        _path = path;
        ReaderParameters readerParams = new(ReadingMode.Immediate) { ReadSymbols = false, InMemory = true };
        MemoryStream stream = new(File.ReadAllBytes(path));
        Module = ModuleDefinition.ReadModule(stream, readerParams);
    }

    public void patch()
    {
        if (installEverest)
        {
            ModuleDefinition everest = ModuleDefinition.ReadModule("/libsdl/Celeste/Everest/Celeste.Mod.mm.dll");
            using (MonoModder modder = new()
            {
                Module = Module,
                Mods = [everest],
                MissingDependencyThrow = false,
            })
            {
                modder.DependencyDirs.Add("/bin");
                modder.DependencyDirs.Add("/libsdl/Celeste/Everest");
                modder.Log("Converting Celeste to NET Core");
                NETCoreifier.Coreifier.ConvertToNetCore(modder);

                modder.Log("Installing Everest");
                modder.MapDependencies();
                modder.AutoPatch();
            }

            using (MonoModder modder = new()
            {
                Module = Module,
                MissingDependencyThrow = false,
            })
            {
                modder.DependencyDirs.Add("/bin");
                modder.DependencyDirs.Add("/libsdl/Celeste/Everest");
                modder.MapDependencies();
                modder.Log("Generating MMHOOK_Celeste.dll");
                string pathOut = "/libsdl/Celeste/Everest/MMHOOK_Celeste.dll";
                var gen = new HookGenerator(modder, Path.GetFileName(pathOut))
                {
                    HookPrivate = true,
                };
                using (var mOut = gen.OutputModule)
                {
                    gen.Generate();

                    // we're supposed to run everest again on the mmhook? i don't feel like doing that
                    // it's only for monomod crimes so we should be fine
                    mOut.Write(pathOut);
                }
            }
        }


        ModuleDefinition wasmMod = ModuleDefinition.ReadModule("/bin/Celeste.Wasm.mm.dll");
        if (!installEverest)
        {
            var ignore = wasmMod.ImportReference(typeof(MonoMod.MonoModIgnore).GetConstructor([]));
			Console.WriteLine($"IGNORING: {ignore}");
            foreach (var type in wasmMod.GetTypes())
            {
                if (type.Namespace.StartsWith("Celeste.Mod"))
                    type.CustomAttributes.Add(new(ignore));
            }
			foreach (var type in wasmMod.GetTypes())
			{
				if (type.Namespace == "Celeste.Wasm.NonEverestOnly")
					type.CustomAttributes.Clear();
			}
		}

        using (MonoModder modder = new()
        {
            Module = Module,
            Mods = [wasmMod],
            MissingDependencyThrow = false,
        })
        {
            modder.DependencyDirs.Add("/bin");
            modder.DependencyDirs.Add("/libsdl/Celeste/Everest");

            modder.Log("Installing WASM patches");
            modder.MapDependencies();
            modder.DependencyMap[modder.Module].Add(wasmMod);

            modder.AutoPatch();
        }

        Module.AssemblyReferences.Add(wasmMod.Assembly.Name);
    }

    public void write(string path)
    {
        Module.Write(path, new WriterParameters() { WriteSymbols = false });
    }
}
