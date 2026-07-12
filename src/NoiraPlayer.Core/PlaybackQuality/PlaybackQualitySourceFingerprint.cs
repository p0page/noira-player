using System;
using System.Security.Cryptography;
using System.Text;
using System.Linq;
using System.Buffers;
using System.Text.Json;

namespace NoiraPlayer.Core.PlaybackQuality
{
    public static class PlaybackQualitySourceFingerprint
    {
        public const string OpenedMediaSignatureKind = "observed-media-signature-v1";

        public static string Compute(string locator)
        {
            var bytes = Encoding.UTF8.GetBytes(locator ?? "");
            return "sha256:" + Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        }

        public static string ComputeOpenedSource(string locator)
        {
            if (!Uri.TryCreate(locator, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                return Compute(locator);
            }

            var stableQuery = uri.Query
                .TrimStart('?')
                .Split('&', StringSplitOptions.RemoveEmptyEntries)
                .Where(part => !string.Equals(
                    Uri.UnescapeDataString(part.Split('=', 2)[0]),
                    "PlaySessionId",
                    StringComparison.OrdinalIgnoreCase))
                .OrderBy(part => part, StringComparer.Ordinal)
                .ToArray();
            var identity = uri.GetLeftPart(UriPartial.Path);
            if (stableQuery.Length > 0)
            {
                identity += "?" + string.Join("&", stableQuery);
            }

            return Compute(identity);
        }

        public static string ComputeOpenedMediaSignature(PlaybackQualityReport report)
        {
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            var buffer = new ArrayBufferWriter<byte>();
            using (var writer = new Utf8JsonWriter(buffer))
            {
                var source = report.Source ?? new PlaybackQualitySource();
                writer.WriteStartObject();
                writer.WriteString("version", OpenedMediaSignatureKind);
                writer.WriteString("container", source.Container ?? "");
                writer.WriteNumber("durationTicks", source.DurationTicks);
                WriteNullableInt64(writer, "containerStartTimeTicks", source.ContainerStartTimeTicks);
                WriteNullableInt64(writer, "videoStreamStartTimeTicks", source.VideoStreamStartTimeTicks);
                writer.WriteString("codec", source.Codec ?? "");
                writer.WriteNumber("width", source.Width);
                writer.WriteNumber("height", source.Height);
                writer.WriteNumber("frameRate", source.FrameRate);
                writer.WriteString("hdrKind", source.HdrKind ?? "");
                writer.WriteString("videoRange", source.VideoRange ?? "");
                writer.WriteString("colorPrimaries", source.ColorPrimaries ?? "");
                writer.WriteString("colorTransfer", source.ColorTransfer ?? "");
                writer.WriteString("colorSpace", source.ColorSpace ?? "");
                WriteTracks(writer, "video", report.Tracks?.Video);
                WriteTracks(writer, "audio", report.Tracks?.Audio);
                WriteTracks(writer, "subtitles", report.Tracks?.Subtitles);
                writer.WriteEndObject();
            }

            return "sha256:" + Convert.ToHexString(SHA256.HashData(buffer.WrittenSpan)).ToLowerInvariant();
        }

        private static void WriteNullableInt64(Utf8JsonWriter writer, string name, long? value)
        {
            if (value.HasValue)
            {
                writer.WriteNumber(name, value.Value);
            }
            else
            {
                writer.WriteNull(name);
            }
        }

        private static void WriteTracks(
            Utf8JsonWriter writer,
            string name,
            System.Collections.Generic.IEnumerable<PlaybackQualityTrack>? tracks)
        {
            writer.WriteStartArray(name);
            foreach (var track in (tracks ?? Array.Empty<PlaybackQualityTrack>()).OrderBy(value => value.Index))
            {
                writer.WriteStartObject();
                writer.WriteNumber("index", track.Index);
                writer.WriteString("kind", track.Kind ?? "");
                writer.WriteString("codec", track.Codec ?? "");
                writer.WriteString("language", track.Language ?? "");
                writer.WriteString("channelLayout", track.ChannelLayout ?? "");
                writer.WriteNumber("channels", track.Channels);
                writer.WriteBoolean("isExternal", track.IsExternal);
                if (track.IsDefault.HasValue) writer.WriteBoolean("isDefault", track.IsDefault.Value); else writer.WriteNull("isDefault");
                if (track.IsForced.HasValue) writer.WriteBoolean("isForced", track.IsForced.Value); else writer.WriteNull("isForced");
                writer.WriteNumber("realFrameRate", track.RealFrameRate);
                writer.WriteNumber("averageFrameRate", track.AverageFrameRate);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }
    }
}
