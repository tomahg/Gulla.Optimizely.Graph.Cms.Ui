# Gulla.Optimizely.Graph.Cms.Ui

A familiar Search & Navigation–style admin UI for **Optimizely CMS 13** that lets editors manage **Pinned Results** (formerly *Best Bets*) and **Synonyms** through Optimizely Graph — without leaving the CMS.

When Optimizely retired Search & Navigation in favour of Optimizely Graph, the editor UI for Best Bets and Synonyms went with it. This package brings that UI back, talking to Graph's REST APIs under the hood.

## Installation

```
dotnet add package Gulla.Optimizely.Graph.Cms.Ui
```

In `Program.cs`:

```csharp
services.AddGraphCmsUi();
```

The package reuses Optimizely Graph's existing configuration. Make sure you already have a `Optimizely:ContentGraph` section in `appsettings.json`:

```json
{
  "Optimizely": {
    "ContentGraph": {
      "GatewayAddress": "https://cg.optimizely.com",
      "AppKey": "your-app-key",
      "Secret": "your-app-secret",
      "SingleKey": "your-single-key"
    }
  }
}
```

## Usage

After install, log in to the CMS as an administrator and click the new **Search** menu item. You'll find two tabs:

- **Pinned Results** — pin specific CMS content to the top of the search results for chosen phrases. Per site, per language. Previously called Best Bet in Search & Navigation.
- **Synonyms** — define one-way (`a => b`) and bidirectional (`a, b`) term equivalences. Per site, per language. Import/export the CMS 12 CSV format directly.

## Authorization

Access is controlled by the `GraphCmsUiAuthorizationPolicy.Default` policy. By default it requires the
`CmsAdmins`, `Administrators`, or `WebAdmins` role. Override it by passing an `AuthorizationOptions`
action to `AddGraphCmsUi()`:

```csharp
services.AddGraphCmsUi(auth =>
{
    auth.AddPolicy(GraphCmsUiAuthorizationPolicy.Default, policy =>
    {
        policy.RequireRole("SearchAdmins");
    });
});
```

The default policy is registered as a `PostConfigure`, so a policy you define yourself always wins —
and the policy always exists even if `AddGraphCmsUi()` is never called, so the site never
fails to start with `No policy found: GraphCmsUiAdmin`.

Using Opti ID you will probably also need `policy.AddAuthenticationSchemes(OptimizelyIdentityDefaults.SchemeName);`
inside the policy.

## Limitations (inherited from Optimizely Graph)

- Pinned results target **internal CMS content only** — no external links.
- Pinned results display the content's own title/description — no overrides.
- A maximum of **5 pinned results** apply per query (storage isn't capped).
- Synonyms are **per-language**; there is no shared "all languages" slot.

## License

MIT — see `<PackageLicenseExpression>` in the project file.
