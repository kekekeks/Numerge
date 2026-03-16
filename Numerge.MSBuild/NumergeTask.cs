using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.NET.StringTools;

namespace Numerge.MSBuild;

public class NumergeTask : Task
{
    public string? ConfigFile { get; set; }

    public bool ClearIntermediatePackages { get; set; }

    [Required] public string PackageVersion { get; set; } = null!;
    [Required] public string Configuration { get; set; } = null!;
    [Required] public string ProjectDirectory { get; set; } = null!;

    public override bool Execute()
    {
        Log.LogMessage(MessageImportance.Normal, "Starting Numerge task execution");

        var numergeConfigFile = ConfigFile ?? "numerge.config.json";
        var projectDirectory = ProjectDirectory!;
        Log.LogMessage(MessageImportance.Normal, $"Project directory: {projectDirectory}");

        if (!Path.IsPathRooted(numergeConfigFile))
        {
            Log.LogMessage(MessageImportance.Normal,
                $"Numerge config path ({numergeConfigFile}) isn't rooted, evaluating as relative to current project file ({projectDirectory})");
            numergeConfigFile = Path.Combine(projectDirectory, numergeConfigFile);
        }

        Log.LogMessage(MessageImportance.Normal, $"Target numerge config file: {numergeConfigFile}");
        if (!File.Exists(numergeConfigFile))
        {
            Log.LogError($"Numerge config file doesn't exist at {numergeConfigFile}");
            return false;
        }

        Log.LogMessage(MessageImportance.Normal, "Loading Numerge configuration");
        var config = MergeConfiguration.LoadFile(numergeConfigFile);

        var solutionDirectory = FindSolutionDirectory(projectDirectory);
        Log.LogMessage(MessageImportance.Normal, $"Solution directory: {solutionDirectory}");

        var tempPath = Path.GetTempPath() + Guid.NewGuid();
        Log.LogMessage(MessageImportance.Normal, $"Creating temporary directory: {tempPath}");
        Directory.CreateDirectory(tempPath);

        try
        {
            Log.LogMessage(MessageImportance.Normal, "Moving .nupkg files to temporary directory");
            MovePackagesToTempDirectory(solutionDirectory, "nupkg", Configuration, config, tempPath, PackageVersion,
                ClearIntermediatePackages);

            Log.LogMessage(MessageImportance.Normal, "Moving .snupkg files to temporary directory");
            MovePackagesToTempDirectory(solutionDirectory, "snupkg", Configuration, config, tempPath, PackageVersion,
                ClearIntermediatePackages);

            var outputDirectory = Path.Combine(projectDirectory, "bin", Configuration);
            Log.LogMessage(MessageImportance.Normal, $"Output directory: {outputDirectory}");

            Log.LogMessage(MessageImportance.Normal, "Starting NuGet package merge process");
            var mergeResult =
                NugetPackageMerger.Merge(tempPath, outputDirectory, config, new NumergeMSBuildLogger(Log));

            if (mergeResult)
            {
                Log.LogMessage(MessageImportance.High, "Numerge task completed successfully");
            }
            else
            {
                Log.LogError("Numerge task failed");
            }

            return mergeResult;
        }
        catch (Exception ex)
        {
            Log.LogErrorFromException(ex, true);
            return false;
        }
        finally
        {
            Log.LogMessage(MessageImportance.Normal, $"Cleaning up temporary directory: {tempPath}");
            Directory.Delete(tempPath, true);
        }
    }

    private void MovePackagesToTempDirectory(string solutionDirectory, string extension, string configuration,
        MergeConfiguration config, string destination, string version, bool move)
    {
        var targetFileNames = config.Packages.SelectMany(x => x.Merge)
            .Select(mergeConfiguration => mergeConfiguration.Id)
            .Concat(config.Packages.Select(x => x.Id))
            .Select(id => $"{id}.{version}.{extension}")
            .ToImmutableArray();

        var files = Directory.GetFiles(solutionDirectory, "*." + extension, SearchOption.AllDirectories)
            .Where(s => s.Contains(configuration))
            .Where(s => targetFileNames.Contains(Path.GetFileName(s)));

        foreach (var file in files)
        {
            if (move)
            {
                File.Move(file, Path.Combine(destination, Path.GetFileName(file)));
            }
            else
            {
                File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
            }
        }
    }

    private string FindSolutionDirectory(string projectDirectory)
    {
        while (!string.IsNullOrEmpty(projectDirectory))
        {
            var solutionFiles = Directory.GetFiles(projectDirectory, "*.sln");
            if (solutionFiles.Length > 0)
            {
                return projectDirectory;
            }

            projectDirectory = Path.GetDirectoryName(projectDirectory)!;
        }

        return Path.GetDirectoryName(projectDirectory)!;
    }
}