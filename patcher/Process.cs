namespace MonoMod
{
    [MonoModLinkFrom("System.Diagnostics.Process")]
    public class Wasm_Process
    {
        public static Wasm_Process GetCurrentProcess()
        {
            return new Wasm_Process();
        }

        public long WorkingSet64 { get { return long.MaxValue; } }
        public long VirtualMemorySize64 { get { return long.MaxValue; } }
    }
}
