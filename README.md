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

After install, log in to the CMS as an administrator, open the product menu in the top bar — the **CMS ⌄** dropdown — and pick **Graph**. It sits alongside CMS itself, in the slot Search & Navigation used to occupy. The left menu then offers the two features, which are also tabs on the page itself:

- **Pinned Results** — pin specific CMS content to the top of the search results for chosen phrases. Organised into collections, scoped per site and per language. Previously called Best Bets in Search & Navigation.
- **Synonyms** — define one-way (`a => b`) and bidirectional (`a <=> b`) term equivalences. Scoped per language and per slot. Import/export the CMS 12 CSV format directly.

### Scoping

The two features are scoped differently, because Graph scopes them differently:

| | Per site | Per language | Per slot |
|---|---|---|---|
| Pinned Results | ✅ (query must opt in — see below) | ✅ | — |
| Synonyms | ❌ | ✅ | ✅ |

Synonyms have **no per-site dimension in Graph** — a synonym list belongs to a language and a slot, and applies to every site sharing that Graph instance. The site picker and the collection picker are therefore both hidden on the Synonyms tab.

### Working across languages

The two features reach "all languages" by different routes, because Graph supports it for one and not the other:

| | Mechanism | What it is |
|---|---|---|
| Pinned Results | `language: null` on the stored item | A **real scope**. One item, stored once, fires whatever language is being searched. |
| Synonyms | *Add to all languages* checkbox | A **write-time fan-out**. One copy per enabled language, independent afterwards. |

For pinned results, tick **All languages** on the form and the pin applies everywhere. Such pins stay visible whatever language the toolbar is set to — they apply to that language too — and are badged *All languages* in the list.

For synonyms, tick **All languages** and the rule is written into every enabled language's slot for the selected slot number. Because there is no shared list behind it, editing or deleting one language's copy leaves the others alone, and the result is reported per language (`Added to 5 of 7 languages`) rather than as a single success. Languages sharing an ISO code — `en` and `en-GB` both route to `en` — are written once.

Pinned results live in **Graph collections**. Every site gets a `default-<site>` collection, created on first use, and you can add more from the toolbar — one per editorial use case, e.g. `black-friday-<site>`. Collection keys are always `<name>-<site>`; the site suffix is what keeps one site's pins out of another's.

> **Per-site scoping only takes effect if your query asks for it.** Graph evaluates **all active
> collections** when a query omits the `collections` argument. On a multi-site solution sharing one
> Graph instance, that means site A's pinned results fire on site B's searches. Pass the collection
> explicitly to keep them apart.

`collections` takes the collection **key**, not its id. Passing the GUID returns zero results — silently, exactly as if you had named a collection that doesn't exist. The UI shows the key of the selected collection with a copy button, next to the id (which you only need for Graph's REST API).

```graphql
query Search($searchText: String) {
  ArticlePage(
    where: { _fulltext: { match: $searchText } }
    pinned: { phrase: $searchText, collections: ["default-mysite"] }
  ) {
    items { Name }
  }
}
```

With the .NET client (`Optimizely.Graph.Cms.Query`):

```csharp
using Optimizely.Graph.Cms.Query;
using Optimizely.Graph.Cms.Query.Abstractions;

public class SearchService(IGraphContentClient client)
{
    public async Task<IEnumerable<ArticlePage>> SearchAsync(string phrase) =>
        await client
            .QueryContent<ArticlePage>()
            .SearchFor(phrase)
            .WithPinned(phrase, "default-mysite")   // collection KEY, despite the parameter name
            .SetLocale("en")
            .GetAsContentAsync();
}
```

> ⚠️ `WithPinned` has three overloads, and two of them are traps:
> `WithPinned(string phrase, Guid? collectionId)` and `WithPinned(string phrase, IEnumerable<Guid>)`
> take GUIDs, which Graph does not match on — they return zero pinned results, silently.
> Use `WithPinned(string phrase, params string[])` and pass **keys**, or `WithPinned(phrase)`
> alone to evaluate every active collection. The `params string[]` parameter is *named*
> `collectionIds`, which does not help.

If you run a **single site** with a single collection, you can omit the collection argument entirely — the default behaviour of evaluating every active collection is then what you want.

### Managing collections

The **Collection** picker in the toolbar applies to the Pinned Results tab only:

- **Select** — switches which collection you're editing. Opens on `default`.
- **New** — prompts for a name, slugified into `<name>-<site>`. Use one per campaign or use case.
- **Delete** — removes the collection **and every pinned result in it**, in all languages. The `default` collection can't be deleted (it would just be recreated empty); delete its pinned results individually instead.

