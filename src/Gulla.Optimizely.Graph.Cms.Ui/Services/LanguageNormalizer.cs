namespace Gulla.Optimizely.Graph.Cms.Ui.Services
{
    /// <summary>
    /// Optimizely Graph expects two-letter ISO 639 codes (e.g. "en", "nb", "sv").
    /// Optimizely CMS hands out full culture codes (e.g. "en-US", "nb-NO", "sv-SE").
    /// </summary>
    internal static class LanguageNormalizer
    {
        public static string ToIsoCode(string language)
        {
            if (string.IsNullOrWhiteSpace(language))
            {
                return language;
            }

            var trimmed = language.Trim();
            var dash = trimmed.IndexOf('-');
            return (dash > 0 ? trimmed.Substring(0, dash) : trimmed).ToLowerInvariant();
        }
    }
}
