using Cake.Common;
using Cake.Common.Diagnostics;
using Cake.Common.IO;
using Cake.Common.Tools.DotNet;
using Cake.Common.Tools.DotNet.Build;
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
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

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
    /// <summary>All Swixy mods use analyze→split packaging.</summary>
    public List<string> ProjectNames { get; } =
    [
        "SwixyClaimChunk",
        "SwixySkyBlock",
        "SwixyQuestBook",
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

        foreach (var projectName in context.ProjectNames)
        {
            context.Information("=== {0} (analyze client/server) ===", projectName);
            try
            {
                ValidateJsonAssets(context, projectName);
                PackageAnalyzedClientServer(context, projectName);
                context.Information("OK: {0}\n", projectName);
            }
            catch (Exception ex)
            {
                context.Error("Error {0}: {1}", projectName, ex.Message);
                if (!context.ContinueOnError)
                    throw;
            }
        }
    }

    static void PackageAnalyzedClientServer(BuildContext context, string projectName)
    {
        var projectRoot = Path.GetFullPath($"../{projectName}");
        if (!Directory.Exists(projectRoot))
            throw new DirectoryNotFoundException(projectRoot);

        var analysis = SourceSideAnalyzer.Analyze(projectRoot, projectName);
        context.Information(
            "  Shared={0}  Server={1}  Client={2}  Both={3}  Skip={4}",
            analysis.Shared.Count, analysis.ServerOnly.Count, analysis.ClientOnly.Count,
            analysis.BothSides.Count, analysis.Skipped.Count);

        foreach (var s in analysis.Skipped)
            context.Warning("  skip {0} ({1})", s.RelativePath, s.Reason);

        // Server compile = Shared + ServerOnly + BothSides (+ pulled Client types)
        var serverSources = analysis.Shared
            .Concat(analysis.ServerOnly)
            .Concat(analysis.BothSides)
            .Concat(analysis.ServerExtraFromClient)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Client compile = Shared + ClientOnly + BothSides
        var clientSources = analysis.Shared
            .Concat(analysis.ClientOnly)
            .Concat(analysis.BothSides)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (serverSources.Count == 0 || clientSources.Count == 0)
            throw new Exception($"{projectName}: empty server/client source set after analysis.");

        var reportDir = Path.Combine(projectRoot, "obj", "cake-split");
        Directory.CreateDirectory(reportDir);
        var reportPath = Path.Combine(reportDir, "classification.txt");
        var reportLines = analysis.Shared.Select(f => "SHARED   " + f)
            .Concat(analysis.ServerOnly.Select(f => "SERVER   " + f))
            .Concat(analysis.ClientOnly.Select(f => "CLIENT   " + f))
            .Concat(analysis.BothSides.Select(f => "BOTH     " + f))
            .Concat(analysis.ServerExtraFromClient.Select(f => "SRV+CLI  " + f))
            .ToList();
        if (analysis.MirrorFullSources)
            reportLines.Insert(0, "MODE     MirrorFull (partial ModSystem — full sources in each side DLL)");
        File.WriteAllLines(reportPath, reportLines);
        context.Information("  report: {0}", reportPath);
        if (analysis.MirrorFullSources)
            context.Warning("  MirrorFull: partial Mod spans Server+Client — each side DLL contains full sources.");

        var vsPath = ResolveVintageStoryPath();
        var splitRoot = Path.Combine(projectRoot, "obj", "cake-split", context.BuildConfiguration);
        ResetDir(context, splitRoot);

        var asmBase = analysis.AssemblyNameBase;
        var sharedName = asmBase + ".Shared";
        var serverName = asmBase + ".Server";
        var clientName = asmBase + ".Client";

        var sharedProj = Path.Combine(splitRoot, "Generated.Shared.csproj");
        var serverProj = Path.Combine(splitRoot, "Generated.Server.csproj");
        var clientProj = Path.Combine(splitRoot, "Generated.Client.csproj");
        var refs = analysis.References;

        // Clean split only when Shared is non-empty AND we are not mirroring full sources.
        var useSharedDll = analysis.Shared.Count > 0 && !analysis.MirrorFullSources;

        if (useSharedDll)
        {
            WriteGeneratedCsproj(
                sharedProj, sharedName, projectRoot, analysis.Shared, vsPath,
                refs, clientLibs: false, projectRefs: [],
                globalUsings: analysis.SharedGlobalUsings, outSub: "shared");
            context.Information("  build Shared…");
            DotNetBuild(context, sharedProj, vsPath);

            var serverOnlyPlusBoth = analysis.ServerOnly
                .Concat(analysis.BothSides)
                .Concat(analysis.ServerExtraFromClient)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var clientOnlyPlusBoth = analysis.ClientOnly
                .Concat(analysis.BothSides)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                .ToList();

            WriteGeneratedCsproj(
                serverProj, serverName, projectRoot, serverOnlyPlusBoth, vsPath,
                refs, clientLibs: analysis.ServerNeedsClientLibs, projectRefs: [sharedProj],
                globalUsings: analysis.ServerGlobalUsings, outSub: "server");
            WriteGeneratedCsproj(
                clientProj, clientName, projectRoot, clientOnlyPlusBoth, vsPath,
                refs, clientLibs: true, projectRefs: [sharedProj],
                globalUsings: analysis.ClientGlobalUsings, outSub: "client");
        }
        else
        {
            // Self-contained side DLLs (MirrorFull or no shared files)
            WriteGeneratedCsproj(
                serverProj, serverName, projectRoot, serverSources, vsPath,
                refs, clientLibs: analysis.ServerNeedsClientLibs || analysis.MirrorFullSources,
                projectRefs: [],
                globalUsings: analysis.ServerGlobalUsings, outSub: "server");
            WriteGeneratedCsproj(
                clientProj, clientName, projectRoot, clientSources, vsPath,
                refs, clientLibs: true, projectRefs: [],
                globalUsings: analysis.ClientGlobalUsings, outSub: "client");
        }

        context.Information("  build Server…");
        DotNetBuild(context, serverProj, vsPath);
        context.Information("  build Client…");
        DotNetBuild(context, clientProj, vsPath);

        var sharedDll = Path.Combine(splitRoot, "out", "shared", sharedName + ".dll");
        var serverDll = Path.Combine(splitRoot, "out", "server", serverName + ".dll");
        var clientDll = Path.Combine(splitRoot, "out", "client", clientName + ".dll");
        RequireFile(serverDll);
        RequireFile(clientDll);
        if (useSharedDll)
            RequireFile(sharedDll);

        var modInfo = ReadModInfo(Path.Combine(projectRoot, "modinfo.json"));
        var serverModInfoSrc = FirstExisting(
            Path.Combine(projectRoot, "modinfo.server.json"),
            Path.Combine(projectRoot, "modinfo.json"));
        var clientModInfoSrc = FirstExisting(
            Path.Combine(projectRoot, "modinfo.client.json"),
            Path.Combine(projectRoot, "modinfo.json"));

        // ── SERVER package ──
        var serverDir = $"../Releases/{modInfo.ModID}_server";
        ResetDir(context, serverDir);
        File.Copy(serverDll, Path.Combine(serverDir, Path.GetFileName(serverDll)), true);
        CopyPdb(serverDll, serverDir);
        if (useSharedDll)
        {
            File.Copy(sharedDll, Path.Combine(serverDir, Path.GetFileName(sharedDll)), true);
            CopyPdb(sharedDll, serverDir);
        }

        // Project-specific server extras
        CopyIfExists(Path.Combine(projectRoot, "Data", "quests"),
            Path.Combine(serverDir, "swixyquestbook", "quests"), context);
        // SkyBlock worldconfig is world setup — useful on server
        CopyFileIfExists(Path.Combine(projectRoot, "worldconfig.json"),
            Path.Combine(serverDir, "worldconfig.json"));
        // Server rarely needs textures; skip assets unless lang-only wanted.
        // Claim/Sky lang still helps server log messages:
        CopyLangAssets(projectRoot, serverDir, context);

        WriteSideModInfo(serverModInfoSrc, Path.Combine(serverDir, "modinfo.json"), "Server", " [SERVER]");
        CopyFileIfExists(Path.Combine(projectRoot, "modicon.png"), Path.Combine(serverDir, "modicon.png"));

        var serverZip = $"../Releases/{modInfo.ModID}_server_{modInfo.Version}.zip";
        ZipFolder(context, serverDir, serverZip);
        context.Information("  SERVER: {0}", string.Join(", ", DllNames(serverDir)));
        context.Information("  → {0}", Path.GetFullPath(serverZip));

        // ── CLIENT package ──
        var clientDir = $"../Releases/{modInfo.ModID}_client";
        ResetDir(context, clientDir);
        File.Copy(clientDll, Path.Combine(clientDir, Path.GetFileName(clientDll)), true);
        CopyPdb(clientDll, clientDir);
        if (useSharedDll)
        {
            File.Copy(sharedDll, Path.Combine(clientDir, Path.GetFileName(sharedDll)), true);
            CopyPdb(sharedDll, clientDir);
        }

        CopyIfExists(Path.Combine(projectRoot, "assets"), Path.Combine(clientDir, "assets"), context);
        WriteSideModInfo(clientModInfoSrc, Path.Combine(clientDir, "modinfo.json"), "Client", " [CLIENT]");
        CopyFileIfExists(Path.Combine(projectRoot, "modicon.png"), Path.Combine(clientDir, "modicon.png"));

        var clientZip = $"../Releases/{modInfo.ModID}_client_{modInfo.Version}.zip";
        ZipFolder(context, clientDir, clientZip);
        context.Information("  CLIENT: {0}", string.Join(", ", DllNames(clientDir)));
        context.Information("  → {0}", Path.GetFullPath(clientZip));

        File.WriteAllText(
            $"../Releases/{modInfo.ModID}_SPLIT_README.txt",
            BuildReadme(modInfo, analysis, useSharedDll),
            Encoding.UTF8);
    }

    static string BuildReadme(ModInfo m, SourceAnalysis a, bool shared) =>
        $"""
        {m.Name} ({m.ModID}) v{m.Version} — auto-split by CakeBuild
        ============================================================
        Shared files : {a.Shared.Count}
        Server-only  : {a.ServerOnly.Count}
        Client-only  : {a.ClientOnly.Count}
        Both sides   : {a.BothSides.Count}
        Server+Client pulls: {a.ServerExtraFromClient.Count}

        SERVER zip: {m.ModID}_server_{m.Version}.zip
          → dedicated server Mods/
        CLIENT zip: {m.ModID}_client_{m.Version}.zip
          → players / public

        Same modid. Do not install both zips in one Mods folder.
        Classification: {a.ProjectName}/obj/cake-split/classification.txt
        """;

    // ── csproj generation ───────────────────────────────────────────────

    static void WriteGeneratedCsproj(
        string csprojPath,
        string assemblyName,
        string projectRoot,
        IReadOnlyList<string> relativeSources,
        string vintageStoryPath,
        ProjectRefSet refs,
        bool clientLibs,
        IReadOnlyList<string> projectRefs,
        string globalUsings,
        string outSub)
    {
        var dir = Path.GetDirectoryName(csprojPath)!;
        Directory.CreateDirectory(dir);

        var usingsPath = Path.Combine(dir, assemblyName + ".GlobalUsings.g.cs");
        File.WriteAllText(usingsPath, globalUsings ?? "", Encoding.UTF8);

        var outPath = Path.Combine(dir, "out", outSub) + Path.DirectorySeparatorChar;
        var sb = new StringBuilder();
        sb.AppendLine("<Project Sdk=\"Microsoft.NET.Sdk\">");
        sb.AppendLine("  <PropertyGroup>");
        sb.AppendLine("    <TargetFramework>net10.0</TargetFramework>");
        sb.AppendLine("    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>");
        sb.AppendLine($"    <AssemblyName>{Xml(assemblyName)}</AssemblyName>");
        sb.AppendLine($"    <RootNamespace>{Xml(refs.RootNamespace)}</RootNamespace>");
        sb.AppendLine("    <Nullable>enable</Nullable>");
        sb.AppendLine("    <ImplicitUsings>enable</ImplicitUsings>");
        sb.AppendLine("    <LangVersion>latest</LangVersion>");
        sb.AppendLine("    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>");
        sb.AppendLine($"    <OutputPath>{Xml(outPath)}</OutputPath>");
        sb.AppendLine("    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>");
        sb.AppendLine("  </PropertyGroup>");
        sb.AppendLine("  <ItemGroup>");
        sb.AppendLine(Ref("protobuf-net", Path.Combine(vintageStoryPath, "Lib", "protobuf-net.dll")));
        sb.AppendLine(Ref("VintagestoryAPI", Path.Combine(vintageStoryPath, "VintagestoryAPI.dll")));
        sb.AppendLine(Ref("VintagestoryLib", Path.Combine(vintageStoryPath, "VintagestoryLib.dll")));
        if (refs.VSSurvivalMod)
            sb.AppendLine(Ref("VSSurvivalMod", Path.Combine(vintageStoryPath, "Mods", "VSSurvivalMod.dll")));
        if (refs.VSEssentials)
            sb.AppendLine(Ref("VSEssentials", Path.Combine(vintageStoryPath, "Mods", "VSEssentials.dll")));
        if (clientLibs || refs.AlwaysCairo)
        {
            sb.AppendLine(Ref("cairo-sharp", Path.Combine(vintageStoryPath, "Lib", "cairo-sharp.dll")));
            if (refs.SkiaSharp)
                sb.AppendLine(Ref("SkiaSharp", Path.Combine(vintageStoryPath, "Lib", "SkiaSharp.dll")));
        }
        sb.AppendLine("  </ItemGroup>");

        if (projectRefs.Count > 0)
        {
            sb.AppendLine("  <ItemGroup>");
            foreach (var pref in projectRefs)
            {
                sb.AppendLine($"    <ProjectReference Include=\"{Xml(pref)}\"><Private>true</Private></ProjectReference>");
            }
            sb.AppendLine("  </ItemGroup>");
        }

        sb.AppendLine("  <ItemGroup>");
        if (!string.IsNullOrWhiteSpace(globalUsings))
            sb.AppendLine($"    <Compile Include=\"{Xml(usingsPath)}\" />");
        foreach (var rel in relativeSources)
        {
            var full = Path.GetFullPath(Path.Combine(projectRoot, rel));
            sb.AppendLine($"    <Compile Include=\"{Xml(full)}\" Link=\"{Xml(rel.Replace('\\', '/'))}\" />");
        }
        sb.AppendLine("  </ItemGroup>");
        sb.AppendLine("</Project>");
        File.WriteAllText(csprojPath, sb.ToString(), Encoding.UTF8);
    }

    static string Ref(string name, string hint) =>
        $"    <Reference Include=\"{name}\"><HintPath>{Xml(hint)}</HintPath><Private>False</Private></Reference>";

    static string Xml(string s) =>
        s.Replace("&", "&amp;").Replace("\"", "&quot;").Replace("<", "&lt;").Replace(">", "&gt;");

    static void DotNetBuild(BuildContext context, string proj, string vsPath)
    {
        context.DotNetBuild(proj, new DotNetBuildSettings
        {
            Configuration = context.BuildConfiguration,
            MSBuildSettings = new DotNetMSBuildSettings()
                .WithProperty("WarningLevel", "0")
                .WithProperty("TreatWarningsAsErrors", "false")
                .WithProperty("VINTAGE_STORY", vsPath)
        });
    }

    // ── helpers ─────────────────────────────────────────────────────────

    static void ValidateJsonAssets(BuildContext context, string projectName)
    {
        if (context.SkipJsonValidation) return;
        foreach (var file in context.GetFiles($"../{projectName}/assets/**/*.json"))
        {
            try { JToken.Parse(File.ReadAllText(file.FullPath)); }
            catch (JsonException ex)
            {
                throw new Exception($"JSON fail {file.FullPath}: {ex.Message}", ex);
            }
        }
    }

    static ModInfo ReadModInfo(string path)
    {
        var json = JObject.Parse(File.ReadAllText(path));
        // VS samples use both "version" and "Version"
        var version = json["version"]?.ToString()
            ?? json["Version"]?.ToString()
            ?? "";
        return new ModInfo
        {
            ModID = json["modid"]?.ToString() ?? json["modId"]?.ToString() ?? "",
            Version = version,
            Name = json["name"]?.ToString() ?? ""
        };
    }

    static void WriteSideModInfo(string sourcePath, string destPath, string side, string suffix)
    {
        var json = JObject.Parse(File.ReadAllText(sourcePath));
        // Normalize version key to lowercase for VS
        var ver = json["version"] ?? json["Version"];
        if (ver != null)
        {
            json["version"] = ver;
            json.Remove("Version");
        }
        json["side"] = side;
        json["requiredOnClient"] = true;
        json["requiredOnServer"] = true;
        var desc = json["description"]?.ToString() ?? "";
        if (!desc.Contains(suffix, StringComparison.Ordinal))
            json["description"] = (desc + suffix).Trim();
        File.WriteAllText(destPath, json.ToString(Formatting.Indented) + Environment.NewLine, Encoding.UTF8);
    }

    static string ResolveVintageStoryPath()
    {
        var env = Environment.GetEnvironmentVariable("VINTAGE_STORY");
        if (!string.IsNullOrWhiteSpace(env) && Directory.Exists(env))
            return Path.GetFullPath(env);

        var userProps = Path.GetFullPath("../Directory.Build.props.user");
        if (File.Exists(userProps))
        {
            var m = Regex.Match(File.ReadAllText(userProps),
                @"<VINTAGE_STORY>\s*([^<]+)\s*</VINTAGE_STORY>", RegexOptions.IgnoreCase);
            if (m.Success && Directory.Exists(m.Groups[1].Value.Trim()))
                return Path.GetFullPath(m.Groups[1].Value.Trim());
        }
        throw new Exception("VINTAGE_STORY is not set.");
    }

    static void ResetDir(BuildContext context, string dir)
    {
        if (Directory.Exists(dir))
            context.DeleteDirectory(dir, new DeleteDirectorySettings { Recursive = true, Force = true });
        context.EnsureDirectoryExists(dir);
    }

    static void CopyIfExists(string src, string dest, BuildContext context)
    {
        if (!Directory.Exists(src)) return;
        context.EnsureDirectoryExists(dest);
        context.CopyDirectory(src, dest);
    }

    static void CopyLangAssets(string projectRoot, string destRoot, BuildContext context)
    {
        var assets = Path.Combine(projectRoot, "assets");
        if (!Directory.Exists(assets)) return;
        foreach (var domain in Directory.GetDirectories(assets))
        {
            var lang = Path.Combine(domain, "lang");
            if (!Directory.Exists(lang)) continue;
            var dest = Path.Combine(destRoot, "assets", Path.GetFileName(domain)!, "lang");
            context.EnsureDirectoryExists(dest);
            context.CopyDirectory(lang, dest);
        }
    }

    static void CopyFileIfExists(string src, string dest)
    {
        if (File.Exists(src)) File.Copy(src, dest, true);
    }

    static void CopyPdb(string dll, string destDir)
    {
        var pdb = Path.ChangeExtension(dll, ".pdb");
        if (File.Exists(pdb))
            File.Copy(pdb, Path.Combine(destDir, Path.GetFileName(pdb)), true);
    }

    static void RequireFile(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("Missing build output", path);
    }

    static string FirstExisting(params string[] paths) =>
        paths.FirstOrDefault(File.Exists)
        ?? throw new FileNotFoundException(string.Join(" | ", paths));

    static IEnumerable<string> DllNames(string dir) =>
        Directory.GetFiles(dir, "*.dll").Select(Path.GetFileName)!;

    static void ZipFolder(BuildContext context, string releaseDir, string zipName)
    {
        if (File.Exists(zipName)) File.Delete(zipName);
        ZipFile.CreateFromDirectory(Path.GetFullPath(releaseDir), Path.GetFullPath(zipName),
            CompressionLevel.Optimal, includeBaseDirectory: false);
    }
}

