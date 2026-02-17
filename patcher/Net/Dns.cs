using System.Net;
using System.Linq;
using DnsOverHttps;

namespace MonoMod
{
    [MonoModLinkFrom("System.Net.Dns")]
    public class Wasm_Dns
    {
        private static DnsOverHttpsClient DnsClient;

        public static IPAddress[] GetHostAddresses(string hostNameOrAddress)
        {
            if (hostNameOrAddress == "localhost") return [new IPAddress([127, 0, 0, 1])];

            if (DnsClient == null) DnsClient = new();

            Answer[] answers = DnsClient.ResolveAll(hostNameOrAddress, ResourceRecordType.A).Result;

            return answers.Select(x => IPAddress.Parse(x.Data)).ToArray();
        }
    }
}
