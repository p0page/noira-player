namespace NextGenEmby.Core.Media
{
    public static class PosterFallbackInitials
    {
        public static string Create(string? value)
        {
            var trimmed = value == null ? "" : value.Trim();
            if (trimmed.Length == 0)
            {
                return "?";
            }

            foreach (var character in trimmed)
            {
                if (char.IsLetterOrDigit(character))
                {
                    return character.ToString().ToUpperInvariant();
                }
            }

            return "?";
        }
    }
}
