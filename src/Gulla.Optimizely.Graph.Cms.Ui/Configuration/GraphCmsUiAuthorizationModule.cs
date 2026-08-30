using EPiServer.Framework;
using EPiServer.Framework.Initialization;
using EPiServer.ServiceLocation;
using Gulla.Optimizely.Graph.Cms.Ui;

namespace Gulla.Optimizely.Graph.Cms.Ui.Configuration
{
    /// <summary>
    /// Guarantees that <see cref="GraphCmsUiAuthorizationPolicy.Default"/> always resolves, even in a
    /// site that never calls <c>AddGraphCmsUi()</c> or that registers it after <c>AddCms()</c>.
    /// Anything referring to the policy by name — the menu item, the controllers, the shell — throws
    /// <c>InvalidOperationException: No policy found: GraphCmsUiAdmin.</c> when it is missing, and that
    /// surfaces during startup rather than on the admin page, which makes it hard to diagnose.
    /// The fallback runs as a <c>PostConfigure</c>, so a policy the site defines itself always wins.
    /// </summary>
    [InitializableModule]
    public class GraphCmsUiAuthorizationModule : IConfigurableModule
    {
        public void ConfigureContainer(ServiceConfigurationContext context)
        {
            context.Services.AddDefaultGraphCmsUiAuthorizationPolicy();
        }

        public void Initialize(InitializationEngine context)
        {
        }

        public void Uninitialize(InitializationEngine context)
        {
        }
    }
}
