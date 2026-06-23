using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace VoidRpLauncher.CoreHost.Services;

public sealed record ServerStatusResult(bool Online, int PlayersOnline, int PlayersMax, string Version);

public sealed class MinecraftServerPingService
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(4);

    public async Task<ServerStatusResult> PingAsync(string host, int port, CancellationToken cancellationToken = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(Timeout);

            using var tcp = new TcpClient();
            await tcp.ConnectAsync(host, port, cts.Token);

            using var stream = tcp.GetStream();

            // Handshake packet
            var addressBytes = Encoding.UTF8.GetBytes(host);
            var handshake = new MemoryStream();
            WriteVarInt(handshake, 0x00);           // packet id
            WriteVarInt(handshake, 47);             // protocol version (doesn't matter for status)
            WriteVarInt(handshake, addressBytes.Length);
            handshake.Write(addressBytes);
            handshake.Write([(byte)(port >> 8), (byte)(port & 0xFF)]); // port big-endian
            WriteVarInt(handshake, 1);              // next state: status

            await SendPacket(stream, handshake.ToArray(), cts.Token);

            // Status request packet (empty body, id=0x00)
            await SendPacket(stream, [0x00], cts.Token);

            // Read status response
            var packetLength = await ReadVarInt(stream, cts.Token);
            var packetId = await ReadVarInt(stream, cts.Token);
            if (packetId != 0x00)
                return new ServerStatusResult(false, 0, 0, "");

            var jsonLength = await ReadVarInt(stream, cts.Token);
            var jsonBytes = new byte[jsonLength];
            var totalRead = 0;
            while (totalRead < jsonLength)
            {
                var read = await stream.ReadAsync(jsonBytes.AsMemory(totalRead, jsonLength - totalRead), cts.Token);
                if (read == 0) break;
                totalRead += read;
            }

            var json = Encoding.UTF8.GetString(jsonBytes, 0, totalRead);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var online = root.TryGetProperty("players", out var players)
                && players.TryGetProperty("online", out var onlineProp)
                ? onlineProp.GetInt32() : 0;
            var max = players.ValueKind != JsonValueKind.Undefined
                && players.TryGetProperty("max", out var maxProp)
                ? maxProp.GetInt32() : 0;
            var version = root.TryGetProperty("version", out var ver)
                && ver.TryGetProperty("name", out var verName)
                ? verName.GetString() ?? "" : "";

            return new ServerStatusResult(true, online, max, version);
        }
        catch
        {
            return new ServerStatusResult(false, 0, 0, "");
        }
    }

    private static async Task SendPacket(Stream stream, byte[] data, CancellationToken ct)
    {
        var lenBuf = new MemoryStream();
        WriteVarInt(lenBuf, data.Length);
        await stream.WriteAsync(lenBuf.ToArray(), ct);
        await stream.WriteAsync(data, ct);
    }

    private static void WriteVarInt(Stream stream, int value)
    {
        var unsigned = (uint)value;
        do
        {
            var b = (byte)(unsigned & 0x7F);
            unsigned >>= 7;
            if (unsigned != 0) b |= 0x80;
            stream.WriteByte(b);
        } while (unsigned != 0);
    }

    private static async Task<int> ReadVarInt(Stream stream, CancellationToken ct)
    {
        var result = 0;
        var shift = 0;
        var buf = new byte[1];
        while (shift < 35)
        {
            var read = await stream.ReadAsync(buf.AsMemory(0, 1), ct);
            if (read == 0) throw new EndOfStreamException();
            result |= (buf[0] & 0x7F) << shift;
            if ((buf[0] & 0x80) == 0) return result;
            shift += 7;
        }
        throw new InvalidDataException("VarInt too large");
    }
}
