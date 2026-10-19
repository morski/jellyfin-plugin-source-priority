using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Querying;
using MediaBrowser.Model.MediaInfo;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace SourcePriority;

// Runs after Jellyfin's device-aware ordering, before DTO serialization.
public sealed class SourcePriorityFilter(ILibraryManager library) : IAsyncResultFilter
{
    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        var memberships = new Dictionary<Guid, Guid[]>();
        Guid[] Membership(BaseItem item)
        {
            if (!memberships.TryGetValue(item.Id, out var ids))
                memberships[item.Id] = ids = library.GetCollectionFolders(item).Select(f => f.Id).ToArray();
            return ids;
        }
        var config = Plugin.Instance?.Configuration;
        if (config?.Enabled == true && context.Result is ObjectResult result)
        {
            switch (result.Value)
            {
                case PlaybackInfoResponse playback:
                    if (context.RouteData.Values.TryGetValue("itemId", out var value)
                        && Guid.TryParse(value?.ToString(), out var id)
                        && library.GetItemById(id) is BaseItem entity && entity is Movie or Episode)
                        playback.MediaSources = Order(playback.MediaSources.ToArray(), config, entity, Membership);
                    break;
                case BaseItemDto item:
                    Apply(item, config, Membership);
                    break;
                case QueryResult<BaseItemDto> query:
                    foreach (var dto in query.Items) Apply(dto, config, Membership);
                    break;
                case IEnumerable<BaseItemDto> items:
                    foreach (var dto in items) Apply(dto, config, Membership);
                    break;
            }
        }
        await next();
    }

    private void Apply(BaseItemDto item, Configuration config, Func<BaseItem, Guid[]> membership)
    {
        if (item.Type is BaseItemKind.Movie or BaseItemKind.Episode && item.MediaSources is { Length: > 1 })
        {
            var entity = (config.Libraries?.Length ?? 0) > 0 ? library.GetItemById(item.Id) : null;
            item.MediaSources = Order(item.MediaSources, config, entity, membership);
        }
    }

    private MediaSourceInfo[] Order(MediaSourceInfo[] sources, Configuration config,
        BaseItem? primary, Func<BaseItem, Guid[]> membership)
    {
        if ((config.Libraries?.Length ?? 0) == 0) return SourceOrdering.Order(sources, config);
        var fallback = primary == null ? [] : membership(primary);
        return SourceOrdering.Order(sources, config, fallback, source =>
            Guid.TryParse(source.Id, out var id) && library.GetItemById(id) is BaseItem version
                ? membership(version) : fallback);
    }
}
