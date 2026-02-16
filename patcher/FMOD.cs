using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Mono.Cecil;
using MonoMod;
using MonoMod.Cil;
using MonoMod.InlineRT;
using FMOD;

namespace FMOD
{
    public struct StringWrapper { }
    public enum RESULT { OK, }
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate RESULT REAL_FILE_CLOSECALLBACK(IntPtr handle, IntPtr userdata);
    // ref uint filesize, ref IntPtr handle
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate RESULT REAL_FILE_OPENCALLBACK(StringWrapper name, IntPtr filesize, IntPtr handle, IntPtr userdata);
    // ref uint bytesread
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate RESULT REAL_FILE_READCALLBACK(IntPtr handle, IntPtr buffer, uint sizebytes, IntPtr bytesread, IntPtr userdata);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate RESULT REAL_FILE_SEEKCALLBACK(IntPtr handle, uint pos, IntPtr userdata);

    public delegate RESULT FILE_OPENCALLBACK(StringWrapper name, ref uint filesize, ref IntPtr handle, IntPtr userdata);
    public delegate RESULT FILE_CLOSECALLBACK(IntPtr handle, IntPtr userdata);
    public delegate RESULT FILE_READCALLBACK(IntPtr handle, IntPtr buffer, uint sizebytes, ref uint bytesread, IntPtr userdata);
    public delegate RESULT FILE_SEEKCALLBACK(IntPtr handle, uint pos, IntPtr userdata);

    public enum SPEAKERMODE
    {
        DEFAULT,
        RAW,
        MONO,
        STEREO,
        QUAD,
        SURROUND,
        _5POINT1,
        _7POINT1,
        _7POINT1POINT4,
        MAX
    }

    public class System
    {

        [MonoModIgnore]
        public extern RESULT getDriverInfo(int id, StringBuilder name, int namelen, out Guid guid, out int systemrate, out SPEAKERMODE speakermode, out int speakermodechannels);

        [MonoModIgnore]
        public extern RESULT setSoftwareFormat(int samplerate, SPEAKERMODE speakermode, int numrawspeakers);

        [MonoModIgnore]
        public extern RESULT setDSPBufferSize(uint bufferlength, int numbuffers);
    }
}
namespace FMOD.Studio
{
    [MonoModIgnore]
    public class Bank { protected IntPtr rawPtr; public Bank(IntPtr raw) { rawPtr = raw; } }

    public struct REAL_BANK_INFO
    {
        public int size;

        public IntPtr userdata;

        public int userdatalength;

        public REAL_FILE_OPENCALLBACK opencallback;

        public REAL_FILE_CLOSECALLBACK closecallback;

        public REAL_FILE_READCALLBACK readcallback;

        public REAL_FILE_SEEKCALLBACK seekcallback;
    }
    public struct BANK_INFO
    {
        public int size;

        public IntPtr userdata;

        public int userdatalength;

        public FILE_OPENCALLBACK opencallback;

        public FILE_CLOSECALLBACK closecallback;

        public FILE_READCALLBACK readcallback;

        public FILE_SEEKCALLBACK seekcallback;
    }

    [Flags]
    public enum LOAD_BANK_FLAGS : uint
    {
        NORMAL = 0u,
        NONBLOCKING = 1u,
        DECOMPRESS_SAMPLES = 2u
    }

    public class System
    {
        [DllImport("fmodstudio")]
        private static extern RESULT FMOD_Studio_System_LoadBankCustom(IntPtr studiosystem, ref REAL_BANK_INFO info, LOAD_BANK_FLAGS flags, out IntPtr bank);

