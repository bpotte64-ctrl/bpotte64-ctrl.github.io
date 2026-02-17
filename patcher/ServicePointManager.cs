using System.Net;
using System.Net.Security;

namespace MonoMod
{
    [MonoModLinkFrom("System.Net.ServicePointManager")]
    public static class Wasm_ServicePointManager
    {
        public static RemoteCertificateValidationCallback ServerCertificateValidationCallback { get { return null; } set { } }
        public static SecurityProtocolType SecurityProtocol { get { return SecurityProtocolType.SystemDefault; } set { } }
    }
}