// ═══════════════════════════════════════════════════════════════════════
// Source analysis
// ═══════════════════════════════════════════════════════════════════════

public static class SourceSideAnalyzer
{
    public static SourceAnalysis Analyze(string projectRoot, string projectName)
    {
        var result = new SourceAnalysis
        {
            ProjectName = projectName,
            AssemblyNameBase = projectName, // SwixyQuestBook, SwixyClaimChunk, …
            References = ProjectRefSet.For(projectName),
        };

        var root = Path.GetFullPath(projectRoot);
        var all = new List<(string Rel, string Full)>();

        foreach (var full in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            if (full.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                || full.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                continue;

            var rel = Path.GetRelativePath(root, full).Replace('\\', '/');
            if (IsSkipped(rel, out var reason))
            {
                result.Skipped.Add((rel, reason));
                continue;
            }
            all.Add((rel, full));
        }

        foreach (var (rel, full) in all)
        {
            var side = Classify(rel, projectName, full);
            switch (side)
            {
                case SourceSide.Shared: result.Shared.Add(rel); break;
                case SourceSide.Server: result.ServerOnly.Add(rel); break;
                case SourceSide.Client: result.ClientOnly.Add(rel); break;
                case SourceSide.Both: result.BothSides.Add(rel); break;
            }
        }

        // Root / Both-side fields may reference Client-only types (e.g. IslandGeneratorLabelRenderer).
        result.ServerExtraFromClient.AddRange(FindClientTypesNeededByBoth(root, result));

        // MirrorFull only if the SAME partial Mod class name appears on both Server and Client.
        // Separate ServerMod / ClientMod classes (after refactor) must NOT trigger mirror.
        var serverPartialNames = result.ServerOnly
            .Select(rel => GetPartialModClassName(root, rel))
            .Where(n => n != null)
            .ToHashSet(StringComparer.Ordinal);
        var clientPartialNames = result.ClientOnly
            .Select(rel => GetPartialModClassName(root, rel))
            .Where(n => n != null)
            .ToHashSet(StringComparer.Ordinal);
        var bothPartialNames = result.BothSides
            .Select(rel => GetPartialModClassName(root, rel))
            .Where(n => n != null)
            .ToHashSet(StringComparer.Ordinal);
        var sharedPartialNames = result.Shared
            .Select(rel => GetPartialModClassName(root, rel))
            .Where(n => n != null)
            .ToHashSet(StringComparer.Ordinal);

        // Any partial Mod class that exists in Shared AND side-only is also unsafe for Shared.dll alone.
        foreach (var name in sharedPartialNames.ToList())
        {
            // Move Shared partial Mod files into BothSides
        }
        // If a partial name is in Server and Client, must mirror.
        bool samePartialSpansSides = serverPartialNames.Overlaps(clientPartialNames)
            || serverPartialNames.Overlaps(bothPartialNames) && clientPartialNames.Overlaps(bothPartialNames)
            || bothPartialNames.Any(n => serverPartialNames.Contains(n!) || clientPartialNames.Contains(n!));

        // Safer: intersection of all partial names seen on server-side files vs client-side files
        var serverSideNames = serverPartialNames.Concat(bothPartialNames).Concat(sharedPartialNames).ToHashSet(StringComparer.Ordinal);
        var clientSideNames = clientPartialNames.Concat(bothPartialNames).Concat(sharedPartialNames).ToHashSet(StringComparer.Ordinal);
        samePartialSpansSides = serverPartialNames.Overlaps(clientPartialNames)
            || serverPartialNames.Overlaps(clientSideNames) && clientPartialNames.Count > 0 && serverPartialNames.Any(n => clientSideNames.Contains(n!));
        // Clean rule: if any class name is partial on BOTH a Server/** file and a Client/** file → mirror
        samePartialSpansSides = serverPartialNames.Overlaps(clientPartialNames);

        if (samePartialSpansSides)
        {
            result.MirrorFullSources = true;
            var mirrored = result.Shared
                .Concat(result.ServerOnly)
                .Concat(result.ClientOnly)
                .Concat(result.BothSides)
                .Concat(result.ServerExtraFromClient)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                .ToList();
            result.Shared.Clear();
            result.ServerOnly.Clear();
            result.ClientOnly.Clear();
            result.BothSides.Clear();
            result.ServerExtraFromClient.Clear();
            result.BothSides.AddRange(mirrored);
            result.Skipped.Add(("*", "MirrorFull: same partial Mod class spans Server+Client folders"));
        }
        else
        {
            // Partial Mod pieces in Shared (Core/*) must ride with both side DLLs, not Shared.dll.
            var sharedPartials = result.Shared.Where(rel => GetPartialModClassName(root, rel) != null).ToList();
            foreach (var rel in sharedPartials)
            {
                result.Shared.Remove(rel);
                result.BothSides.Add(rel);
            }
        }

        // If Content/ GUI is on server side, need cairo
        result.ServerNeedsClientLibs = result.MirrorFullSources
            || result.BothSides.Any(f =>
                f.StartsWith("Content/", StringComparison.OrdinalIgnoreCase)
                || f.StartsWith("Gui/", StringComparison.OrdinalIgnoreCase))
            || result.ServerExtraFromClient.Count > 0
            || result.References.AlwaysCairo;

        if (projectName.Equals("SwixyQuestBook", StringComparison.OrdinalIgnoreCase))
        {
            result.SharedGlobalUsings = QuestbookSharedUsings;
            result.ServerGlobalUsings = QuestbookSharedUsings;
            result.ClientGlobalUsings = QuestbookClientUsings;
        }
        else
        {
            result.SharedGlobalUsings = "";
            result.ServerGlobalUsings = "";
            result.ClientGlobalUsings = "";
        }

        return result;
    }

    static bool IsPartialModFile(string projectRoot, string rel) =>
        GetPartialModClassName(projectRoot, rel) != null;

    static string? GetPartialModClassName(string projectRoot, string rel)
    {
        var path = Path.Combine(projectRoot, rel.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path)) return null;
        var head = ReadFileHead(path, 8000);
        var m = Regex.Match(head, @"\bpartial\s+class\s+(\w*Mod)\b");
        return m.Success ? m.Groups[1].Value : null;
    }

