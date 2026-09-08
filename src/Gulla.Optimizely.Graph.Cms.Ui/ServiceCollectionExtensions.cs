using System;
using System.Net.Http.Headers;
using System.Text;
using Gulla.Optimizely.Graph.Cms.Ui.Configuration;
using Gulla.Optimizely.Graph.Cms.Ui.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Gulla.Optimizely.Graph.Cms.Ui
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds the Graph.Cms.Ui admin tool with default authorization (CmsAdmins, Administrators or WebAdmins).
        /// Reads Graph credentials from the existing <c>Optimizely:ContentGraph</c> configuration section.
        /// </summary>
        public static IServiceCollection AddGraphCmsUi(this IServiceCollection services)
        {
            return AddGraphCmsUi(services, null, null);
        }

        /// <summary>
        /// Adds the Graph.Cms.Ui admin tool with custom options and default authorization.
        /// </summary>
        public static IServiceCollection AddGraphCmsUi(
            this IServiceCollection services,
            Action<GraphCmsUiOptions> setupAction)
        {
            return AddGraphCmsUi(services, setupAction, null);
        }

        /// <summary>
        /// Adds the Graph.Cms.Ui admin tool with default options and custom authorization.
        /// </summary>
        public static IServiceCollection AddGraphCmsUi(
            this IServiceCollection services,
            Action<AuthorizationOptions> authorizationOptions)
        {
            return AddGraphCmsUi(services, null, authorizationOptions);
        }

        /// <summary>
        /// Adds the Graph.Cms.Ui admin tool with custom options and custom authorization.
        /// </summary>
        public static IServiceCollection AddGraphCmsUi(
            this IServiceCollection services,
            Action<GraphCmsUiOptions> setupAction,
            Action<AuthorizationOptions> authorizationOptions)
        {
            services.AddOptions<GraphCmsUiOptions>().Configure<IConfiguration>((options, configuration) =>
            {
                // Graph credentials come from Optimizely's own section, so they are never
                // entered twice. The UI-only settings (DefaultSlot, DefaultLanguage) have no
                // business in Optimizely's section, so they get a Gulla one — bound second, so
                // it also wins if a value is set in both places. Finally the setup action, so
                // code always overrides configuration.
                configuration.GetSection("Optimizely:ContentGraph").Bind(options);
                configuration.GetSection("Gulla:GraphCmsUi").Bind(options);
                setupAction?.Invoke(options);
            });

            services.AddHttpClient<IGraphPinnedClient, GraphPinnedClient>(ConfigureGraphHttpClient);
            services.AddHttpClient<IGraphSynonymClient, GraphSynonymClient>(ConfigureGraphHttpClient);

            services.AddSingleton<ISiteCollectionResolver, SiteCollectionResolver>();
            services.AddSingleton<SynonymCsvSerializer>();

            if (authorizationOptions != null)
            {
                services.AddAuthorization(authorizationOptions);
            }

            services.AddDefaultGraphCmsUiAuthorizationPolicy();

            return services;
        }

        /// <summary>
        /// Registers the default authorization policy unless one with the same name already exists.
        /// Runs as a <c>PostConfigure</c>, so a policy the site defines itself — through the
        /// <see cref="AuthorizationOptions"/> overload above, or through its own <c>AddAuthorization</c>
        /// call, whether registered before or after this one — always takes precedence.
        /// </summary>
        internal static IServiceCollection AddDefaultGraphCmsUiAuthorizationPolicy(this IServiceCollection services)
        {
            services.AddAuthorization();
            services.PostConfigure<AuthorizationOptions>(options =>
            {
                if (options.GetPolicy(GraphCmsUiAuthorizationPolicy.Default) != null)
                {
                    return;
                }

                options.AddPolicy(GraphCmsUiAuthorizationPolicy.Default, policy =>
                {
                    policy.RequireRole("CmsAdmins", "Administrators", "WebAdmins");
                });
            });

            return services;
        }

        private static void ConfigureGraphHttpClient(IServiceProvider provider, System.Net.Http.HttpClient client)
        {
            var options = provider.GetRequiredService<IOptions<GraphCmsUiOptions>>().Value;

            if (!string.IsNullOrWhiteSpace(options.GatewayAddress))
            {
                client.BaseAddress = new Uri(options.GatewayAddress.TrimEnd('/') + "/");
            }

            if (!string.IsNullOrWhiteSpace(options.AppKey) && !string.IsNullOrWhiteSpace(options.Secret))
            {
                var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{options.AppKey}:{options.Secret}"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
            }
        }
    }
}
