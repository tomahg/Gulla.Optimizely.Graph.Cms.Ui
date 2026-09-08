using System;

namespace Gulla.Optimizely.Graph.Cms.Ui.Services
{
    /// <summary>
    /// The synonym list Graph stores when a request carries no <c>language_routing</c> at all.
    /// Optimizely's own Search Management UI shows these rules with the language "ANY", and any
    /// routing value Graph does not recognise reads and writes this same list — there is no
    /// separate wildcard document.
    /// <para>
    /// The name oversells it: measured against a live instance (2026-09-08), rules in this list
    /// fire only for queries with no <c>locale</c> argument or <c>locale: ALL</c>. A site search
    /// that passes the visitor's language never sees them. The UI shows the list so rules
    /// created elsewhere can be found, edited and deleted, and says plainly what it applies to.
    /// </para>
    /// </summary>
    public static class SynonymLanguage
    {
        /// <summary>
        /// The id the language picker and the <c>lang</c> query parameter use for this list.
        /// Deliberately the word Optimizely uses, so <c>?lang=any</c> and the exported
        /// <c>synonyms-any-slot-one.csv</c> read the same as their UI. Not a valid culture
        /// name, so it can never collide with an enabled CMS language.
        /// </summary>
        public const string NoLocaleId = "any";

        public static bool IsNoLocale(string languageId)
        {
            return string.Equals(languageId?.Trim(), NoLocaleId, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// What to hand the Graph client: the CMS language id as-is, or <c>null</c> for the
        /// no-locale list, which the client turns into a request without <c>language_routing</c>.
        /// </summary>
        public static string RouteFor(string languageId)
        {
            return IsNoLocale(languageId) ? null : languageId;
        }
    }
}