    static bool IsSkipped(string rel, out string reason)
    {
        if (rel.StartsWith("tools/", StringComparison.OrdinalIgnoreCase))
        {
            reason = "tools";
            return true;
        }
        var file = Path.GetFileName(rel);
        if (file.Equals("GlobalUsings.cs", StringComparison.OrdinalIgnoreCase)
            || file.EndsWith("GlobalUsings.cs", StringComparison.OrdinalIgnoreCase)
            || file.EndsWith("GlobalUsings.g.cs", StringComparison.OrdinalIgnoreCase)
            || rel.Contains("/Compatibility/", StringComparison.OrdinalIgnoreCase))
        {
            reason = "global-usings";
            return true;
        }
        reason = "";
        return false;
    }

    /// <summary>
    /// Folder conventions + content scan for partial ModSystem parts.
    /// IMPORTANT: any file with <c>partial class *Mod</c> must NOT go into Shared.dll alone —
    /// it is compiled into BOTH Server and Client assemblies (Both).
    /// </summary>
    public static SourceSide Classify(string rel, string projectName, string? fullPath = null)
    {
        var n = rel.Replace('\\', '/');

        // Partial ModSystem pieces always stay with both side assemblies (not Shared).
        if (!string.IsNullOrEmpty(fullPath) && File.Exists(fullPath))
        {
            var head = ReadFileHead(fullPath, 8000);
            if (Regex.IsMatch(head, @"\bpartial\s+class\s+\w*Mod\b"))
            {
                if (n.StartsWith("Server/", StringComparison.OrdinalIgnoreCase))
                    return SourceSide.Server;
                if (n.StartsWith("Client/", StringComparison.OrdinalIgnoreCase))
                    return SourceSide.Client;
                // Root Mod.cs, Core/* partials → both side DLLs
                return SourceSide.Both;
            }
        }

        if (n.StartsWith("Server/", StringComparison.OrdinalIgnoreCase))
            return SourceSide.Server;

        if (n.StartsWith("Client/", StringComparison.OrdinalIgnoreCase)
            || n.StartsWith("Gui/", StringComparison.OrdinalIgnoreCase))
            return SourceSide.Client;

        if (n.Equals("QuestbookMod.cs", StringComparison.OrdinalIgnoreCase))
            return SourceSide.Client;

        // Root *Mod.cs shell
        if (Regex.IsMatch(Path.GetFileName(n), @"^.+Mod\.cs$", RegexOptions.IgnoreCase)
            && !n.Contains('/'))
            return SourceSide.Both;

        // GUI cells/dialogs — client package only (after ServerMod/ClientMod split)
        if (n.StartsWith("Content/", StringComparison.OrdinalIgnoreCase))
            return SourceSide.Client;

        if (n.StartsWith("Domain/", StringComparison.OrdinalIgnoreCase)
            || n.StartsWith("Network/", StringComparison.OrdinalIgnoreCase)
            || n.StartsWith("Core/", StringComparison.OrdinalIgnoreCase)
            || n.StartsWith("Net/", StringComparison.OrdinalIgnoreCase)
            || n.StartsWith("Util/Inventory/", StringComparison.OrdinalIgnoreCase)
            || n.Equals("Util/Items/QuestbookItemCodeHelper.cs", StringComparison.OrdinalIgnoreCase))
            return SourceSide.Shared;

        if (n.StartsWith("Util/Audio/", StringComparison.OrdinalIgnoreCase)
            || n.StartsWith("Util/Localization/", StringComparison.OrdinalIgnoreCase)
            || n.StartsWith("Util/Textures/", StringComparison.OrdinalIgnoreCase)
            || n.Equals("Util/Items/QuestbookItemDisplayHelper.cs", StringComparison.OrdinalIgnoreCase)
            || n.Equals("Util/Items/QuestbookItemIconHelper.cs", StringComparison.OrdinalIgnoreCase))
            return SourceSide.Client;

        return SourceSide.Shared;
    }

