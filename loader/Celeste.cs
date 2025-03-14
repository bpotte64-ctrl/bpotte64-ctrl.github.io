using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.InteropServices;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Xna.Framework;

[assembly: System.Runtime.Versioning.SupportedOSPlatform("browser")]

public static partial class CelesteLoader
{
    private static void Main()
    {
        Console.WriteLine("Hi!");
    }

	[JSImport("requestframe", "Celeste.js")]
	public static partial Task requestframe(int frametime);

    [DllImport("Emscripten")]
    public extern static void wasm_func_viil(Int32 x, Int32 y, Int64 l);

    internal static void CallPinvokeFixers()
    {
        wasm_func_viil(0, 0, 0);
        MonoMod.CustomBankLoader.PinvokeFix();
    }

    [JSExport]
    internal static Task PreInit()
    {
        return Task.Run(() =>
        {
            try
            {
                CallPinvokeFixers();
                Console.WriteLine("fixed pinvoke");

                Environment.SetEnvironmentVariable("FNA_PLATFORM_BACKEND", "SDL3");
                Environment.SetEnvironmentVariable("MONOMOD_DEPENDENCY_REMOVE_PATCH", "0");
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("Error in PreInit()!");
                Console.Error.WriteLine(e);
                throw;
            }
        });
    }

    static Game game;
    static Assembly celeste;
    static FieldInfo RunApplication;

    [JSExport]
    internal static Task Init()
    {
        try
        {
            File.CreateSymbolicLink("/bin/Celeste.exe", "/libsdl/CustomCeleste.dll");
            File.CreateSymbolicLink("/bin/Celeste.dll", "/libsdl/CustomCeleste.dll");
            File.CreateSymbolicLink("/bin/Celeste.Mod.mm.dll", "/libsdl/Celeste.Mod.mm.dll");
            File.CreateSymbolicLink("/bin/MMHOOK_Celeste.dll", "/libsdl/MMHOOK_Celeste.dll");

            File.Copy("/libsdl/Celeste/Everest/Celeste.Mod.mm.dll", "/libsdl/Celeste.Mod.mm.dll", true);
            File.Copy("/libsdl/Celeste/Everest/MMHOOK_Celeste.dll", "/libsdl/MMHOOK_Celeste.dll", true);
            Console.WriteLine("created celeste symlinks");

            celeste = Assembly.LoadFrom("/libsdl/CustomCeleste.dll");
            Console.WriteLine($"CELESTE: {celeste}");

            AssemblyLoadContext.Default.ResolvingUnmanagedDll += (assembly, name) =>
            {
                if (name == "SDL2") name = "SDL3";
                return NativeLibrary.Load(name, assembly, null);
            };
            AssemblyLoadContext.Default.Resolving += (ctx, name) =>
            {
                try
                {
                    Assembly asm;
                    if (name.Name == "Celeste") asm = ctx.LoadFromAssemblyPath($"/libsdl/CustomCeleste.dll");
                    else if (name.Name == "Celeste.Mod.mm") asm = ctx.LoadFromAssemblyPath($"/libsdl/Celeste.Mod.mm.dll");
                    else asm = ctx.LoadFromAssemblyPath($"/libsdl/Celeste/Everest/{name.Name}.dll");
                    return asm;
                }
                catch (Exception err)
                {
                    return null;
                }
            };

            JsSplash.Init(celeste);

            var Celeste = celeste.GetType("Celeste.Celeste");
            Console.WriteLine($"Celeste.Celeste: {Celeste}");
            var Settings = celeste.GetType("Celeste.Settings");
            Console.WriteLine($"Celeste.Settings: {Settings}");
            var Engine = celeste.GetType("Monocle.Engine");
            Console.WriteLine($"Monocle.Engine: {Engine}");

            var MainThreadId = Celeste.GetField("_mainThreadId", BindingFlags.Static | BindingFlags.NonPublic);
            Console.WriteLine($"Celeste.Celeste._mainThreadId: {MainThreadId}");
            var AssemblyDirectory = Engine.GetField("AssemblyDirectory", BindingFlags.Static | BindingFlags.NonPublic);
            Console.WriteLine($"Engine.AssemblyDirectory: {AssemblyDirectory}");
            var SettingsInitialize = Settings.GetMethod("Initialize", BindingFlags.Static | BindingFlags.Public);
            Console.WriteLine($"Settings.Initialize: {SettingsInitialize}");
            var GameConstructor = Celeste.GetConstructor([]);
            Console.WriteLine($"Celeste.Celeste..ctor: {GameConstructor}");

            MainThreadId.SetValue(null, Thread.CurrentThread.ManagedThreadId);
            Console.WriteLine($"MAIN THREAD INITIALIZED");
            AssemblyDirectory.SetValue(null, "/");
            Console.WriteLine($"ASSEMBLY DIRECTORY SET");

            SettingsInitialize.Invoke(null, []);
            Console.WriteLine($"SETTINGS INITIALIZED");

            var Everest = celeste.GetType("Celeste.Mod.Everest");
            if (Everest != null)
            {
                Console.WriteLine($"EVEREST DETECTED: {Everest}");
                var ParseArgs = Everest.GetMethod("ParseArgs", BindingFlags.Static | BindingFlags.NonPublic);
                ParseArgs.Invoke(null, [new string[] { }]);
            }

            game = (Game)GameConstructor.Invoke([]);
            Console.WriteLine($"CELESTE CREATED");
            RunApplication = Celeste.GetField("RunApplication", BindingFlags.NonPublic | BindingFlags.Instance);
            Console.WriteLine($"RUNAPPLICATION FOUND");
        }
        catch (Exception e)
        {
            Console.Error.WriteLine("Error in Init()!");
            Console.Error.WriteLine(e);
            return Task.FromException(e);
        }
        return Task.Delay(0);
    }

    [JSExport]
    internal static Task Cleanup()
    {
        try
        {
            celeste.GetType("Celeste.RunThread").GetMethod("WaitAll").Invoke(null, []);
            game.Dispose();
            celeste.GetType("Celeste.Audio").GetMethod("Unload").Invoke(null, []);
        }
        catch (Exception e)
        {
            Console.Error.WriteLine("Error in Cleanup()!");
            Console.Error.WriteLine(e);
            return Task.FromException(e);
        }
        return Task.Delay(0);
    }

    [JSExport]
    internal static Task<bool> RunAFrame()
    {
        try
        {
            game.RunOneFrame();
        }
        catch (Exception e)
        {
            Console.Error.WriteLine("Error in RunOneFrame()!");
            Console.Error.WriteLine(e);
            return (Task<bool>)Task.FromException(e);
        }
        return Task.FromResult((bool)RunApplication.GetValue(game));
    }

    [JSExport]
    internal static async Task MainLoop()
    {
		Stopwatch stopwatch = new();
        try
        {
            while ((bool)RunApplication.GetValue(game))
            {
				stopwatch.Restart();
                game.RunOneFrame();
				stopwatch.Stop();
				await requestframe((int)stopwatch.ElapsedMilliseconds);
            }
        }
        catch (Exception e)
        {
            Console.Error.WriteLine("Error in MainLoop()!");
            Console.Error.WriteLine(e);
			throw;
        }
    }
}
