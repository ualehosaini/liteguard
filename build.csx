#load "packages/simple-targets-csx.5.0.0/simple-targets.csx"

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using static SimpleTargets;

// version
var versionSuffix = Environment.GetEnvironmentVariable("VERSION_SUFFIX") ?? "";
var buildNumber = Environment.GetEnvironmentVariable("BUILD_NUMBER") ?? "000000";
var buildNumberSuffix = versionSuffix == "" ? "" : "-build" + buildNumber;
var version = File.ReadAllText("src/LiteGuard/Properties/CommonAssemblyInfo.cs")
    .Split(new[] { "AssemblyInformationalVersion(\"" }, 2, StringSplitOptions.RemoveEmptyEntries)[1]
    .Split('\"').First() + versionSuffix + buildNumberSuffix;

// locations
var solution = "./LiteGuard.sln";
var logs = "./artifacts/logs";
var msBuild = $"{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)}/MSBuild/14.0/Bin/msbuild.exe";
var nuspecs = new[] { "./src/LiteGuard.nuspec", "./src/LiteGuard.Source.nuspec", };
var output = "./artifacts/output";
var nuget = "./.nuget/NuGet.exe";
var xunit = "packages/xunit.runners.1.9.2/tools/xunit.console.clr4.exe";
var acceptanceTests = new[]
    {
        Path.GetFullPath("tests/LiteGuard.Test.Acceptance.net35/bin/Release/LiteGuard.Test.Acceptance.net35.dll"),
        Path.GetFullPath("tests/LiteGuard.Test.Acceptance.net45/bin/Release/LiteGuard.Test.Acceptance.net45.dll"),
    };

// targets
var targets = new TargetDictionary();

targets.Add("default", DependsOn("pack", "accept"));

targets.Add("logs", () => Directory.CreateDirectory(logs));

targets.Add(
    "build",
    DependsOn("logs"),
    () => Cmd(
        msBuild,
        $"{solution} /p:Configuration=Release /nologo /m /v:m /nr:false " +
            $"/fl /flp:LogFile={logs}/msbuild.log;Verbosity=Detailed;PerformanceSummary"));

targets.Add("output", () => Directory.CreateDirectory(output));

targets.Add(
    "src",
    () =>
    {
        // {platform}, {src}
        var platforms = new Dictionary<string, string>
        {
            { "net35", "net35" },
            { "netstandard1.0", "" },
        };

        foreach (var platform in platforms)
        {
            var originalSource = File.ReadAllText($"src/LiteGuard.{platform.Value}/Guard.cs");
            var modifiedSource = originalSource
                .Replace("namespace LiteGuard", "namespace $rootnamespace$")
                .Replace("public static class", "internal static class");

            Directory.CreateDirectory($"src/contentFiles/cs/{platform.Key}");
            File.WriteAllText($"src/contentFiles/cs/{platform.Key}/Guard.cs.pp", modifiedSource);
        }
    });

targets.Add(
    "pack",
    DependsOn("build", "src", "output"),
    () =>
    {
        foreach (var nuspec in nuspecs)
        {
            Cmd(nuget, $"pack {nuspec} -Version {version} -OutputDirectory {output} -NoPackageAnalysis");
        }
    });

targets.Add(
    "accept",
    DependsOn("build"),
    () =>
    {
        foreach (var acceptanceTest in acceptanceTests)
        {
            Cmd(xunit, $"{acceptanceTest} /html {acceptanceTest}.TestResults.html /xml {acceptanceTest}.TestResults.xml");
        }
    });

Run(Args, targets);

// helper
public static void Cmd(string fileName, string args)
{
    using (var process = new Process())
    {
        process.StartInfo = new ProcessStartInfo { FileName = $"\"{fileName}\"", Arguments = args, UseShellExecute = false, };
        Console.WriteLine($"Running '{process.StartInfo.FileName} {process.StartInfo.Arguments}'...");
        process.Start();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"The command exited with code {process.ExitCode}.");
        }
    }
}
