using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Numerge
{
    public static class NugetPackageMerger
    {
        public static bool Merge(string directory, string output,
            MergeConfiguration configuration, INumergeLogger logger)
        {
            try
            {
                var packages = Directory.EnumerateFiles(directory, "*.nupkg").Select(p => new LoadedPackage(p))
                    .ToDictionary(p => p.Spec.Id);

                foreach (var p in packages.Values)
                    p.ResolveBinaryDependencies(packages);

                foreach (var config in configuration.Packages)
                {
                    logger.Info($"Processing {config.Id}");
                    if (!packages.TryGetValue(config.Id, out var package))
                        throw new MergeAbortedException($"Package {config.Id} not found");

                    var mergeConfigs = config.Merge?.ToList() ?? new List<PackageMergeConfiguration>();
                    if (config.MergeAll)
                    {
                        foreach (var dep in package.Spec.Dependencies
                            .SelectMany(x => x.Value).OfType<BinaryDependency>())
                        {
                            if (!mergeConfigs.Any(x => x.Id == dep.Id) &&
                                (config.Exclude == null || !config.Exclude.Contains(dep.Id)))
                                mergeConfigs.Add(new PackageMergeConfiguration {Id = dep.Id});
                        }
                    }

                    foreach (var mergeConfig in mergeConfigs)
                    {
                        logger.Info($"Merging {mergeConfig.Id}");
                        if (!packages.TryGetValue(mergeConfig.Id, out var victim))
                            throw new MergeAbortedException($"Package {mergeConfig.Id} not found");
                        
                        package.MergeContents(logger, victim, mergeConfig);
                        foreach (var p in packages.Values)
                            p.ReplaceDeps(mergeConfig.Id, package);
                        packages.Remove(mergeConfig.Id);
                    }
                }

                foreach (var package in packages.Values)
                {
                    logger.Info($"Saving {package.FileName}");
                    package.SaveToDirectory(output);
                }

            }
            catch (MergeAbortedException e)
            {
                logger.Error(e.Message);
                return false;
            }

            return true;
        }
        
        
    }
}