using Cake.Common;
using Cake.Common.Diagnostics;
using Cake.Common.IO;
using Cake.Common.Tools.DotNet;
using Cake.Common.Tools.DotNet.Clean;
using Cake.Common.Tools.DotNet.MSBuild;
using Cake.Common.Tools.DotNet.Publish;
using Cake.Core;
using Cake.Frosting;
using Cake.Json;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;

namespace CakeBuild;

public static class Program
{
    public static int Main(string[] args)
    {
        return new CakeHost()
            .UseContext<BuildContext>()
            .Run(args);
    }
}

public class BuildContext : FrostingContext
{
    public List<string> ProjectNames =
    [
        "SwixyClaimChunk"
    ];

    public BuildContext(ICakeContext context) : base(context)
    {
        BuildConfiguration = context.Argument("configuration", "Release");
        SkipJsonValidation = context.Argument("skipJsonValidation", false);
        ContinueOnError = context.Argument("continueOnError", false);
    }

    public string BuildConfiguration { get; set; }
    public bool SkipJsonValidation { get; set; }
    public bool ContinueOnError { get; set; }
}

[TaskName("PerProject")]
public sealed class PerProjectTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        context.EnsureDirectoryExists("../Releases");
        context.CleanDirectory("../Releases");

        foreach (var projectName in context.ProjectNames)
        {
            context.Information("=== Processing project: {0} ===", projectName);

            try
            {
                if (!context.SkipJsonValidation)
                {
                    var jsonFiles = context.GetFiles($"../{projectName}/assets/**/*.json");
                    foreach (var file in jsonFiles)
                    {
                        try
                        {
                            var json = File.ReadAllText(file.FullPath);
                            JToken.Parse(json);
                        }
                        catch (JsonException ex)
                        {
                            throw new Exception($"Validation failed for JSON file in project {projectName}: {file.FullPath}{Environment.NewLine}{ex.Message}", ex);
                        }
                    }

                    context.Information("JSON validation passed for {0}", projectName);
                }
                else
                {
                    context.Information("Skipping JSON validation for {0}", projectName);
                }

                var csprojPath = $"../{projectName}/{projectName}.csproj";
                context.Information("Cleaning project {0}", projectName);
                context.DotNetClean(csprojPath, new DotNetCleanSettings
                {
                    Configuration = context.BuildConfiguration
                });

                context.Information("Publishing project {0}", projectName);
                context.DotNetPublish(csprojPath, new DotNetPublishSettings
                {
                    Configuration = context.BuildConfiguration,
                    MSBuildSettings = new DotNetMSBuildSettings()
                        .WithProperty("WarningLevel", "0")
                        .WithProperty("TreatWarningsAsErrors", "false")
                });

                var modInfoPath = $"../{projectName}/modinfo.json";
                if (!File.Exists(modInfoPath))
                {
                    throw new FileNotFoundException($"modinfo.json not found for project {projectName}", modInfoPath);
                }

                var modInfo = context.DeserializeJsonFromFile<ModInfo>(modInfoPath);
                var version = modInfo.Version;
                var name = modInfo.ModID;

                var releaseDir = $"../Releases/{name}";
                context.EnsureDirectoryExists(releaseDir);

                var flatPublishDir = $"../{projectName}/bin/{context.BuildConfiguration}/Mods/publish";
                var nestedPublishDir = $"../{projectName}/bin/{context.BuildConfiguration}/Mods/mod/publish";
                var publishDir = Directory.Exists(flatPublishDir) ? flatPublishDir : nestedPublishDir;
                var publishSource = $"{publishDir}/*";
                context.Information("Copying published files from {0} to {1}", publishSource, releaseDir);
                context.CopyFiles(publishSource, releaseDir);

                context.CopyDirectory($"../{projectName}/assets", $"{releaseDir}/assets");
                context.CopyFile($"../{projectName}/modinfo.json", $"{releaseDir}/modinfo.json");

                var iconPath = $"../{projectName}/modicon.png";
                if (File.Exists(iconPath))
                {
                    context.CopyFile(iconPath, $"{releaseDir}/modicon.png");
                }
                else
                {
                    context.Warning("modicon.png not found for project {0}, skipping icon copy.", projectName);
                }

                var zipName = $"../Releases/{name}_{version}.zip";
                context.Information("Zipping {0} -> {1}", releaseDir, zipName);
                context.Zip(releaseDir, zipName);

                context.Information("Project {0} processed successfully.\n", projectName);
            }
            catch (Exception ex)
            {
                context.Error("Error processing project {0}: {1}", projectName, ex.Message);
                if (!context.ContinueOnError)
                {
                    throw;
                }

                context.Warning("ContinueOnError is true — continuing to next project.");
            }
        }
    }
}

[TaskName("Default")]
[IsDependentOn(typeof(PerProjectTask))]
public class DefaultTask : FrostingTask;

public class ModInfo
{
    [JsonProperty("modid")]
    public string ModID { get; set; } = "";

    [JsonProperty("Version")]
    public string Version { get; set; } = "";
}
