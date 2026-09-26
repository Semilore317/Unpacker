using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Unpacker.Services;

public class ArchiveInspector
{
    public string? DetectExecutable(
        string directory,
        string appName
    )
    {
        var files =
            Directory.GetFiles(
                directory,
                "*",
                SearchOption.AllDirectories
            );

        var candidates =
            new List<(
                string Path,
                bool IsElf,
                bool IsScript
            )>();

        foreach (var file in files)
        {
            if (new FileInfo(file).Length == 0)
            {
                continue;
            }

            bool isElf = IsElf(file);

            bool isScript =
                !isElf &&
                IsShellScript(file);

            if (isElf || isScript)
            {
                candidates.Add(
                    (
                        file,
                        isElf,
                        isScript
                    )
                );
            }
        }

        if (candidates.Count == 0)
        {
            return null;
        }

        // Prefer an ELF binary matching the application name.
        var simpleAppName =
            new string(
                appName
                    .Where(char.IsLetter)
                    .ToArray()
            )
            .ToLowerInvariant();

        var nameMatch =
            candidates
                .Where(c => c.IsElf)
                .Where(c =>
                {
                    var simpleName =
                        new string(
                            Path
                                .GetFileNameWithoutExtension(
                                    c.Path
                                )
                                .Where(char.IsLetter)
                                .ToArray()
                        )
                        .ToLowerInvariant();

                    return
                        simpleName.Contains(
                            simpleAppName
                        ) ||
                        simpleAppName.Contains(
                            simpleName
                        );
                })
                .OrderBy(
                    c =>
                        Path.GetFileName(
                            c.Path
                        ).Length
                )
                .FirstOrDefault();

        if (nameMatch.Path != null)
        {
            return nameMatch.Path;
        }

        // Otherwise prefer the largest ELF binary.
        var largestElf =
            candidates
                .Where(c => c.IsElf)
                .OrderByDescending(
                    c =>
                        new FileInfo(
                            c.Path
                        ).Length
                )
                .FirstOrDefault();

        if (largestElf.Path != null)
        {
            return largestElf.Path;
        }

        // Scripts are the last resort.
        // Prefer short launcher names such as "run" or "start".
        var script =
            candidates
                .Where(c => c.IsScript)
                .OrderBy(
                    c =>
                        Path.GetFileName(
                            c.Path
                        ).Length
                )
                .FirstOrDefault();

        return script.Path;
    }

    public bool IsElf(string path)
    {
        try
        {
            using var fs =
                File.OpenRead(path);

            var buffer = new byte[4];

            if (fs.Read(buffer, 0, 4) < 4)
            {
                return false;
            }

            return
                buffer[0] == 0x7F &&
                buffer[1] == 0x45 &&
                buffer[2] == 0x4C &&
                buffer[3] == 0x46;
        }
        catch
        {
            return false;
        }
    }

    public bool IsShellScript(string path)
    {
        try
        {
            using var fs =
                File.OpenRead(path);

            var buffer = new byte[2];

            if (fs.Read(buffer, 0, 2) < 2)
            {
                return false;
            }

            return
                buffer[0] == '#' &&
                buffer[1] == '!';
        }
        catch
        {
            return false;
        }
    }
}