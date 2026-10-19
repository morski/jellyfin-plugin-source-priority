using MediaBrowser.Model.Dto;
using SourcePriority;
using System.Reflection;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using MediaBrowser.Model.MediaInfo;
using MediaBrowser.Model.Querying;
using Jellyfin.Data.Enums;

int count = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception(name); count++; }
var config = new Configuration();
Check(config.DeprioritizedPaths.Length == 0 && config.Libraries.Length == 0, "neutral defaults");
config.DeprioritizedPaths = ["/example/archive"];
MediaSourceInfo Source(string id, string? path) => new() { Id = id, Path = path! };
string Ids(MediaSourceInfo[] sources) => string.Join(",", sources.Select(s => s.Id));
Check(SourceOrdering.Matches("/example/archive/movie/file.mkv", config.DeprioritizedPaths), "subdirectory");
Check(SourceOrdering.Matches("/example/archive", config.DeprioritizedPaths), "exact path");
Check(!SourceOrdering.Matches("/example/archive-other/file.mkv", config.DeprioritizedPaths), "boundary");
Check(!SourceOrdering.Matches("/example/ARCHIVE/file.mkv", config.DeprioritizedPaths), "case");
Check(!SourceOrdering.Matches(null, config.DeprioritizedPaths), "missing path");
Check(!SourceOrdering.Matches("https://example.org/example/archive/file", config.DeprioritizedPaths), "URL untouched");
var sources = new[] { Source("s1", "/example/archive/a"), Source("n1", "/example/primary/a"), Source("s2", "/example/archive/b"), Source("n2", "/example/primary/b") };
Check(Ids(SourceOrdering.Order(sources, config)) == "n1,n2,s1,s2", "stable partition");
Check(Ids(sources) == "s1,n1,s2,n2", "original array untouched");
Check(Ids(SourceOrdering.Order([sources[0]], config)) == "s1", "only slow copy retained");
Check(SourceOrdering.Order([], config).Length == 0, "empty");
Check(Ids(SourceOrdering.Order([sources[0], sources[2]], config)) == "s1,s2", "all slow stable");
config.Enabled = false;
Check(ReferenceEquals(SourceOrdering.Order(sources, config), sources), "disabled");
Check(SourceOrdering.Matches("/example/archive/a", ["/example/archive/"]), "trailing slash");
Check(!SourceOrdering.Matches("/example/archive/a", [""]), "empty rule");
config.Enabled = true;
var paths = Stub.Create<IApplicationPaths>((_, _) => Path.GetTempPath());
var serializer = Stub.Create<IXmlSerializer>((_, _) => config);
_ = new Plugin(paths, serializer);
var movie = new Movie();
BaseItem currentItem = movie;
var libraryId = Guid.NewGuid();
var otherLibraryId = Guid.NewGuid();
var library = Stub.Create<ILibraryManager>((method, _) => method!.Name == "GetCollectionFolders"
    ? new List<Folder> { new Folder { Id = libraryId } } : currentItem);
