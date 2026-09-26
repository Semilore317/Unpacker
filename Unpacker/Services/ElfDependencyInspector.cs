using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Unpacker.Services;

public sealed record ElfIdentity(
    string Class,
    string Machine
);

public sealed class ElfDependencyInspector
{
    private readonly ProcessRunner _processRunner;

    public ElfDependencyInspector(
        ProcessRunner processRunner
    )
    {
        _processRunner = processRunner;
    }

    public static IReadOnlyList<string> ParseNeededLibraries(
        string output
    )
    {
        var dependencies =
            new List<string>();

        var seen =
            new HashSet<string>(
                StringComparer.Ordinal
            );

        if (string.IsNullOrWhiteSpace(output))
        {
            return dependencies;
        }

        foreach (var line in output.Split('\n'))
        {
            if (
                !line.Contains(
                    "(NEEDED)",
                    StringComparison.Ordinal
                )
            )
            {
                continue;
            }

            int start = line.IndexOf('[');
            int end = line.IndexOf(']', start + 1);

            if (
                start < 0 ||
                end <= start + 1
            )
            {
                continue;
            }

            string soname =
                line[(start + 1)..end]
                    .Trim();

            if (
                soname.Length > 0 &&
                seen.Add(soname)
            )
            {
                dependencies.Add(soname);
            }
        }

        return dependencies;
    }

    public static ElfIdentity? ParseElfIdentity(
        string output
    )
    {
        string? elfClass = null;
        string? machine = null;

        foreach (var line in output.Split('\n'))
        {
            var trimmed =
                line.Trim();

            if (
                trimmed.StartsWith(
                    "Class:",
                    StringComparison.Ordinal
                )
            )
            {
                elfClass =
                    trimmed["Class:".Length..]
                        .Trim();
            }
            else if (
                trimmed.StartsWith(
                    "Machine:",
                    StringComparison.Ordinal
                )
            )
            {
                machine =
                    trimmed["Machine:".Length..]
                        .Trim();
            }
        }

        if (
            string.IsNullOrEmpty(elfClass) ||
            string.IsNullOrEmpty(machine)
        )
        {
            return null;
        }

        return new ElfIdentity(
            elfClass,
            machine
        );
    }

    public static IReadOnlyDictionary<string, IReadOnlyList<string>>
        ParseLdConfigMappings(
            string output
        )
    {
        var mappings =
            new Dictionary<string, List<string>>(
                StringComparer.Ordinal
            );

        if (string.IsNullOrWhiteSpace(output))
        {
            return new Dictionary<string, IReadOnlyList<string>>(
                StringComparer.Ordinal
            );
        }

        foreach (var line in output.Split('\n'))
        {
            int separator =
                line.IndexOf(
                    "=>",
                    StringComparison.Ordinal
                );

            if (separator < 0)
            {
                continue;
            }

            string descriptor =
                line[..separator]
                    .Trim();

            string path =
                line[(separator + 2)..]
                    .Trim();

            if (
                descriptor.Length == 0 ||
                path.Length == 0 ||
                !path.StartsWith(
                    "/",
                    StringComparison.Ordinal
                )
            )
            {
                continue;
            }

            int metadataStart =
                descriptor.IndexOf('(');

            string soname =
                (
                    metadataStart >= 0
                        ? descriptor[..metadataStart]
                        : descriptor
                )
                .Trim();

            if (soname.Length == 0)
            {
                continue;
            }

            if (
                !mappings.TryGetValue(
                    soname,
                    out var paths
                )
            )
            {
                paths =
                    new List<string>();

                mappings.Add(
                    soname,
                    paths
                );
            }

            // ldconfig may contain multiple paths
            // for the same SONAME.
            if (!paths.Contains(path))
            {
                paths.Add(path);
            }
        }

        var result =
            new Dictionary<string, IReadOnlyList<string>>(
                StringComparer.Ordinal
            );

        foreach (var entry in mappings)
        {
            result.Add(
                entry.Key,
                entry.Value.AsReadOnly()
            );
        }

        return result;
    }

    public async Task<IReadOnlyList<string>> InspectAsync(
        string binaryPath
    )
    {
        var headerResult =
            await _processRunner.RunCheckedAsync(
                "readelf",
                new[]
                {
                    "-h",
                    binaryPath
                }
            );

        var targetIdentity =
            ParseElfIdentity(
                headerResult.StandardOutput
            )
            ?? throw new InvalidOperationException(
                "Could not determine ELF architecture."
            );

        var dynamicResult =
            await _processRunner.RunCheckedAsync(
                "readelf",
                new[]
                {
                    "-d",
                    binaryPath
                }
            );

        var neededLibraries =
            ParseNeededLibraries(
                dynamicResult.StandardOutput
            );

        if (neededLibraries.Count == 0)
        {
            return Array.Empty<string>();
        }

        var ldConfigResult =
            await _processRunner.RunCheckedAsync(
                "ldconfig",
                new[]
                {
                    "-p"
                }
            );

        var mappings =
            ParseLdConfigMappings(
                ldConfigResult.StandardOutput
            );

        var resolvedPaths =
            new List<string>();

        var seen =
            new HashSet<string>(
                StringComparer.Ordinal
            );

        foreach (var soname in neededLibraries)
        {
            if (
                !mappings.TryGetValue(
                    soname,
                    out var paths
                )
            )
            {
                continue;
            }

            foreach (var path in paths)
            {
                var candidateHeaderResult =
                    await _processRunner.RunAsync(
                        "readelf",
                        new[]
                        {
                            "-h",
                            path
                        }
                    );

                if (candidateHeaderResult.ExitCode != 0)
                {
                    continue;
                }

                var candidateIdentity =
                    ParseElfIdentity(
                        candidateHeaderResult.StandardOutput
                    );

                if (candidateIdentity != targetIdentity)
                {
                    continue;
                }

                if (seen.Add(path))
                {
                    resolvedPaths.Add(path);
                }
            }
        }

        return resolvedPaths;
    }
}