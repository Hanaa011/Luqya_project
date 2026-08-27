using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace LostFound.Calls
{
    // A trimmed, self-contained port of Agora's official RTC token v2
    // ("Token007") generator - github.com/AgoraIO/Tools, DynamicKey/
    // AgoraDynamicKey/csharp. Only the RTC join + publish-audio path is
    // kept (voice-only, per this feature's scope); the RTM/Chat/Streaming/
    // FPA/APaaS service types and the token-parsing/verification path from
    // the original are not needed here and are omitted. The one
    // substitution from the original: Agora's reference uses the
    // third-party SharpZipLib for zlib compression - this uses .NET's own
    // built-in System.IO.Compression.ZLibStream (available since .NET 6)
    // instead, which produces standard, fully compatible zlib output, so
    // no new NuGet package is needed. The algorithm itself (HMAC-SHA256
    // signing chain, byte packing order, "007" version prefix) is
    // otherwise unchanged from the original.
    internal static class AgoraTokenBuilder
    {
        private const string Version = "007";
        private const ushort ServiceTypeRtc = 1;
        private const ushort PrivilegeJoinChannel = 1;
        private const ushort PrivilegePublishAudioStream = 2;

        // Builds an RTC token granting only "join channel" and "publish
        // audio" - no video, no data stream - for the given channel/uid,
        // valid until privilegeExpireSeconds from now.
        public static string BuildVoiceToken(
            string appId, string appCertificate, string channelName, uint uid,
            uint tokenExpireSeconds, uint privilegeExpireSeconds)
        {
            if (!IsUuid(appId) || !IsUuid(appCertificate))
            {
                throw new InvalidOperationException("Agora App ID/App Certificate are missing or malformed.");
            }

            var issuedAt = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var salt = (uint)new Random().Next();

            var privileges = new Dictionary<ushort, uint>
            {
                [PrivilegeJoinChannel] = privilegeExpireSeconds,
                [PrivilegePublishAudioStream] = privilegeExpireSeconds,
            };

            var uidText = uid == 0 ? "" : uid.ToString(CultureInfo.InvariantCulture);

            var serviceBuf = new ByteBuf()
                .Put(ServiceTypeRtc)
                .PutIntMap(privileges)
                .Put(GetBytes(channelName))
                .Put(GetBytes(uidText));

            var message = new ByteBuf()
                .Put(GetBytes(appId))
                .Put(issuedAt)
                .Put(tokenExpireSeconds)
                .Put(salt)
                .Put((ushort)1) // exactly one service (RTC)
                .Copy(serviceBuf.AsBytes());

            var signingKey = EncodeHmacSha256(BitConverter.GetBytes(issuedAt), Encoding.UTF8.GetBytes(appCertificate));
            signingKey = EncodeHmacSha256(BitConverter.GetBytes(salt), signingKey);
            var signature = EncodeHmacSha256(signingKey, message.AsBytes());

            var content = new ByteBuf().Put(signature).Copy(message.AsBytes());

            return Version + Convert.ToBase64String(ZlibCompress(content.AsBytes()));
        }

        private static bool IsUuid(string value) =>
            !string.IsNullOrEmpty(value) && value.Length == 32 && value.All(Uri.IsHexDigit);

        private static byte[] GetBytes(string value) => Encoding.UTF8.GetBytes(value ?? "");

        private static byte[] EncodeHmacSha256(byte[] key, byte[] data)
        {
            using var hmac = new HMACSHA256(key);
            return hmac.ComputeHash(data);
        }

        private static byte[] ZlibCompress(byte[] data)
        {
            using var output = new MemoryStream();
            using (var zlib = new ZLibStream(output, CompressionLevel.Optimal, leaveOpen: true))
            {
                zlib.Write(data, 0, data.Length);
            }
            return output.ToArray();
        }

        // Little-endian byte packer matching Token007's wire format exactly
        // (ByteBuf/ByteBuffer in the original reference implementation).
        private sealed class ByteBuf
        {
            private readonly List<byte> _bytes = new();

            public byte[] AsBytes() => _bytes.ToArray();

            public ByteBuf Put(ushort value)
            {
                _bytes.Add((byte)(value & 0xff));
                _bytes.Add((byte)((value >> 8) & 0xff));
                return this;
            }

            public ByteBuf Put(uint value)
            {
                _bytes.Add((byte)(value & 0xff));
                _bytes.Add((byte)((value >> 8) & 0xff));
                _bytes.Add((byte)((value >> 16) & 0xff));
                _bytes.Add((byte)((value >> 24) & 0xff));
                return this;
            }

            public ByteBuf Put(byte[] value)
            {
                Put((ushort)value.Length);
                _bytes.AddRange(value);
                return this;
            }

            public ByteBuf Copy(byte[] value)
            {
                _bytes.AddRange(value);
                return this;
            }

            public ByteBuf PutIntMap(Dictionary<ushort, uint> map)
            {
                Put((ushort)map.Count);
                foreach (var pair in map)
                {
                    Put(pair.Key);
                    Put(pair.Value);
                }
                return this;
            }
        }
    }
}
