using Mono.Cecil;
using MonoMod.InlineRT;
using System.Collections.Generic;
using System;

namespace MonoMod
{
    static partial class MonoModRules
    {
        static Dictionary<string, string> FMODMappings = new() {
            { "FMOD_Studio_System_GetLowLevelSystem", "FMOD_Studio_System_GetCoreSystem" },
            { "FMOD_Studio_System_SetListenerAttributes", "FMOD_Studio_System_SetListenerAttributes_2" },
            { "FMOD_Studio_EventInstance_GetParameterValue", "FMOD_Studio_EventInstance_GetParameterByName" },
            { "FMOD_Studio_EventInstance_SetParameterValue", "FMOD_Studio_EventInstance_SetParameterByName_2" },
            { "FMOD_Studio_EventDescription_GetParameterCount", "FMOD_Studio_EventDescription_GetParameterDescriptionCount" },
            { "FMOD_Studio_EventDescription_GetParameterByIndex", "FMOD_Studio_EventDescription_GetParameterDescriptionByIndex" },
            { "FMOD_Studio_EventDescription_GetParameterByName", "FMOD_Studio_EventDescription_GetParameterDescriptionByName" },
            { "FMOD_Studio_EventDescription_GetParameter", "FMOD_Studio_EventDescription_GetParameterDescriptionByName" },
            { "FMOD_Studio_EventDescription_HasCue", "FMOD_Studio_EventDescription_HasSustainPoint" },
            { "FMOD_Studio_EventInstance_TriggerCue", "FMOD_Studio_EventInstance_KeyOff" },
        };

        static MonoModRules()
        {
            Console.WriteLine($"[Celeste.Wasm] Loaded into module {MonoModRule.Modder.Module}");
            MonoModRule.Modder.Log($"[Celeste.Wasm] Loaded into module {MonoModRule.Modder.Module}");
            MonoModRule.Modder.PostProcessors += FMODPostProcessor;
            MonoModRule.Modder.PostProcessors += GCPostProcessor;

            HackRelinkType("System.Net.Sockets.Socket", typeof(Celeste.Wasm.WasmSocket));
            HackRelinkType("System.Net.Sockets.NetworkStream", typeof(Celeste.Wasm.WasmNetworkStream));
        }

        // hacky relink without copying the type
        internal static void HackRelinkType(string source, Type dest)
        {
            MonoModRule.RelinkType(source, dest.FullName);
            foreach (var member in dest.GetMembers())
            {
                MonoModRule.RelinkMember($"{source}::{member.Name}", dest.FullName, member.Name);
            }
        }

        internal static void FMODPostProcessor(MonoModder modder)
        {
            foreach (TypeDefinition type in modder.Module.Types)
                foreach (MethodDefinition method in type.Methods)
                    FMODPostProcessMethod(modder, method);
        }
        internal static void FMODPostProcessMethod(MonoModder modder, MethodDefinition method)
        {
            if (!method.HasBody && method.HasPInvokeInfo && method.PInvokeInfo.Module.Name.StartsWith("fmod"))
            {
                modder.LogVerbose($"[FMODPatcher] Wrapping {method.FullName} -> {method.PInvokeInfo.Module.Name}::{method.PInvokeInfo.EntryPoint}");
                if (FMODMappings.TryGetValue(method.PInvokeInfo.EntryPoint, out var remapped))
                {
                    method.PInvokeInfo.EntryPoint = remapped;
                }
            }
        }

        internal static void GCPostProcessor(MonoModder modder)
        {
            GC.Collect();
            Console.WriteLine("[Celeste.Wasm] Forced GC after patch");
            MonoModRule.Modder.Log($"[Celeste.Wasm] Forced GC after patch");
        }
    }
}
