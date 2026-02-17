using MonoMod;
using System.Linq;

namespace Celeste
{
    public class patch_Credits
    {
        public class patch_Thanks
        {
            [MonoModConstructor]
            public extern void orig_ctor(int padding, string title, string[] people);

            [MonoModConstructor]
            public void ctor(int padding, string title, string[] people)
            {
                if (title == "Porting")
                {
                    orig_ctor(padding, title, people.Concat(["r58Playz (WASM)", "velzie (WASM)", "bomberfish (WASM)"]).ToArray());
                }
                else
                {
                    orig_ctor(padding, title, people);
                }
            }
        }
    }
}
