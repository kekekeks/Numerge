using System;
using System.IO;

namespace Numerge.Console
{
    class Program
    {
        static int Main(string[] args)
        {
            if (args.Length != 3)
            {
                System.Console.Error.WriteLine("Usage: <config> <inputdir> <outputdir>");
                return 1;
            }

            var configPath = args[0];
            var inputDir = args[1];
            var outputDir = args[2];
            var mergeConfig = MergeConfiguration.LoadFile(configPath);
            if (Directory.Exists(outputDir))
                Directory.Delete(outputDir, true);
            return NugetPackageMerger.Merge(inputDir, outputDir, mergeConfig, new NumergeConsoleLogger()) ? 0 : 2;
        }
    }
}