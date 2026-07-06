using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace NextGenEmby.Core.Emby
{
    public static class SearchRecentTermsPolicy
    {
        public const int DefaultMaxCount = 6;

        public static IReadOnlyList<string> Add(
            IReadOnlyList<string>? currentTerms,
            string? term,
            int maxCount = DefaultMaxCount)
        {
            var normalizedTerm = NormalizeTerm(term);
            if (string.IsNullOrWhiteSpace(normalizedTerm))
            {
                return Limit(Clean(currentTerms), maxCount);
            }

            var terms = new List<string> { normalizedTerm };
            foreach (var current in Clean(currentTerms))
            {
                if (!string.Equals(current, normalizedTerm, StringComparison.OrdinalIgnoreCase))
                {
                    terms.Add(current);
                }
            }

            return Limit(terms, maxCount);
        }

        public static IReadOnlyList<string> FromStoredValue(
            string? storedValue,
            int maxCount = DefaultMaxCount)
        {
            var value = storedValue ?? "";
            if (string.IsNullOrWhiteSpace(value))
            {
                return Array.Empty<string>();
            }

            return Limit(Clean(value.Split(new[] { '\n' }, StringSplitOptions.None)), maxCount);
        }

        public static string ToStoredValue(IReadOnlyList<string>? terms)
        {
            return string.Join("\n", Clean(terms));
        }

        private static IReadOnlyList<string> Clean(IReadOnlyList<string>? terms)
        {
            if (terms == null || terms.Count == 0)
            {
                return Array.Empty<string>();
            }

            var cleaned = new List<string>();
            foreach (var term in terms)
            {
                var normalizedTerm = NormalizeTerm(term);
                if (string.IsNullOrWhiteSpace(normalizedTerm))
                {
                    continue;
                }

                if (cleaned.Exists(
                    value => string.Equals(value, normalizedTerm, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                cleaned.Add(normalizedTerm);
            }

            return cleaned;
        }

        private static IReadOnlyList<string> Limit(IReadOnlyList<string> terms, int maxCount)
        {
            if (terms.Count == 0 || maxCount <= 0)
            {
                return Array.Empty<string>();
            }

            var limited = new List<string>();
            for (var i = 0; i < terms.Count && i < maxCount; i++)
            {
                limited.Add(terms[i]);
            }

            return limited;
        }

        private static string NormalizeTerm(string? term)
        {
            var value = term ?? "";
            if (string.IsNullOrWhiteSpace(value))
            {
                return "";
            }

            return Regex.Replace(value.Trim(), "\\s+", " ");
        }
    }
}
