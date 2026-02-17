using System.Net.WebSockets;
using System.Threading;
using System;
using System.Linq;
using System.IO;
using System.Net;
using System.Net.Sockets;
using MonoMod;

namespace Celeste.Wasm
{
    [MonoModIgnore]
    public class Wasm_NetworkStream : Stream
    {
        private ClientWebSocket Socket;

        public override bool CanRead => true;
        public override bool CanWrite => true;
        public override bool CanSeek => false;
        public override long Length => throw new NotImplementedException();
        public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public Wasm_NetworkStream(Wasm_Socket socket) : this(socket, false) { }

        public Wasm_NetworkStream(Wasm_Socket socket, bool ownsSocket)
        {
            Socket = socket.Socket;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            try
            {
                var ret = Socket.ReceiveAsync(new ArraySegment<byte>(buffer, offset, count), CancellationToken.None).Result;
                return ret.Count;
            }
            catch (Exception err)
            {
                Console.Error.WriteLine($"err {err}");
                throw;
            }
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            try
            {
                Socket.SendAsync(buffer[offset..(offset + count)], WebSocketMessageType.Binary, WebSocketMessageFlags.EndOfMessage, CancellationToken.None).AsTask().Wait();
            }
            catch (Exception err)
            {
                Console.Error.WriteLine($"err {err}");
                throw;
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Flush()
        {
            // do nothing
        }
    }

    [MonoModIgnore]
    public class Wasm_Socket : IDisposable
    {
        public static bool OSSupportsIPv4 { get { return true; } }
        public static bool OSSupportsIPv6 { get { return false; } }
        public static bool OSSupportsUnixDomainSockets { get { return false; } }

        public AddressFamily AddressFamily { get; private set; }
        public SocketType SocketType { get; private set; }
        public ProtocolType ProtocolType { get; private set; }

        public int ReceiveTimeout { get; set; }
        public int SendTimeout { get; set; }
        public bool Connected { get; private set; }

        public EndPoint RemoteEndPoint { get; private set; }

        private static ProtocolType[] SupportedProtocolTypes = [ProtocolType.Tcp, ProtocolType.Udp];
        private static SocketType[] SupportedSocketTypes = [SocketType.Stream, SocketType.Dgram];

        internal ClientWebSocket Socket;

        public Wasm_Socket(AddressFamily addressFamily, SocketType socketType, ProtocolType protocolType)
        {
            if (!SupportedProtocolTypes.Contains(protocolType)) throw new NotSupportedException($"{protocolType} is not supported by WasmSocket");
            if (!SupportedSocketTypes.Contains(socketType)) throw new NotSupportedException($"{socketType} is not supported by WasmSocket");
            if (addressFamily != AddressFamily.InterNetwork) throw new NotSupportedException($"{addressFamily} is not supported by WasmSocket");

            AddressFamily = addressFamily;
            SocketType = socketType;
            ProtocolType = protocolType;
            Socket = new ClientWebSocket();
        }

        public void Connect(EndPoint endpoint)
        {
            if (endpoint is IPEndPoint ip)
            {
                Connect(ip.Address, ip.Port);
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        public void Connect(IPAddress address, int port)
        {
            UriBuilder builder = new();
            builder.Scheme = "ws";
            builder.Host = $"__celestewasm_wisp_proxy_ws__{ProtocolType.ToString().ToLowerInvariant()}";
            builder.Path = $"{address.ToString()}:{port}";

            Socket.ConnectAsync(builder.Uri, CancellationToken.None).Wait();
            Connected = true;
            RemoteEndPoint = new IPEndPoint(address, port);
        }

        public int Receive(byte[] buf)
        {
            try
            {
                var ret = Socket.ReceiveAsync(new ArraySegment<byte>(buf), CancellationToken.None).Result;
                return ret.Count;
            }
            catch (Exception err)
            {
                Console.Error.WriteLine($"err {err}");
                throw;
            }
        }

        public int Send(byte[] buf)
        {
            return Send(buf, SocketFlags.None);
        }

        public int Send(byte[] buf, SocketFlags flags)
        {
            return Send(buf, buf.Length, flags);
        }

        public int Send(byte[] buf, int size, SocketFlags flags)
        {
            try
            {
                Socket.SendAsync(buf[..size], WebSocketMessageType.Binary, WebSocketMessageFlags.EndOfMessage, CancellationToken.None).AsTask().Wait();
                return size;
            }
            catch (Exception err)
            {
                Console.Error.WriteLine($"err {err}");
                throw;
            }
        }

        public void SetSocketOption(SocketOptionLevel level, SocketOptionName name, int value)
        {
            // do nothing
        }

        public void Shutdown(SocketShutdown shutdown)
        {
            Close();
        }

        public void Close()
        {
            Socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None).Wait();
        }

        public void Dispose()
        {
            if (Socket != null)
                Socket.Dispose();
            Socket = null;
        }
    }
}