Deleting a collection breaks any query still passing its key, so the confirmation names the key.

### Synonym slots

Graph exposes **two synonym slots per language**, named `ONE` and `TWO`. Which one applies is chosen by the query, not by the data, so your GraphQL must say which slot it wants.

`synonyms` goes **inside the field filter**, alongside the operator — not as a sibling of `where`:

```graphql
{
  ArticlePage(locale: en, where: { MainBody: { contains: "fruit", synonyms: [ONE] } }) {
    items { Name }
  }
}
```

The same thing with the .NET client (`Optimizely.Graph.Cms.Query`), which is how most CMS 13 sites query Graph:

```csharp
using Optimizely.Graph.Cms.Query;
using Optimizely.Graph.Cms.Query.Abstractions;
using Optimizely.Graph.Cms.Query.Filtering;
using Optimizely.Graph.Cms.Query.Implementation.Request.Expressions; // SynonymSlot

public class SearchService(IGraphContentClient client)
{
    // Full-text search, expanded with the synonyms in slot One.
    public async Task<IEnumerable<ArticlePage>> SearchAsync(string phrase) =>
        await client
            .QueryContent<ArticlePage>()
            .SearchFor(phrase, synonymSlots: [SynonymSlot.One])
            .SetLocale("en")
            .GetAsContentAsync();

    // Or apply synonyms to one field instead of the whole document.
    public async Task<IEnumerable<ArticlePage>> ByHeadingAsync(string phrase) =>
        await client
            .QueryContent<ArticlePage>()
            .Where(x => x.Heading.Match(phrase, SynonymSlot.One))
            .GetAsContentAsync();
}
```

Three things that cost time if you don't know them:

- `SynonymSlot` lives in `Optimizely.Graph.Cms.Query.Implementation.Request.Expressions` — an `Implementation` namespace, but it is the public enum you need. Values are `SynonymSlot.One` and `SynonymSlot.Two`.
- `UsingFullText()` is obsolete; `SearchFor(query, highlightTag, boost, synonymSlots)` replaces it and is the only overload that accepts slots.
- `GetAsContentAsync()` returns `IGetAsContentResult<T>`, which *is* an `IEnumerable<T>` — there's no `.Content` property to unwrap.

Pick the matching slot in the UI before adding or importing synonyms. Changes can take a few minutes to take effect.

## Configuration

Graph credentials are read from Optimizely's own `Optimizely:ContentGraph` section, so they are
never entered twice. The addon's own settings live in a `Gulla:GraphCmsUi` section:

```json
{
  "Gulla": {
    "GraphCmsUi": {
      "DefaultLanguage": "nb-NO",
      "DefaultSlot": "two"
    }
  }
}
```

The same settings can be set in code instead, which takes precedence over `appsettings.json`:

```csharp
builder.Services.AddGraphCmsUi(options =>
{
    options.DefaultLanguage = "nb-NO"; // default: the first enabled language
    options.DefaultSlot = "two";       // default: "one"
});
```

| Option | Default | Purpose |
|---|---|---|
| `DefaultLanguage` | first enabled language | Language pre-selected in the language picker on both tabs when the URL doesn't name one. |
| `DefaultSlot` | `one` | Synonym slot used when the UI doesn't specify one. |

`DefaultLanguage` accepts either a CMS language ID (`nb-NO`) or a bare ISO code (`nb`) — both
match the enabled language either way round. A value that matches no enabled language is
ignored, and the first enabled language is used.

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
- Pinned results display the content's own title/description. The pinned item carries no display fields to override them with.
- When several pinned items share a phrase, Graph **shows only the top five**, ordered by priority. Storage isn't capped.
- Graph matches a pinned item's `phrases` value as a single literal string. The comma-separated box in the UI is therefore split into one Graph item per phrase; a phrase cannot itself contain a comma.
- Synonyms are **per-language** in Graph, and Graph folds related variants into one list — `no`, `nb`, `nn` and `nn-NO` all address the same document, verified against a live instance. The Synonyms tab names the languages that share a list when it can measure it. There is no shared "all languages" slot: omitting `language_routing` writes a real `standard` slot, but that applies only to queries with no `locale` argument. The UI's *Add to all languages* checkbox works around this by writing a copy into every enabled language, but the copies are independent from that point on &mdash; editing or deleting one does not touch the others.
- A synonym list is stored as one document and saved whole, so two editors working on the same language and slot at the same time will overwrite each other.

## License

MIT
