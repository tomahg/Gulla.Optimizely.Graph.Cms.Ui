# Gulla.Optimizely.Graph.Cms.Ui for CMS 13

A familiar Search & Navigation–style admin UI for **Optimizely CMS 13** that lets editors manage **Pinned Results** (formerly *Best Bets*) and **Synonyms** through Optimizely Graph, without leaving the CMS.

When Optimizely retired Search & Navigation in favour of Optimizely Graph, the editor UI for Best Bets and Synonyms went with it. This package brings that UI back, talking to Graph's REST APIs under the hood.

## Requirements

- .NET 10
- Optimizely CMS 13.1.1 or later (`EPiServer.CMS.Core` / `EPiServer.CMS.UI.Core`)
- An Optimizely Graph instance the site is already configured against

## Installation

```
dotnet add package Gulla.Optimizely.Graph.Cms.Ui
```

In `Program.cs`:

```csharp
builder.Services.AddGraphCmsUi();
```

The package reuses Optimizely Graph's existing configuration. Make sure you already have an `Optimizely:ContentGraph` section in `appsettings.json`:

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

`AppKey` and `Secret` are required — the Graph REST endpoints this addon calls are HTTP Basic authenticated and are not reachable with `SingleKey` alone.

## Usage

After install, log in to the CMS as an administrator and go to **Settings → Graph Optimization** (in the left sidebar, under *Data & Sync Management*, alongside Scheduled Jobs and GraphiQL). You'll find two tabs:

- **Pinned Results** — pin specific CMS content to the top of the search results for chosen phrases. Scoped per site and per language. Previously called Best Bets in Search & Navigation.
- **Synonyms** — define one-way (`a => b`) and bidirectional (`a <=> b`) term equivalences. Scoped per language and per slot. Import/export the CMS 12 CSV format directly.

### Scoping

The two features are scoped differently, because Graph scopes them differently:

| | Per site | Per language | Per slot |
|---|---|---|---|
| Pinned Results | ✅ (query must opt in — see below) | ✅ | — |
| Synonyms | ❌ | ✅ | ✅ |

Pinned results are stored in a **per-site Graph collection**, keyed `default-<site>`. The collection is created on first use.

> **Per-site scoping only takes effect if your query asks for it.** Graph evaluates **all active
> collections** when a query omits the `collections` argument. On a multi-site solution sharing one
> Graph instance, that means site A's pinned results fire on site B's searches. Pass the collection
> explicitly to keep them apart.

`collections` takes the collection's **id** (a GUID), not its key. To find the id for a site, call Graph's REST API and match on the key this addon generates:

```
GET {GatewayAddress}/api/pinned/collections
Authorization: Basic base64(AppKey:Secret)
```

```graphql
query Search($searchText: String, $collectionId: String) {
  ArticlePage(
    where: { _fulltext: { match: $searchText } }
    pinned: { phrase: $searchText, collections: $collectionId }
  ) {
    items { Name }
  }
}
```

If you run a **single site** on the Graph instance, you can omit `collections` entirely — there is only one active collection and the default behaviour is what you want.

Synonyms have **no per-site dimension in Graph** — a synonym list belongs to a language and a slot, and applies to every site sharing that Graph instance. The site picker in the toolbar is therefore hidden on the Synonyms tab.

### Synonym slots

Graph exposes **two synonym slots per language**, named `ONE` and `TWO`. Which one applies is chosen by the query, not by the data, so your GraphQL must say which slot it wants:

```graphql
{
  ArticlePage(where: { _fulltext: { match: "fruit" } }, synonyms: [ONE]) {
    items { Name }
  }
}
```

Pick the matching slot in the UI before adding or importing synonyms. Changes can take a few minutes to take effect.

## Configuration

`AddGraphCmsUi()` binds `GraphCmsUiOptions` from `Optimizely:ContentGraph`. One extra setting is not part of that section and can only be set in code:

```csharp
builder.Services.AddGraphCmsUi(options =>
{
    options.DefaultSlot = "two"; // default: "one"
});
```

| Option | Default | Purpose |
|---|---|---|
| `DefaultSlot` | `one` | Synonym slot used when the UI doesn't specify one. |

## Authorization

Access is controlled by the `GraphCmsUiAuthorizationPolicy.Default` policy. By default it requires the
`CmsAdmins`, `Administrators`, or `WebAdmins` role. Override it by passing an `AuthorizationOptions`
action to `AddGraphCmsUi()`:

```csharp
builder.Services.AddGraphCmsUi(auth =>
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

- Pinned results target **internal CMS content only**. Graph's `targetKey` is a content GUID, so there is no way to pin an external link.
- Pinned results display the content's own title/description — the pinned item carries no display fields to override them with.
- When several pinned items share a phrase, Graph **shows only the top five**, ordered by priority. Storage isn't capped.
- Graph matches a pinned item's `phrases` value as a single literal string. The comma-separated box in the UI is therefore split into one Graph item per phrase; a phrase cannot itself contain a comma.
- Synonyms are **per-language**; there is no shared "all languages" slot.
- A synonym list is stored as one document and saved whole, so two editors working on the same language and slot at the same time will overwrite each other.

## License

MIT