        public RESULT loadBankCustom(BANK_INFO info, LOAD_BANK_FLAGS flags, out Bank bank)
        {
            bank = null;
            info.size = Marshal.SizeOf(info);
            IntPtr bank2 = default(IntPtr);

            CustomBankLoader.MODOPEN = info.opencallback;
            CustomBankLoader.MODCLOSE = info.closecallback;
            CustomBankLoader.MODREAD = info.readcallback;
            CustomBankLoader.MODSEEK = info.seekcallback;

            REAL_BANK_INFO real = new REAL_BANK_INFO()
            {
                size = info.size,
                userdata = info.userdata,
                userdatalength = info.userdatalength,

                opencallback = CustomBankLoader.OPEN,
                closecallback = CustomBankLoader.CLOSE,
                readcallback = CustomBankLoader.READ,
                seekcallback = CustomBankLoader.SEEK,
            };

            RESULT rESULT = FMOD_Studio_System_LoadBankCustom((IntPtr)typeof(System).GetField("rawPtr", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(this), ref real, flags, out bank2);
            if (rESULT != 0)
            {
                return rESULT;
            }
            bank = new Bank(bank2);
            return rESULT;
        }

        [MonoModIgnore]
        [PatchFMODVersion]
        public extern static RESULT create(out System studiosystem);

        public extern RESULT orig_getLowLevelSystem(out FMOD.System system);
        // https://www.fmod.com/docs/2.03/api/platforms-html5.html#performance-and-memory
        public RESULT getLowLevelSystem(out FMOD.System system)
        {
            RESULT ret = orig_getLowLevelSystem(out system);
            if (ret != RESULT.OK) return ret;

            Guid guid;
            int systemrate;
            SPEAKERMODE speakermode;
            int speakermodechannels;

            ret = system.getDriverInfo(0, new StringBuilder(), 0, out guid, out systemrate, out speakermode, out speakermodechannels);
            if (ret != RESULT.OK) return ret;

            ret = system.setSoftwareFormat(systemrate, speakermode, speakermodechannels);
            if (ret != RESULT.OK) return ret;

            ret = system.setDSPBufferSize(2048, 4);
            if (ret != RESULT.OK) return ret;

            return RESULT.OK;
        }
    }
}

namespace MonoMod
{
    /// <summary>
    /// Patch the FMOD.Studio.System.create method instead of reimplementing it in CelesteWasm.
    /// </summary>
    [MonoModCustomMethodAttribute(nameof(MonoModRules.PatchFMODVersion))]
    class PatchFMODVersionAttribute : Attribute { }

    static partial class MonoModRules
    {
        public static void PatchFMODVersion(ILContext context, CustomAttribute attrib)
        {
            ILCursor cursor = new(context);
            if (!cursor.TryGotoNext(i => i.MatchLdcI4(out var num) && num == 69652))
                throw new Exception("[FMODPatcher] Unable to find FMOD version in FMOD.Studio.System.create");

            context.Instrs[cursor.Index].Operand = (int)0x00020307;

            MonoModRule.Modder.Log("[FMODPatcher] Patched FMOD Version");
        }
    }

    [AttributeUsage(AttributeTargets.Method)]
    sealed class MonoPInvokeCallbackAttribute : Attribute
    {
        public MonoPInvokeCallbackAttribute(Type t) { }
    }

    public class CustomBankLoader
    {
        public static object MODOPEN;
        public static object MODCLOSE;
        public static object MODREAD;
        public static object MODSEEK;

        public static REAL_FILE_OPENCALLBACK OPEN = OpenCallback;
        public static REAL_FILE_CLOSECALLBACK CLOSE = CloseCallback;
        public static REAL_FILE_READCALLBACK READ = ReadCallback;
        public static REAL_FILE_SEEKCALLBACK SEEK = SeekCallback;

        public static void PinvokeFix()
        {
            OPEN = OpenCallback;
            CLOSE = CloseCallback;
            READ = ReadCallback;
            SEEK = SeekCallback;
        }

        [MonoPInvokeCallback(typeof(REAL_FILE_OPENCALLBACK))]
        public static RESULT OpenCallback(StringWrapper name, IntPtr realfilesize, IntPtr realhandle, IntPtr userdata)
        {
            uint filesize = 0;
            IntPtr handle = 0;
            var ret = ((FILE_OPENCALLBACK)MODOPEN)(name, ref filesize, ref handle, userdata);
            Marshal.WriteIntPtr(realhandle, handle);
            Marshal.WriteInt32(realfilesize, (int)filesize);

            return ret;
        }

        [MonoPInvokeCallback(typeof(REAL_FILE_CLOSECALLBACK))]
        public static RESULT CloseCallback(IntPtr handle, IntPtr userdata)
        {
            var ret = ((FILE_CLOSECALLBACK)MODCLOSE)(handle, userdata);
            return ret;
        }

        [MonoPInvokeCallback(typeof(REAL_FILE_READCALLBACK))]
        public static RESULT ReadCallback(IntPtr handle, IntPtr buffer, uint sizebytes, IntPtr realbytesread, IntPtr userdata)
        {
            uint bytesread = 0;
            var ret = ((FILE_READCALLBACK)MODREAD)(handle, buffer, sizebytes, ref bytesread, userdata);
            Marshal.WriteInt32(realbytesread, (int)bytesread);

            return ret;
        }

        [MonoPInvokeCallback(typeof(REAL_FILE_SEEKCALLBACK))]
        public static RESULT SeekCallback(IntPtr handle, uint pos, IntPtr userdata)
        {
            var ret = ((FILE_SEEKCALLBACK)MODSEEK)(handle, pos, userdata);
            return ret;
        }
    }
}
