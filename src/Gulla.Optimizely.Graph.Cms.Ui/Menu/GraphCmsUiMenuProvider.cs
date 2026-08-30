using System.Collections.Generic;
using EPiServer.Shell.Navigation;
using EPiServer.Shell.Navigation.Internal;
using Gulla.Optimizely.Graph.Cms.Ui.Configuration;
using Gulla.Optimizely.Graph.Cms.Ui.Controllers;

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
        private const string Root = MenuPaths.Global + "/graphcmsui";

        public IEnumerable<MenuItem> GetMenuItems()
        {
            // A path directly under /global puts the addon in the product switcher — the
            // "CMS ⌄" dropdown in the top bar — next to CMS itself, which is where Search &
            // Navigation used to sit.
            //
            // SortIndex.Late keeps it after CMS rather than ahead of it.
            //
            // The children are not decoration: the left rail is driven by
            // /epiplatformnavigation?product=global_graphcmsui, which returns this item's
            // children. With none, the rail renders its three-dot loading indicator forever —
            // an empty list and a list still loading look identical to it. They also change how
            // the product switcher behaves: a product with children swaps the rail instead of
            // navigating, so the rail entries are what actually opens the page.
            //
            // Each child needs a distinct *path*. The rail compares an item's Url to the
            // current location without stripping the query string, so a "?tab=" URL matches
            // nothing — and with no matching item it decides it has no current section and
            // renders collapsed, icons only, no labels. Hence the routes on GraphAdminController.
            return
            [
                new UrlMenuItem("Graph", Root, "/GraphCmsUi")
                {
                    AuthorizationPolicy = GraphCmsUiAuthorizationPolicy.Default,
                    SortIndex = SortIndex.Late
                },
                new UrlMenuItem("Pinned Results", Root + "/pinnedresults", "/GraphCmsUi/" + GraphAdminController.PinnedResultsTab)
                {
                    AuthorizationPolicy = GraphCmsUiAuthorizationPolicy.Default,
                    SortIndex = 10,
                    IconName = "thumbtack"
                },
                new UrlMenuItem("Synonyms", Root + "/synonyms", "/GraphCmsUi/" + GraphAdminController.SynonymsTab)
                {
                    AuthorizationPolicy = GraphCmsUiAuthorizationPolicy.Default,
                    SortIndex = 20,
                    IconName = "arrow-right-arrow-left"
                }
            ];
        }
    }
}
