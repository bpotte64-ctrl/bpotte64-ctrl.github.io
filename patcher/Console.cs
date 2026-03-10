using System;
using MonoMod;

namespace Celeste.Wasm
{
    public static class Wasm_Console
    {
        public static ConsoleColor _ForegroundColor { get; set; } = ConsoleColor.Gray;
        public static ConsoleColor _BackgroundColor { get; set; } = ConsoleColor.Black;

        [MonoModLinkFrom("System.ConsoleColor System.Console::get_ForegroundColor()")]
        public static ConsoleColor get_ForegroundColor()
        {
            return _ForegroundColor;
        }

        [MonoModLinkFrom("System.Void System.Console::set_ForegroundColor(System.ConsoleColor)")]
        public static void set_ForegroundColor(ConsoleColor value)
        {
            _ForegroundColor = value;
        }

        [MonoModLinkFrom("System.ConsoleColor System.Console::get_BackgroundColor()")]
        public static ConsoleColor get_BackgroundColor()
        {
            return _BackgroundColor;
        }

        [MonoModLinkFrom("System.Void System.Console::set_BackgroundColor(System.ConsoleColor)")]
        public static void set_BackgroundColor(ConsoleColor value)
        {
            _BackgroundColor = value;
        }

        [MonoModLinkFrom("System.Void System.Console::ResetColor()")]
        public static void ResetColor()
        {
            _ForegroundColor = ConsoleColor.Gray;
            _BackgroundColor = ConsoleColor.Black;
        }
    }
}