    static string ReadFileHead(string path, int maxChars)
    {
        try
        {
            using var sr = new StreamReader(path);
            var buf = new char[maxChars];
            var read = sr.Read(buf, 0, maxChars);
            return new string(buf, 0, read);
        }
        catch
        {
            return "";
        }
    }

    /// <summary>
    /// If root Both-side files mention a type defined in a Client-only file, pull it into server compile.
    /// </summary>
    static List<string> FindClientTypesNeededByBoth(string projectRoot, SourceAnalysis result)
    {
        var pulls = new List<string>();
        if (result.BothSides.Count == 0 || result.ClientOnly.Count == 0)
            return pulls;

        var bothText = new StringBuilder();
        foreach (var rel in result.BothSides)
        {
            var path = Path.Combine(projectRoot, rel.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(path))
                bothText.AppendLine(File.ReadAllText(path));
        }
        var hay = bothText.ToString();

        foreach (var rel in result.ClientOnly)
        {
            var path = Path.Combine(projectRoot, rel.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path)) continue;
            var src = File.ReadAllText(path);
            // public sealed/class/interface Name
            foreach (Match m in Regex.Matches(src, @"\b(?:class|interface|struct|enum|record)\s+(\w+)"))
            {
                var typeName = m.Groups[1].Value;
                if (typeName.Length < 3) continue;
                // Field/usage in both-side sources
                if (Regex.IsMatch(hay, $@"\b{Regex.Escape(typeName)}\b"))
                {
                    pulls.Add(rel);
                    break;
                }
            }
        }

