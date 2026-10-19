using MediaBrowser.Model.Dto;

namespace SourcePriority;

public static class SourceOrdering
{
    // Linux container paths: match case and directory boundaries exactly.
    public static bool Matches(string? path, IEnumerable<string> prefixes)
    {
        if (string.IsNullOrEmpty(path) || !path.StartsWith('/')) return false;
        foreach (var entry in prefixes)
        {
            if (entry == "/") return true;
            var prefix = entry?.TrimEnd('/');
            if (string.IsNullOrEmpty(prefix) || !prefix.StartsWith('/')) continue;
            if (path.Equals(prefix, StringComparison.Ordinal)
                || path.StartsWith(prefix + "/", StringComparison.Ordinal)) return true;
        }
        return false;
    }

    public static int Priority(string? path, Configuration config, IEnumerable<Guid> libraryIds)
    {
        var ids = libraryIds.ToHashSet();
        // Nested overrides win. For equally specific rules in shared libraries, higher wins.
        var match = (config.Libraries ?? [])
            .Where(l => ids.Contains(l.LibraryId))
            .SelectMany(l => l.Folders ?? [])
            .Where(f => Matches(path, [f.Path]))
            .OrderByDescending(f => f.Path.TrimEnd('/').Length)
            .ThenByDescending(f => f.Priority)
            .FirstOrDefault();
        return match?.Priority ?? (Matches(path, config.DeprioritizedPaths ?? []) ? -100 : 0);
    }

    public static MediaSourceInfo[] Order(MediaSourceInfo[] sources, Configuration config,
        IEnumerable<Guid>? libraryIds = null, Func<MediaSourceInfo, IEnumerable<Guid>>? sourceLibraries = null)
        => !config.Enabled || sources.Length < 2 ? sources
            : sources.OrderByDescending(s => Priority(s.Path, config,
                sourceLibraries?.Invoke(s) ?? libraryIds ?? [])).ToArray();
}
