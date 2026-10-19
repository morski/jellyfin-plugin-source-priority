using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace SourcePriority;

public sealed class Configuration : BasePluginConfiguration
{
    public bool Enabled { get; set; } = true;
    public LibraryPriority[] Libraries { get; set; } = [];
    // Retained for 1.0 configuration compatibility, until the settings page migrates it.
    public string[] DeprioritizedPaths { get; set; } = [];
}

public sealed class LibraryPriority
{
    public Guid LibraryId { get; set; }
    public FolderPriority[] Folders { get; set; } = [];
}

public sealed class FolderPriority
{
    public string Path { get; set; } = string.Empty;
    public int Priority { get; set; }
}

public sealed class Plugin : BasePlugin<Configuration>, IHasWebPages
{
    public static Plugin? Instance { get; private set; }
    public Plugin(IApplicationPaths paths, IXmlSerializer serializer) : base(paths, serializer)
        => Instance = this;
    public override string Name => "Source Priority";
    public override Guid Id => new("5abdf826-f64e-45c5-b3d8-9b9cf31a053f");
    public IEnumerable<PluginPageInfo> GetPages() => [new PluginPageInfo
    {
        Name = "SourcePriority",
        DisplayName = Name,
        EmbeddedResourcePath = "SourcePriority.configPage.html"
    }];
    public override string Description => "Choose library folder priorities for movie and episode versions.";
}

public sealed class ServiceRegistrator : IPluginServiceRegistrator
{
    public void RegisterServices(IServiceCollection services, IServerApplicationHost host)
    {
        services.AddTransient<SourcePriorityFilter>();
        services.Configure<MvcOptions>(options => options.Filters.AddService<SourcePriorityFilter>());
    }
}
