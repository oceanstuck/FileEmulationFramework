﻿using FileEmulationFramework.Interfaces;
using FileEmulationFramework.Interfaces.Reference;
using FileEmulationFramework.Lib.IO;
using FileEmulationFramework.Lib.Utilities;
using SPD.File.Emulator.Spd;
using SPD.File.Emulator.Utilities;

namespace SPD.File.Emulator;

/// <summary>
/// Simple emulator for Atlus SPD files.
/// </summary>
public class SpdEmulator : IEmulator
{
    /// <summary>
    /// If enabled, dumps newly emulated files.
    /// </summary>
    public bool DumpFiles { get; set; }

    // Note: Handle->Stream exists because hashing IntPtr is easier; thus can resolve reads faster.
    private readonly SpdBuilderFactory _builderFactory;
    private Dictionary<string, MultiStream?> _pathToStream = new(StringComparer.OrdinalIgnoreCase);
    private Logger _log;

    public SpdEmulator(Logger log, bool dumpFiles)
    {
        _log = log;
        DumpFiles = dumpFiles;
        _builderFactory = new(log);
    }

    public bool TryCreateFile(IntPtr handle, string filepath, string route, out IEmulatedFile emulated)
    {
        // Check if we already made a custom SPD for this file.
        emulated = null!;
        if (_pathToStream.TryGetValue(filepath, out var multiStream))
        {
            // Avoid recursion into same file.
            if (multiStream == null)
                return false;

            emulated = new EmulatedFile<MultiStream>(multiStream);
            return true;
        }

        // Check extension.
        if (!filepath.EndsWith(Constants.SpdExtension, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!TryCreateEmulatedFile(handle, filepath, filepath, filepath, ref emulated!, out _))
            return false;

        return true;
    }

    /// <summary>
    /// Tries to create an emulated file from a given file handle.
    /// </summary>
    /// <param name="handle">Handle of the file where the data is sourced from.</param>
    /// <param name="srcDataPath">Path of the file where the handle refers to.</param>
    /// <param name="outputPath">Path where the emulated file is stored.</param>
    /// <param name="route">The route of the emulated file, for builder to pick up.</param>
    /// <param name="emulated">The emulated file.</param>
    /// <param name="stream">The created stream under the hood.</param>
    /// <returns>True if an emulated file could be created, false otherwise</returns>
    public bool TryCreateEmulatedFile(IntPtr handle, string srcDataPath, string outputPath, string route, ref IEmulatedFile? emulated, out MultiStream? stream)
    {
        stream = null;
        // Check if there's a known route for this file
        // Put this before actual file check because I/O.
        if (!_builderFactory.TryCreateFromPath(route, out var builder))
            return false;

        // Check file type.
        if (!SpdChecker.IsSpdFile(handle))
            return false;

        // Make the SPD file.
        _pathToStream[outputPath] = null; // Avoid recursion into same file.

        stream = builder!.Build(handle, srcDataPath, _log);

        _pathToStream.TryAdd(outputPath, stream);
        emulated = new EmulatedFile<MultiStream>(stream);
        _log.Info("[SpdEmulator] Created Emulated file with Path {0}", outputPath);

        if (DumpFiles)
            DumpFile(outputPath, stream);

        return true;
    }

    /// <summary>
    /// Called when a mod is being loaded.
    /// </summary>
    /// <param name="modFolder">Folder where the mod is contained.</param>
    public void OnModLoading(string modFolder)
    {
        var redirectorFolder = $"{modFolder}/{Constants.RedirectorFolder}";

        if (Directory.Exists(redirectorFolder))
            _builderFactory.AddFromFolders(redirectorFolder);
    }

    private void DumpFile(string filepath, MultiStream stream)
    {
        var filePath = Path.GetFullPath($"{Constants.DumpFolder}/{Path.GetFileName(filepath)}");
        Directory.CreateDirectory(Constants.DumpFolder);
        _log.Info($"[SpdEmulator] Dumping {filepath}");
        using var fileStream = new FileStream(filePath, FileMode.Create);
        stream.CopyTo(fileStream);
        _log.Info($"[SpdEmulator] Written To {filePath}");
    }

    internal List<RouteGroupTuple> GetInput() => _builderFactory._routeGroupTuples;

    internal void AddFromFolders(string dir) => _builderFactory.AddFromFolders(dir);
}
