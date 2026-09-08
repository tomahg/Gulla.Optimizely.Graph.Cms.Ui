using System;
using System.Collections.Generic;
using System.Reflection;
using EPiServer.Shell.Navigation;
using EPiServer.Shell.Navigation.Internal;
using Gulla.Optimizely.Graph.Cms.Ui.Configuration;
using Gulla.Optimizely.Graph.Cms.Ui.Controllers;
using Microsoft.Extensions.Options;

namespace Gulla.Optimizely.Graph.Cms.Ui.Menu
{
    // IEPiProductMenuProvider is a marker with no members. MenuAssembler reads it to stamp
    // MenuItem.IsEPiMenuItem, and the shell's left rail renders only items carrying that flag
    // as first-class entries — everything else is buried inside a collapsed "Add-ons" group.
    // Setting IsEPiMenuItem on the items directly does not work: MenuAssembler overwrites it.
    // The type sits in an .Internal namespace but is public, which is Optimizely's marker for
    // "supported to use, not to depend on" — worth re-checking on a major shell upgrade.
    [MenuProvider]
    public class GraphCmsUiMenuProvider : IMenuProvider, IEPiProductMenuProvider
    {
        /// <summary>
        /// The section Optimizely.ContentGraph.Cms registers for its own left menu (GraphiQL,
        /// Admin, Content Sync). Its menu provider is an internal API, so the path is a string
        /// here and an option for the site, not a reference to their constant.
        /// </summary>
        public const string OptimizelyGraphMenuPath = "/global/ContentGraph";

        // The assembly that registers OptimizelyGraphMenuPath. Resolved by name so this package
        // has no compile-time dependency on it, and so a site without it still gets a menu.
        private const string OptimizelyGraphAssemblyName = "Optimizely.ContentGraph.Cms.NetCore";

        private const string StandaloneRoot = MenuPaths.Global + "/graphcmsui";

        private readonly GraphCmsUiOptions _options;

        public GraphCmsUiMenuProvider(IOptions<GraphCmsUiOptions> options)
        {
            _options = options.Value;
        }

        public IEnumerable<MenuItem> GetMenuItems()
        {
            // The shell assembles one tree from every provider's paths, so children can hang off
            // a section another provider owns. By default ours go under Optimizely Graph's own
            // section, after its items, and the addon adds nothing to the product switcher.
            //
            // Without a parent — by configuration, or because the Optimizely package is not
            // installed and the default parent would never exist — the addon gets a section of
            // its own directly under /global, which puts "Graph" in the product switcher (the
            // "CMS ⌄" dropdown in the top bar) next to CMS itself. SortIndex.Late keeps it
            // after CMS rather than ahead of it.
            //
            // The children are not decoration: the left rail is driven by
            // /epiplatformnavigation?product=<section>, which returns the section's children.
            // With none, the rail renders its three-dot loading indicator forever — an empty
            // list and a list still loading look identical to it.
            //
            // Each child needs a distinct *path*. The rail compares an item's Url to the
            // current location without stripping the query string, so a "?tab=" URL matches
            // nothing — and with no matching item it decides it has no current section and
            // renders collapsed, icons only, no labels. Hence the routes on GraphAdminController.
            var parent = ResolveParentPath();
            var items = new List<MenuItem>();

            if (parent == null)
            {
                parent = StandaloneRoot;
                items.Add(new UrlMenuItem("Graph", StandaloneRoot, "/GraphCmsUi")
                {
                    AuthorizationPolicy = GraphCmsUiAuthorizationPolicy.Default,
                    SortIndex = SortIndex.Late
                });
            }

            // Optimizely's own children sit at 100–500, so 600+ lands after them. Icon names
            // ending in "-thin" select the Font Awesome thin set, which is the weight Optimizely's
            // items use (code-thin, gear-thin, window-restore-thin); both names below are in the
            // shell's bundled subset for CMS 12. An unbundled name renders a blank, not an error.
            items.Add(new UrlMenuItem("Pinned Results", parent + "/pinnedresults", "/GraphCmsUi/" + GraphAdminController.PinnedResultsTab)
            {
                AuthorizationPolicy = GraphCmsUiAuthorizationPolicy.Default,
                SortIndex = 600,
                IconName = "thumbtack-thin"
            });
            items.Add(new UrlMenuItem("Synonyms", parent + "/synonyms", "/GraphCmsUi/" + GraphAdminController.SynonymsTab)
            {
                AuthorizationPolicy = GraphCmsUiAuthorizationPolicy.Default,
                SortIndex = 700,
                IconName = "arrows-repeat-thin"
            });

            return items;
        }

        /// <summary>
        /// The configured parent, or <c>null</c> for a section of our own. The default parent is
        /// only trusted when the package that creates it is present; a custom path is the site's
        /// promise and is used as given.
        /// </summary>
        private string ResolveParentPath()
        {
            var configured = _options.MenuParentPath?.Trim().TrimEnd('/');
            if (string.IsNullOrEmpty(configured))
            {
                return null;
            }

            var isDefault = string.Equals(configured, OptimizelyGraphMenuPath, StringComparison.OrdinalIgnoreCase);
            return isDefault && !OptimizelyGraphIsInstalled() ? null : configured;
        }

        private static bool OptimizelyGraphIsInstalled()
        {
            try
            {
                return Assembly.Load(new AssemblyName(OptimizelyGraphAssemblyName)) != null;
            }
            catch (Exception)
            {
                // FileNotFoundException for a missing assembly; anything else is the same answer
                // for our purposes — we cannot count on the section being there.
                return false;
            }
        }
    }
}
