# Source Priority 1.1.1

Companion plugin for Jellyfin Server **12.2** (API packages `12.2.0`, .NET 10), for **movie and TV episode versions**. Leave Merge Versions installed to group duplicate items. Source Priority orders sources; it does not merge, split, rename or delete files or modify library records.

## Install from the repository

After the first GitHub release is published, add this URL under Dashboard → Plugins → Repositories:

```text
https://raw.githubusercontent.com/morski/jellyfin-plugin-source-priority/main/manifest.json
```

Then install **Source Priority** from the catalog and restart Jellyfin. Keep Merge Versions installed.

The catalog is empty until the first release workflow succeeds. The release workflow publishes a DLL-only ZIP and updates the catalog. Live plugin loading and client playback remain unverified.

## Plugin settings page

After a future installation, open Dashboard → Plugins → Source Priority → Settings.

The page reads Jellyfin’s configured libraries and lists each library’s media-folder locations. It does not recursively list individual movie, series or season directories. Set a numeric priority for each folder. **Higher numbers come first**; equal priorities preserve Jellyfin’s order. Use **Add subfolder override** for a more specific path.

Example, within a Movies or TV library:

| Folder | Priority |
| --- | ---: |
| `/example/primary` | 20 |
| `/example/secondary` | 0 |
| `/example/archive` | -100 |

If the library has only `/example` configured as its root, leave `/example` at 0 and add `/example/archive` as a subfolder override at -100. A nested override such as `/example/archive/special` at 50 takes precedence over both parent rules.

The page validates paths and whole-number priorities, refuses duplicate paths, detects configuration changes by another editor, and reads settings back after saving. Settings apply to new responses without restarting Jellyfin; clients may cache old responses. Library names and paths are rendered as text.

The page lists all library types, but ordering only applies to movie and episode sources. Settings are per library ID, so renaming a library retains its rules. Rules for temporarily missing libraries are retained. A recreated library with a new ID needs its priorities configured again. If library paths change, reopen the settings page and remove obsolete subfolder rules before saving.

## Selection rules

- Paths use exact case and directory boundaries: `/example/archive` matches itself and descendants, not `/example/archive-other` or `/example/ARCHIVE`.
- The longest matching folder rule wins. An unconfigured path has priority 0, except the legacy fallback described below. Higher priority sorts first, with stable order for ties.
- Library membership comes from Jellyfin. For cross-library merged versions, each source uses its own item’s library memberships when its source ID resolves to an item; otherwise it uses the primary item’s memberships. If an item belongs to several libraries, the longest matching path wins across those libraries; equal-length matches use the highest priority.
- Explicitly selected versions and sole copies remain available. All source capability flags are retained. Audio and other media are unchanged. A show or season itself is not ranked; the rule operates on each episode’s versions.
- The plugin changes movie/episode detail and list responses containing sources, plus playback-info responses after Jellyfin’s normal device-aware sorting. Folder priority takes precedence over that sorting, so a high-priority copy can require transcoding while a lower-priority copy could direct-play.

Paths must be those **visible inside Jellyfin’s container**, not the host mount-source paths. All paths in this document are fictional examples.

### Existing 1.0 settings

The plugin keeps its existing ID and configuration filename. New configurations contain no folder rules and no built-in path preferences: every folder starts at priority 0. Existing user-configured priorities are retained. The optional legacy `DeprioritizedPaths` array defaults to empty; if explicitly populated by an older configuration, its entries have priority -100 when no matching library rule exists. The page displays applicable legacy entries as editable overrides. Explicit library rules take precedence.

## Client compatibility limits

This is a server source-order preference, not a guarantee that every client obeys it. Clients can reorder sources, cache old lists, choose a primary version before asking for playback info, or explicitly submit a `MediaSourceId`. Explicit selections are respected; files or editions are never silently substituted.

Jellyfin’s `AutoOpenLiveStream` processing happens before this result filter, so an already opened source cannot be reselected here. This mostly concerns dynamically opened sources. Response shapes outside the handled detail/list and playback-info DTOs are not rewritten.

## Build and tests

```sh
dotnet build src/SourcePriority.csproj -c Release
dotnet run --project tests/Tests.csproj -c Release
node tests/test-ui.mjs
```

The build uses Jellyfin.Controller and Jellyfin.Model 12.2.0. The local SDK needed `-p:AllowMissingPrunePackageData=true` and an isolated ASP.NET Core 10 runtime to run tests; these are local environment workarounds, not server changes.

Validation completed:

- Build: zero warnings and errors.
- 55 passing checks: path boundaries, priorities, stable sorting, nested overrides, library separation and shared paths, cross-library episode versions, movie and episode MVC responses, explicit selection, disabled behavior, XML round trip, and embedded page registration.
- Settings-page JavaScript harness: library discovery, legacy-rule display, save/read-back including Guid normalization, invalid/duplicate paths, concurrent changes and loading errors. Uses fake DOM/API objects; it does not prove browser layout or live Jellyfin integration.
- Official Jellyfin client source confirms the `getVirtualFolders()` helper used by the page.

## Future installation and verification — not performed

Only `Jellyfin.Plugin.SourcePriority.dll` belongs in a dedicated folder under Jellyfin’s actual plugin directory. Do not copy test dependencies into Jellyfin. Restart Jellyfin after installation, then open the plugin settings page. Use only the current DLL; do not load older copies alongside it. No server paths or installation commands were verified.

Before relying on it, confirm plugin loading, page rendering, library-folder discovery, save/reload persistence and actual played paths for merged movies and episodes on each client in use. Test manual low-priority selection, resume, next-episode autoplay, sole copies, equal priorities, and a preferred copy requiring transcoding. Confirm Merge Versions still operates normally.

Nothing has been deployed, and no live Jellyfin API or playback checks were performed. Removing the plugin and restarting returns normal ordering without changing library items or merge groups.

## References

- https://github.com/jellyfin/jellyfin/releases/tag/v12.2
- https://github.com/jellyfin/jellyfin/blob/v12.2/Jellyfin.Api/Controllers/MediaInfoController.cs
- https://github.com/jellyfin/jellyfin/blob/v12.2/MediaBrowser.Controller/Plugins/IPluginServiceRegistrator.cs
- https://github.com/jellyfin/jellyfin-plugin-template
- https://github.com/jellyfin/jellyfin-apiclient-javascript/blob/master/src/apiClient.js
- https://github.com/danieladov/jellyfin-plugin-mergeversions
