A tool for merging nuget packages

Usage: 
`Numerge.exe <config> <directory_with_packages> <output_directory>`


Config example:

```json
{
  "Packages":
  [
    {
      "Id": "Avalonia",
      "MergeAll": true,
      "Exclude": ["Avalonia.Remote.Protocol"],
      "Merge": [
        {
          "Id": "Avalonia.Build.Tasks",
          "IgnoreMissingFrameworkBinaries": true,
          "DoNotMergeDependencies": true
        },
        {
          "Id": "Avalonia.DesktopRuntime",
          "IgnoreMissingFrameworkBinaries": true,
          "IgnoreMissingFrameworkDependencies": true
        }

      ]
    }
  ]
}

```


The functionality is currently limited to the needs of Avalonia project. PRs are welcome.

The list of known problems so far:
- `netstandard2.0` is assumed to be the only .NET Standard that fits all frameworks
- package parsing/saving is very naive


## Numerge.MSBuild
You can add the `Numerge.MSBuild` nuget package to your project to automatically apply MSBuild task.

`NumergeTask` also has the following properties:
- `NumergeConfigFile` - specifies the path to the config file (default: `numerge.config.json` in current project directory)
- `NumergeClearIntermediatePackages` - specifies if intermediate packages should be deleted (default: `true`) 