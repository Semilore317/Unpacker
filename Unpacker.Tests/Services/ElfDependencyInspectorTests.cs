using Unpacker.Services;

namespace Unpacker.Tests.Services;

public sealed class ElfDependencyInspectorTests
{
    [Fact]
    public void ParseNeededLibraries_ExtractsNeededEntries()
    {
        const string output = """
            (NEEDED) Shared library: [libfoo.so.1]
            (NEEDED) Shared library: [libc.so.6]
            (RUNPATH) Library runpath: [/usr/lib]
            """;

        var result =
            ElfDependencyInspector.ParseNeededLibraries(
                output
            );

        Assert.Equal(
            new[]
            {
                "libfoo.so.1",
                "libc.so.6"
            },
            result
        );
    }

    [Fact]
    public void ParseNeededLibraries_IgnoresDuplicateEntries()
    {
        const string output = """
            (NEEDED) Shared library: [libfoo.so.1]
            (NEEDED) Shared library: [libfoo.so.1]
            """;

        var result =
            ElfDependencyInspector.ParseNeededLibraries(
                output
            );

        Assert.Single(result);

        Assert.Equal(
            "libfoo.so.1",
            result[0]
        );
    }

    [Fact]
    public void ParseNeededLibraries_IgnoresMalformedAndUnrelatedEntries()
    {
        const string output = """
            (SONAME) Library soname: [example]
            (NEEDED) Shared library: []
            (NEEDED) Shared library: libbroken.so
            random text
            """;

        var result =
            ElfDependencyInspector.ParseNeededLibraries(
                output
            );

        Assert.Empty(result);
    }

    [Fact]
    public void ParseNeededLibraries_EmptyOutput_ReturnsEmpty()
    {
        var result =
            ElfDependencyInspector.ParseNeededLibraries(
                ""
            );

        Assert.Empty(result);
    }

    [Fact]
    public void ParseLdConfigMappings_ParsesInstalledLibraryPaths()
    {
        const string output = """
            838 libs found in cache `/etc/ld.so.cache'
                libfoo.so.1 (libc6,x86-64) => /lib/x86_64-linux-gnu/libfoo.so.1
                libfoo.so.1 (libc6) => /lib/i386-linux-gnu/libfoo.so.1
                libc.so.6 (libc6,x86-64) => /lib/x86_64-linux-gnu/libc.so.6
            """;

        var result =
            ElfDependencyInspector.ParseLdConfigMappings(
                output
            );

        Assert.Equal(
            new[]
            {
                "/lib/x86_64-linux-gnu/libfoo.so.1",
                "/lib/i386-linux-gnu/libfoo.so.1"
            },
            result["libfoo.so.1"]
        );

        Assert.Equal(
            new[]
            {
                "/lib/x86_64-linux-gnu/libc.so.6"
            },
            result["libc.so.6"]
        );
    }

    [Fact]
    public void ParseLdConfigMappings_IgnoresMalformedEntries()
    {
        const string output = """
            malformed line
            libfoo.so.1 (libc6,x86-64) => relative/path/libfoo.so.1
             => /lib/libbroken.so
            """;

        var result =
            ElfDependencyInspector.ParseLdConfigMappings(
                output
            );

        Assert.Empty(result);
    }

    [Fact]
    public void ParseElfIdentity_ParsesClassAndMachine()
    {
        const string output = """
            ELF Header:
              Class:                             ELF64
              Machine:                           Advanced Micro Devices X86-64
            """;
    
        var result =
            ElfDependencyInspector.ParseElfIdentity(
                output
            );
    
        Assert.NotNull(result);
        Assert.Equal("ELF64", result.Class);
        Assert.Equal(
            "Advanced Micro Devices X86-64",
            result.Machine
        );
    }
    
    [Fact]
    public void ParseElfIdentity_MalformedOutput_ReturnsNull()
    {
        const string output = """
            this is not an ELF header
            """;
    
        var result =
            ElfDependencyInspector.ParseElfIdentity(
                output
            );
    
        Assert.Null(result);
    }
}