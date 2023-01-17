using Google.Protobuf;
using Meshtastic.Data;
using Meshtastic.Protobufs;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
using System.Net.Sockets;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Meshtastic.Connections;

public class TcpConnection : DeviceConnection, IDisposable
{
    private readonly TcpClient client;
    private NetworkStream? networkStream;
    private const int DEFAULT_BUFFER_SIZE = 32;

    public TcpConnection(ILogger logger, string host, int port = Resources.DEFAULT_TCP_PORT) : base(logger)
    {
        client = new TcpClient(host, port)
        {
            ReceiveBufferSize = DEFAULT_BUFFER_SIZE,
            NoDelay = true
        };
    }

    public override async Task WriteToRadio(ToRadio data, Func<FromRadio, DeviceStateContainer, Task<bool>> isComplete)
    {
        var toRadio = PacketFraming.CreatePacket(data.ToByteArray());
        networkStream = client.GetStream();
        await networkStream.WriteAsync(toRadio);
        Logger.LogDebug($"Sent: {data}");
        await ReadFromRadio(isComplete);
    }

    public override async Task WriteToRadio(ToRadio data)
    {
        var toRadio = PacketFraming.CreatePacket(data.ToByteArray());
        await networkStream!.WriteAsync(toRadio);
        Logger.LogDebug($"Sent: {data}");
    }

    public override async Task ReadFromRadio(Func<FromRadio, DeviceStateContainer, Task<bool>> isComplete, int readTimeoutMs = Resources.DEFAULT_READ_TIMEOUT)
    {
        if (networkStream == null)
            throw new ApplicationException("Could not establish network stream");

        var buffer = new byte[DEFAULT_BUFFER_SIZE];
        while (networkStream.CanRead)
        {
            await networkStream.ReadAsync(buffer);
            foreach (var item in buffer)
            {
                if (await ParsePackets(item, isComplete))
                    return;
            }
        }
    }

    public void Dispose()
    {
        client?.Dispose();
        GC.SuppressFinalize(this);
    }
}