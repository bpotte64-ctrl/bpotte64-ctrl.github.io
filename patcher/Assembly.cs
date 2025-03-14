using System.Reflection;
using MonoMod;

namespace Celeste.Wasm.NonEverestOnly
{
	[MonoModIgnore]
    public sealed class AssemblyPatch : Assembly
    {
        [MonoModLinkFrom("System.Reflection.Assembly System.Reflection.Assembly::GetEntryAssembly()")]
		public new static Assembly GetEntryAssembly() => Assembly.GetCallingAssembly();
    }
}