        return pulls.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    const string QuestbookSharedUsings =
        """
        global using QuestbookGoalObjective = SwixyQuestBook.Domain.Goals.QuestbookGoalObjective;
        global using QuestbookLocalizedText = SwixyQuestBook.Domain.Localization.QuestbookLocalizedText;
        global using QuestbookLocalizedTextJsonConverter = SwixyQuestBook.Domain.Localization.QuestbookLocalizedTextJsonConverter;
        global using QuestbookQuestItemData = SwixyQuestBook.Domain.Models.QuestbookQuestItemData;
        global using QuestbookQuestItemRequirement = SwixyQuestBook.Domain.Models.QuestbookQuestItemRequirement;
        global using QuestbookQuestNodeData = SwixyQuestBook.Domain.Models.QuestbookQuestNodeData;
        global using QuestbookQuestConnectionData = SwixyQuestBook.Domain.Models.QuestbookQuestConnectionData;
        global using QuestbookCategoryData = SwixyQuestBook.Domain.Models.QuestbookCategoryData;
        global using QuestbookQuestDatabase = SwixyQuestBook.Domain.Models.QuestbookQuestDatabase;
        global using QuestbookQuestManifest = SwixyQuestBook.Domain.Models.QuestbookQuestManifest;
        global using QuestbookQuestManifestEntry = SwixyQuestBook.Domain.Models.QuestbookQuestManifestEntry;
        global using QuestbookCompletedQuestEntry = SwixyQuestBook.Domain.Progress.QuestbookCompletedQuestEntry;
        global using QuestbookCraftProgressEntry = SwixyQuestBook.Domain.Progress.QuestbookCraftProgressEntry;
        global using QuestbookPlayerProgressData = SwixyQuestBook.Domain.Progress.QuestbookPlayerProgressData;
        global using QuestbookInventoryHelper = SwixyQuestBook.Util.Inventory.QuestbookInventoryHelper;
        global using QuestbookItemCodeHelper = SwixyQuestBook.Util.Items.QuestbookItemCodeHelper;
        """;

