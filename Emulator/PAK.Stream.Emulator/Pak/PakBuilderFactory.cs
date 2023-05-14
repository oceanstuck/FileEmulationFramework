﻿using FileEmulationFramework.Lib;
using FileEmulationFramework.Lib.IO;
using System.Text.RegularExpressions;

namespace PAK.Stream.Emulator.Pak;

public class PakBuilderFactory
{
    internal List<RouteGroupTuple> RouteGroupTuples = new();

    /// <summary>
    /// Adds all available routes from folders.
    /// </summary>
    /// <param name="redirectorFolder">Folder containing the redirector's files.</param>
    public void AddFromFolders(string redirectorFolder)
    {
        // Get contents.
        WindowsDirectorySearcher.GetDirectoryContentsRecursiveGrouped(redirectorFolder, out var groups);

        // Find matching folders.
        foreach (var group in groups)
        {
            if (group.Files.Length <= 0)
                continue;

            var route = Route.GetRoute(redirectorFolder, group.Directory.FullPath);

            RouteGroupTuples.Add(new RouteGroupTuple()
            {
                Route = new Route(route),
                Files = group
            });
        }
    }

    /// <summary>
    /// Tries to create an PAK from a given route.
    /// </summary>
    /// <param name="path">The file path/route to create PAK Builder for.</param>
    /// <param name="builder">The created builder.</param>
    /// <returns>True if a builder could be made, else false (if there are no files to modify this PAK).</returns>
    public bool TryCreateFromPath(string path, out PakBuilder? builder)
    {
        builder = default;
        var route = new Route(path);
        foreach (var group in RouteGroupTuples)
        {
            if (!route.Matches(group.Route.FullPath) && !RoutePartialMatches(route, group))
                continue;

            // Make builder if not made.
            builder ??= new PakBuilder();

            // Add files to builder.
            var dir = group.Files.Directory.FullPath;
            foreach (var file in group.Files.Files)
                builder.AddOrReplaceFile(Path.Combine(dir, file), Path.GetFileName(path));
        }

        return builder != null;
    }

    /// <summary>
    /// Check if a route is in a group. Unlike Route.Matches this considers the path to the actual archive file
    /// E.g. the route init_free.bin\field\script will match init_free.bin as this recognises the actual file is at init_free.bin
    /// </summary>
    /// <param name="route">The route to compare</param>
    /// <param name="group">The group to compare</param>
    /// <returns>True if the route contains the group, false otherwise</returns>
    private bool RoutePartialMatches(Route route, RouteGroupTuple group)
    {
        var match = Regex.Match(group.Route.FullPath, @".+\.[^\\]+");
        if (!match.Success) return false;
        return route.FullPath.Contains(match.Value);
    }
}

internal struct RouteGroupTuple
{
    /// <summary>
    /// Route associated with this tuple.
    /// </summary>
    public Route Route;

    /// <summary>
    /// Files bound by this route.
    /// </summary>
    public DirectoryFilesGroup Files;
}