using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod;
using MonoMod.Cil;
using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;

namespace Celeste.Mod
{
    public static partial class patch_Everest
    {
        public static class patch_Relinker
        {
            private static void SeedDependencyCache(MonoModder modder, ModuleDefinition module)
            {
                string asmName = module.Assembly?.Name?.Name;
                string asmFullName = module.Assembly?.FullName;
                string moduleName = module.Name;

                if (!string.IsNullOrEmpty(asmName))
                {
                    modder.DependencyCache[asmName] = module;
                    modder.RelinkModuleMap[asmName] = module;
                }

                if (!string.IsNullOrEmpty(asmFullName))
                    modder.DependencyCache[asmFullName] = module;

                if (!string.IsNullOrEmpty(moduleName))
                {
                    modder.DependencyCache[moduleName] = module;
                    modder.RelinkModuleMap[moduleName] = module;

                    if (moduleName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                    {
                        string moduleNameWithoutExt = Path.GetFileNameWithoutExtension(moduleName);
                        modder.DependencyCache[moduleNameWithoutExt] = module;
                        modder.RelinkModuleMap[moduleNameWithoutExt] = module;
                    }
                }
            }

            private static extern void orig_InitMMFlags(MonoModder modder);
            private static void InitMMFlags(MonoModder modder)
            {
                orig_InitMMFlags(modder);

                modder.DependencyDirs.Add("/bin/");
                ModuleDefinition wasmRules = ModuleDefinition.ReadModule("/bin/Celeste.Wasm.mm.dll");;

                if (!modder.Mods.Any(m => string.Equals(m.Name, wasmRules.Name, StringComparison.OrdinalIgnoreCase)))
                    modder.Mods.Add(wasmRules);

				// load it again as a dep to make sure monomod doesn't mess it up
				wasmRules = ModuleDefinition.ReadModule("/bin/Celeste.Wasm.mm.dll");

                SeedDependencyCache(modder, wasmRules);
                modder.MapDependencies(wasmRules);
                modder.RemovePatchReferences = false;
            }

            [MonoModIgnore]
            [PatchRelinkModuleMapPath]
            public extern static Dictionary<string, ModuleDefinition> get_SharedRelinkModuleMap();
        }

        [MonoModIgnore]
        public extern static byte[] ComputeHash(Stream inputStream);

        public static Func<string, byte[]> CelesteWasm_XXHash64Fast;

        // forward to js
        public static byte[] GetChecksum(string path)
        {
            if (CelesteWasm_XXHash64Fast != null)
            {
                var res = CelesteWasm_XXHash64Fast(path);
                if (res != null)
                    return res;
            }

            using (FileStream fs = File.OpenRead(path))
            {
                return ComputeHash(fs);
            }
        }
    }

    public sealed class patch_EverestModuleAssemblyContext
    {
        private static readonly Dictionary<string, AssemblyDefinition> _GlobalAssemblyResolveCache = new Dictionary<string, AssemblyDefinition>();

        private extern AssemblyDefinition orig_ResolveGlobal(AssemblyNameReference asmName);

        private AssemblyDefinition ResolveGlobal(AssemblyNameReference asmName)
        {
            if (!_GlobalAssemblyResolveCache.TryGetValue(asmName.Name, out AssemblyDefinition def))
            {
                try
                {
                    def = ModuleDefinition.ReadModule($"/bin/{asmName.Name}.dll").Assembly;
                    _GlobalAssemblyResolveCache.Add(asmName.Name, def);
                }
                catch { }
            }

            def = orig_ResolveGlobal(asmName);
            return def;
        }
    }
}

namespace MonoMod
{
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchRelinkModuleMapPath))]
    class PatchRelinkModuleMapPathAttribute : Attribute { }

    static partial class MonoModRules
    {
        public static void PatchRelinkModuleMapPath(ILContext context, CustomAttribute attrib)
        {
            int matchedcnt = 0;

            foreach (var instr in context.Instrs)
            {
                if (instr.MatchCallOrCallvirt(out var mref) && mref.Name == "get_PathGame")
                {
                    instr.OpCode = OpCodes.Ldstr;
                    instr.Operand = "/bin/";
                    matchedcnt++;
                }
            }

            if (matchedcnt != 2) throw new Exception("Failed to match get_PathGame twice!");
        }
    }
}
