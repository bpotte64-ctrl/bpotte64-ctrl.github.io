using MonoMod;
using Microsoft.Xna.Framework;

namespace Monocle
{
	[MonoModIgnore]
    class patch_Engine
    {
        public static GraphicsDeviceManager Graphics { get; private set; }
    }
}

namespace Celeste
{
    public class patch_Settings
    {
        public int WindowScale;
        public bool Fullscreen;

        public extern void orig_ApplyScreen();
        public void ApplyScreen()
        {
            orig_ApplyScreen();

            Monocle.patch_Engine.Graphics.PreferredBackBufferWidth = WindowScale * 320;
            Monocle.patch_Engine.Graphics.PreferredBackBufferHeight = WindowScale * 180;
            Monocle.patch_Engine.Graphics.IsFullScreen = false;
            Monocle.patch_Engine.Graphics.ApplyChanges();
            Fullscreen = false;
        }
    }
}