    const string QuestbookClientUsings =
        QuestbookSharedUsings +
        """

        global using QuestbookItemDisplayHelper = SwixyQuestBook.Util.Items.QuestbookItemDisplayHelper;
        global using QuestbookItemIconHelper = SwixyQuestBook.Util.Items.QuestbookItemIconHelper;
        global using QuestbookItemIconContext = SwixyQuestBook.Util.Items.QuestbookItemIconContext;
        global using QuestbookLang = SwixyQuestBook.Util.Localization.QuestbookLang;
        global using QuestbookSoundHelper = SwixyQuestBook.Util.Audio.QuestbookSoundHelper;
        global using QuestbookTextureHelper = SwixyQuestBook.Util.Textures.QuestbookTextureHelper;
        """;
}

public enum SourceSide { Shared, Server, Client, Both }

public sealed class SourceAnalysis
{
    public string ProjectName { get; set; } = "";
    public string AssemblyNameBase { get; set; } = "";
    public ProjectRefSet References { get; set; } = new();
    public List<string> Shared { get; } = [];
    public List<string> ServerOnly { get; } = [];
    public List<string> ClientOnly { get; } = [];
    public List<string> BothSides { get; } = [];
    public List<string> ServerExtraFromClient { get; } = [];
    public List<(string RelativePath, string Reason)> Skipped { get; } = [];
    public bool ServerNeedsClientLibs { get; set; }
    /// <summary>
    /// True when partial ModSystem cannot be split cleanly — each side DLL gets all sources.
    /// </summary>
    public bool MirrorFullSources { get; set; }
    public string SharedGlobalUsings { get; set; } = "";
    public string ServerGlobalUsings { get; set; } = "";
    public string ClientGlobalUsings { get; set; } = "";
}

