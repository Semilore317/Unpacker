using Unpacker.Services;

namespace Unpacker.Tests.Services;

public sealed class ArchiveInspectorTests
{
    private readonly ArchiveInspector _inspector = new();

    [Fact]
    public void DetectExecutable_PrefersElfMatchingAppName()
    {
        string tempDir = CreateTempDirectory();

        try
        {
            string matchingElf =
                Path.Combine(tempDir, "my-app");

            string unrelatedElf =
                Path.Combine(tempDir, "something-else");

            WriteElf(matchingElf, 16);
            WriteElf(unrelatedElf, 128);

            string? result =
                _inspector.DetectExecutable(
                    tempDir,
                    "my-app"
                );

            Assert.Equal(
                matchingElf,
                result
            );
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void DetectExecutable_UsesLargestElfAsFallback()
    {
        string tempDir = CreateTempDirectory();

        try
        {
            string smallElf =
                Path.Combine(tempDir, "small");

            string largeElf =
                Path.Combine(tempDir, "large");

            WriteElf(smallElf, 16);
            WriteElf(largeElf, 128);

            string? result =
                _inspector.DetectExecutable(
                    tempDir,
                    "unrelated-app"
                );

            Assert.Equal(
                largeElf,
                result
            );
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void DetectExecutable_PrefersElfOverScript()
    {
        string tempDir = CreateTempDirectory();

        try
        {
            string elfPath =
                Path.Combine(tempDir, "binary");

            string scriptPath =
                Path.Combine(tempDir, "run");

            WriteElf(elfPath, 16);

            File.WriteAllText(
                scriptPath,
                "#!/bin/sh\necho test\n"
            );

            string? result =
                _inspector.DetectExecutable(
                    tempDir,
                    "unrelated-app"
                );

            Assert.Equal(
                elfPath,
                result
            );
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void DetectExecutable_UsesShortestScriptWhenNoElfExists()
    {
        string tempDir = CreateTempDirectory();

        try
        {
            string shortScript =
                Path.Combine(tempDir, "run");

            string longScript =
                Path.Combine(
                    tempDir,
                    "start-application"
                );

            File.WriteAllText(
                shortScript,
                "#!/bin/sh\necho short\n"
            );

            File.WriteAllText(
                longScript,
                "#!/bin/sh\necho long\n"
            );

            string? result =
                _inspector.DetectExecutable(
                    tempDir,
                    "example"
                );

            Assert.Equal(
                shortScript,
                result
            );
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void DetectExecutable_NoCandidates_ReturnsNull()
    {
        string tempDir = CreateTempDirectory();

        try
        {
            File.WriteAllText(
                Path.Combine(tempDir, "README.txt"),
                "not executable"
            );

            string? result =
                _inspector.DetectExecutable(
                    tempDir,
                    "example"
                );

            Assert.Null(result);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void IsElf_DetectsElfMagic()
    {
        string tempDir = CreateTempDirectory();

        try
        {
            string elfPath =
                Path.Combine(tempDir, "test");

            WriteElf(elfPath, 16);

            Assert.True(
                _inspector.IsElf(elfPath)
            );
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void IsElf_RejectsNonElfFile()
    {
        string tempDir = CreateTempDirectory();

        try
        {
            string filePath =
                Path.Combine(tempDir, "test");

            File.WriteAllText(
                filePath,
                "hello"
            );

            Assert.False(
                _inspector.IsElf(filePath)
            );
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void IsShellScript_DetectsShebang()
    {
        string tempDir = CreateTempDirectory();

        try
        {
            string scriptPath =
                Path.Combine(tempDir, "run");

            File.WriteAllText(
                scriptPath,
                "#!/bin/sh\necho test\n"
            );

            Assert.True(
                _inspector.IsShellScript(
                    scriptPath
                )
            );
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    private static string CreateTempDirectory()
    {
        string path =
            Path.Combine(
                Path.GetTempPath(),
                "UnpackerTests_" + Guid.NewGuid()
            );

        Directory.CreateDirectory(path);

        return path;
    }

    private static void WriteElf(
        string path,
        int size
    )
    {
        var bytes =
            new byte[Math.Max(size, 4)];

        bytes[0] = 0x7F;
        bytes[1] = 0x45;
        bytes[2] = 0x4C;
        bytes[3] = 0x46;

        File.WriteAllBytes(
            path,
            bytes
        );
    }
}