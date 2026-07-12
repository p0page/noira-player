using System;

namespace NoiraPlayer.Core.Emby
{
    public static class EmbyWebPathPolicy
    {
        public static bool IsAllowed(EmbySession session, string path)
        {
            if (session == null ||
                string.IsNullOrEmpty(session.UserId) ||
                string.IsNullOrWhiteSpace(path) ||
                path[0] == '/' ||
                path.IndexOf('\\') >= 0 ||
                path.IndexOf('#') >= 0)
            {
                return false;
            }

            var queryIndex = path.IndexOf('?');
            var pathWithoutQuery = queryIndex < 0 ? path : path.Substring(0, queryIndex);
            var query = queryIndex < 0 ? "" : path.Substring(queryIndex + 1);
            var rawSegments = pathWithoutQuery.Split('/');
            var segments = new string[rawSegments.Length];
            for (var index = 0; index < rawSegments.Length; index++)
            {
                if (!TryDecodePathSegment(rawSegments[index], out segments[index]))
                {
                    return false;
                }
            }

            if (!TryReadUserId(query, out var queryUserId, out var queryUserIdCount))
            {
                return false;
            }

            if (segments.Length == 2 &&
                string.Equals(segments[0], "Shows", StringComparison.Ordinal) &&
                string.Equals(segments[1], "NextUp", StringComparison.Ordinal))
            {
                return queryUserIdCount == 1 &&
                    string.Equals(queryUserId, session.UserId, StringComparison.Ordinal);
            }

            if (segments.Length < 3 || segments.Length > 4 ||
                !string.Equals(segments[0], "Users", StringComparison.Ordinal) ||
                !string.Equals(segments[1], session.UserId, StringComparison.Ordinal) ||
                queryUserIdCount != 0)
            {
                return false;
            }

            if (segments.Length == 3)
            {
                return string.Equals(segments[2], "Views", StringComparison.Ordinal) ||
                    string.Equals(segments[2], "Items", StringComparison.Ordinal);
            }

            return string.Equals(segments[2], "Items", StringComparison.Ordinal);
        }

        private static bool TryDecodePathSegment(string rawSegment, out string segment)
        {
            segment = "";
            if (rawSegment.Length == 0 || !TryDecode(rawSegment, formEncoded: false, out segment))
            {
                return false;
            }

            return segment.Length > 0 &&
                segment.IndexOf('/') < 0 &&
                segment.IndexOf('\\') < 0 &&
                segment.IndexOf('%') < 0 &&
                !string.Equals(segment, ".", StringComparison.Ordinal) &&
                !string.Equals(segment, "..", StringComparison.Ordinal) &&
                !ContainsControlCharacter(segment);
        }

        private static bool TryReadUserId(string query, out string userId, out int userIdCount)
        {
            userId = "";
            userIdCount = 0;
            foreach (var parameter in query.Split('&'))
            {
                var separatorIndex = parameter.IndexOf('=');
                var rawKey = separatorIndex < 0 ? parameter : parameter.Substring(0, separatorIndex);
                var rawValue = separatorIndex < 0 ? "" : parameter.Substring(separatorIndex + 1);
                if (!TryDecode(rawKey, formEncoded: true, out var key) ||
                    !TryDecode(rawValue, formEncoded: true, out var value) ||
                    key.IndexOf('\\') >= 0 ||
                    key.IndexOf('%') >= 0 ||
                    value.IndexOf('\\') >= 0 ||
                    ContainsControlCharacter(key) ||
                    ContainsControlCharacter(value))
                {
                    return false;
                }

                if (string.Equals(key, "UserId", StringComparison.OrdinalIgnoreCase))
                {
                    userIdCount++;
                    userId = value;
                }
            }

            return true;
        }

        private static bool TryDecode(string value, bool formEncoded, out string decoded)
        {
            decoded = "";
            for (var index = 0; index < value.Length; index++)
            {
                if (value[index] != '%')
                {
                    continue;
                }

                if (index + 2 >= value.Length ||
                    !IsHexDigit(value[index + 1]) ||
                    !IsHexDigit(value[index + 2]))
                {
                    return false;
                }

                index += 2;
            }

            try
            {
                decoded = Uri.UnescapeDataString(formEncoded ? value.Replace('+', ' ') : value);
                return true;
            }
            catch (UriFormatException)
            {
                return false;
            }
        }

        private static bool IsHexDigit(char value)
        {
            return (value >= '0' && value <= '9') ||
                (value >= 'A' && value <= 'F') ||
                (value >= 'a' && value <= 'f');
        }

        private static bool ContainsControlCharacter(string value)
        {
            foreach (var character in value)
            {
                if (char.IsControl(character))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
