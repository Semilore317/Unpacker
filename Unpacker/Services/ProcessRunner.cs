using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Unpacker.Services;

public sealed record ProcessResult
(
    int ExitCode,
    string StandardOutput,
    string StandardError
);

public sealed class ProcessRunner
{
    public async Task<ProcessResult> RunAsync(
        string fileName,
        IEnumerable<string> arguments
    )
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var arg in arguments)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start process `{fileName}`");

        var standardOutputTask = process.StandardOutput.ReadToEndAsync();
        var standardErrorTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        return new ProcessResult
        (
            process.ExitCode,
            await standardOutputTask,
            await standardErrorTask
        );
    }

    public async Task<ProcessResult> RunCheckedAsync(
        string filename,
        IEnumerable<string> arguments
    )
    {
        var result = await RunAsync(filename, arguments);

        if (result.ExitCode != 0) {
            throw new InvalidOperationException(
            $"Process '{filename}' exited with code {result.ExitCode}. \n" +
                $"StdOut: {result.StandardOutput} \n" +
                $"StdErr: {result.StandardError}");
        }

        return result;
    }
}
