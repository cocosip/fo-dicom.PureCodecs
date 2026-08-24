using System.Diagnostics;

namespace FellowOakDicom.PureCodecs.Tests.TestSupport;

internal sealed record BoundedWorkerResult(
    int ExitCode,
    string StandardOutput,
    string StandardError,
    bool TimedOut);

internal static class BoundedWorkerProcess
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(120);

    public static BoundedWorkerResult Run(
        string workerAssemblyPath,
        IReadOnlyList<string> arguments,
        TimeSpan? timeout = null)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add(workerAssemblyPath);
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start HTJ2K worker process.");
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        var completed = process.WaitForExit((int)(timeout ?? DefaultTimeout).TotalMilliseconds);
        if (!completed)
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
        }

        Task.WaitAll(standardOutput, standardError);
        return new BoundedWorkerResult(
            completed ? process.ExitCode : -1,
            standardOutput.Result,
            standardError.Result,
            TimedOut: !completed);
    }
}
