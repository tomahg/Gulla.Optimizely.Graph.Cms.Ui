using System;
using System.Text;

namespace Gulla.Optimizely.Graph.Cms.Ui.Services
{
    /// <summary>
    /// Builds and reads the collection keys this addon owns: <c>{name}-{site}</c>.
    /// <para>
    /// The key is not an internal detail — it is the handle a developer passes to
    /// <c>pinned: { collections: [...] }</c> in GraphQL, so it has to be readable, stable and
    /// free of characters that would be awkward to type into a query.
    /// </para>
    /// <para>
    /// The site suffix is what keeps one site's pinned results out of another's: a query that
    /// omits <c>collections</c> evaluates every active collection on the Graph instance.
    /// </para>
    /// </summary>
    internal static class CollectionKeys
    {
        public const string DefaultName = "default";

        public static string Build(string name, string siteKey)
        {
            var slug = Slug(name);
            if (slug.Length == 0)
            {
                slug = DefaultName;
            }

            return $"{slug}-{Slug(siteKey)}";
        }

        /// <summary>
        /// True when the key belongs to the given site. Collections are listed per site, and
        /// Graph has no metadata field to store the owner in, so the suffix is the only marker.
        /// </summary>
        public static bool BelongsToSite(string key, string siteKey)
        {
            return NameFrom(key, siteKey) != null;
        }

        /// <summary>
        /// The display name for a key — the key with its <c>-{site}</c> suffix removed — or
        /// <c>null</c> when the key belongs to another site (or to something that isn't ours).
        /// </summary>
        public static string NameFrom(string key, string siteKey)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

            var suffix = "-" + Slug(siteKey);
            if (key.Length <= suffix.Length || !key.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return key.Substring(0, key.Length - suffix.Length);
        }

        public static bool IsDefault(string key, string siteKey)
        {
            return string.Equals(NameFrom(key, siteKey), DefaultName, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Lowercase, alphanumerics kept, every other run of characters collapsed to a single
        /// dash. Site names in Optimizely routinely contain spaces, and an unslugged space would
        /// end up in a key someone has to paste into a GraphQL query.
        /// </summary>
        public static string Slug(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(value.Length);
            var pendingDash = false;

            foreach (var c in value.Trim().ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(c))
                {
                    if (pendingDash && builder.Length > 0)
                    {
                        builder.Append('-');
                    }
                    pendingDash = false;
                    builder.Append(c);
                }
                else
                {
                    pendingDash = true;
                }
            }

            return builder.ToString();
        }
    }
}