public sealed class ProjectRefSet
{
    public string RootNamespace { get; set; } = "";
    public bool VSSurvivalMod { get; set; }
    public bool VSEssentials { get; set; }
    public bool AlwaysCairo { get; set; }
    public bool SkiaSharp { get; set; }

    public static ProjectRefSet For(string projectName) => projectName switch
    {
        "SwixyQuestBook" => new ProjectRefSet
        {
            RootNamespace = "SwixyQuestBook",
            VSSurvivalMod = true,
            SkiaSharp = true,
            AlwaysCairo = false,
        },
        "SwixyClaimChunk" => new ProjectRefSet
        {
            RootNamespace = "SwixyClaimChunk",
            VSEssentials = true,
            AlwaysCairo = true, // Content GUI pulled into both sides
        },
        "SwixySkyBlock" => new ProjectRefSet
        {
            RootNamespace = "SwixySkyBlock",
            VSSurvivalMod = true,
            VSEssentials = true,
            AlwaysCairo = true,
        },
        _ => new ProjectRefSet { RootNamespace = projectName },
    };
}

[TaskName("Default")]
[IsDependentOn(typeof(PerProjectTask))]
public class DefaultTask : FrostingTask;

public class ModInfo
{
    public string ModID { get; set; } = "";
    public string Version { get; set; } = "";
    public string Name { get; set; } = "";
}
