using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using SimpleExec;
using static Bullseye.Targets;
using static SimpleExec.Command;

internal class Program
{
    public static Task Main(string[] args)
    {
        Target("default", DependsOn("pack", "test"));

        Target("build", () => RunAsync("dotnet", "build --configuration Release /nologo"));

        Target(
            "pack",
            DependsOn("build"),
            ForEach("LiteGuard.nuspec", "LiteGuard.Source.nuspec"),
            async nuspec =>
            {
                Environment.SetEnvironmentVariable("NUSPEC_FILE", nuspec, EnvironmentVariableTarget.Process);
                await RunAsync("dotnet", $"pack LiteGuard --configuration Release --no-build /nologo");
            });

        Target(
            "test",
            DependsOn("build"),
            () => RunAsync("dotnet", $"test --configuration Release --no-build /nologo"));

        return RunTargetsAndExitAsync(args, ex => ex is NonZeroExitCodeException);
    }
}