var filter = new SourcePriorityFilter(library);
async Task<object> Run(object result, string? id = null)
{
    var route = new RouteData();
    if (id != null) route.Values["itemId"] = id;
    var action = new ActionContext(new DefaultHttpContext(), route, new ActionDescriptor());
    var filters = new List<IFilterMetadata>();
    var controller = new object();
    var objectResult = new ObjectResult(result);
    var context = new ResultExecutingContext(action, filters, objectResult, controller);
    bool nextCalled = false;
    await filter.OnResultExecutionAsync(context, () =>
    {
        nextCalled = true;
        return Task.FromResult(new ResultExecutedContext(action, filters, objectResult, controller));
    });
    Check(nextCalled, "filter continues response");
    return result;
}
var playback = new PlaybackInfoResponse { MediaSources = sources };
await Run(playback, Guid.NewGuid().ToString());
Check(Ids(playback.MediaSources.ToArray()) == "n1,n2,s1,s2", "playback response");
var explicitSelection = new PlaybackInfoResponse { MediaSources = [sources[0]] };
await Run(explicitSelection, Guid.NewGuid().ToString());
Check(explicitSelection.MediaSources.Single().Id == "s1", "explicit selected version retained");
var dto = new BaseItemDto { Type = BaseItemKind.Movie, MediaSources = sources };
await Run(dto);
Check(Ids(dto.MediaSources) == "n1,n2,s1,s2", "movie details");
var episode = new BaseItemDto { Type = BaseItemKind.Episode, MediaSources = sources };
await Run(episode);
Check(Ids(episode.MediaSources) == "n1,n2,s1,s2", "episode details ordered");
dto.MediaSources = sources;
await Run(new QueryResult<BaseItemDto> { Items = [dto] });
Check(Ids(dto.MediaSources) == "n1,n2,s1,s2", "query result");
dto.MediaSources = sources;
await Run(new[] { dto });
Check(Ids(dto.MediaSources) == "n1,n2,s1,s2", "array result");
var invalidRoute = new PlaybackInfoResponse { MediaSources = sources };
await Run(invalidRoute, "invalid");
Check(Ids(invalidRoute.MediaSources.ToArray()) == Ids(sources), "invalid route untouched");
config.Enabled = false;
dto.MediaSources = sources;
await Run(dto);
Check(ReferenceEquals(dto.MediaSources, sources), "filter disabled");
config.Enabled = true;
currentItem = new Episode();
var episodePlayback = new PlaybackInfoResponse { MediaSources = sources };
await Run(episodePlayback, Guid.NewGuid().ToString());
Check(Ids(episodePlayback.MediaSources.ToArray()) == "n1,n2,s1,s2", "episode playback");
var audio = new BaseItemDto { Type = BaseItemKind.Audio, MediaSources = sources };
await Run(audio);
Check(ReferenceEquals(audio.MediaSources, sources), "audio untouched");
config.Libraries = [new LibraryPriority { LibraryId = libraryId, Folders = [
    new FolderPriority { Path = "/example", Priority = 10 },
    new FolderPriority { Path = "/example/archive", Priority = -5 },
    new FolderPriority { Path = "/example/archive/special", Priority = 50 }
]}, new LibraryPriority { LibraryId = otherLibraryId, Folders = [
    new FolderPriority { Path = "/example/archive", Priority = 100 }
]}];
Check(SourceOrdering.Priority("/example/archive/a", config, [libraryId]) == -5, "library priority");
Check(SourceOrdering.Priority("/example/archive/a", config, [otherLibraryId]) == 100, "same path other library");
Check(SourceOrdering.Priority("/example/archive/special/a", config, [libraryId]) == 50, "nested override");
Check(SourceOrdering.Priority("/example/primary/a", config, [libraryId]) == 10, "parent priority");
Check(SourceOrdering.Priority("/unconfigured/a", config, [libraryId]) == 0, "unconfigured neutral");
Check(SourceOrdering.Priority("/example/archive/a", config, []) == -100, "legacy fallback");
Check(SourceOrdering.Priority("/example/archive/a", config, [libraryId, otherLibraryId]) == 100, "shared library tie");
Check(SourceOrdering.Matches("/example/file", ["/"]), "filesystem root");
var rankedEpisode = new PlaybackInfoResponse { MediaSources = [Source("slow", "/example/archive/a"), Source("special", "/example/archive/special/a"), Source("fast", "/example/primary/a")] };
await Run(rankedEpisode, Guid.NewGuid().ToString());
Check(Ids(rankedEpisode.MediaSources.ToArray()) == "special,fast,slow", "episode library membership ranking");
dto.Id = Guid.NewGuid(); dto.MediaSources = sources;
await Run(dto);
Check(Ids(dto.MediaSources) == "n1,n2,s1,s2", "movie DTO library membership");
config.Libraries[0].Folders = [new FolderPriority { Path = "/example/archive", Priority = 200 }];
episode.MediaSources = sources;
await Run(episode);
Check(Ids(episode.MediaSources) == "s1,s2,n1,n2", "episode DTO high priority overrides legacy");
var configXml = new System.Xml.Serialization.XmlSerializer(typeof(Configuration));
using var xml = new StringWriter(); configXml.Serialize(xml, config);
var restored = (Configuration)configXml.Deserialize(new StringReader(xml.ToString()))!;
Check(restored.Libraries[0].LibraryId == libraryId && restored.Libraries[0].Folders[0].Priority == 200,
    "XML configuration round trip");
var settingsResource = typeof(Plugin).Assembly.GetManifestResourceStream("SourcePriority.configPage.html");
Check(settingsResource != null, "embedded settings page");
Check(Plugin.Instance!.GetPages().Single().EmbeddedResourcePath == "SourcePriority.configPage.html", "page registration");
var firstVersion = new Episode { Id = Guid.NewGuid() };
var secondVersion = new Episode { Id = Guid.NewGuid() };
((Stub)(object)library).Handler = (method, args) => method!.Name == "GetCollectionFolders"
    ? new List<Folder> { new Folder { Id = ReferenceEquals(args![0], secondVersion) ? otherLibraryId : libraryId } }
    : (Guid)args![0]! == secondVersion.Id ? secondVersion : firstVersion;
config.Libraries = [
    new LibraryPriority { LibraryId = libraryId, Folders = [new FolderPriority { Path = "/a", Priority = 5 }] },
    new LibraryPriority { LibraryId = otherLibraryId, Folders = [new FolderPriority { Path = "/b", Priority = 10 }] }
];
var crossLibrary = new PlaybackInfoResponse { MediaSources = [Source(firstVersion.Id.ToString("N"), "/a/episode"), Source(secondVersion.Id.ToString("N"), "/b/episode")] };
await Run(crossLibrary, firstVersion.Id.ToString());
Check(crossLibrary.MediaSources[0].Id == secondVersion.Id.ToString("N"), "cross-library version uses its own library priority");
config.Libraries[1].Folders[0].Priority = 5;
config.DeprioritizedPaths = [];
Check(Ids(SourceOrdering.Order(sources, config, [libraryId])) == Ids(sources), "equal priorities preserve input order");
Console.WriteLine($"PASS: {count} ordering, MVC, configuration and resource checks");

public class Stub : DispatchProxy
{
    public Func<MethodInfo?, object?[]?, object?> Handler = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method, args);
    public static T Create<T>(Func<MethodInfo?, object?[]?, object?> handler) where T : class
    {
        var proxy = DispatchProxy.Create<T, Stub>();
        ((Stub)(object)proxy).Handler = handler;
        return proxy;
    }
}
