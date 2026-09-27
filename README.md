# Unpacker

[![CI](https://github.com/Semilore317/Unpacker/actions/workflows/ci.yml/badge.svg)](https://github.com/Semilore317/Unpacker/actions/workflows/ci.yml)

Unpacker is a Linux desktop utility for safely installing applications distributed as tar archives. It can integrate an application into the current user's desktop without elevation, or generate and install a native system package.

## Features

- Inspects and extracts `.tar`, `.tar.gz`, `.tar.xz`, `.tar.bz2`, and `.tgz` archives.
- Detects ELF executables and script launchers, then selects the most likely entry point.
- Installs applications for the current user under `~/.local`, including a launcher, desktop entry, and optional icon.
- Generates native packages with FPM for Debian/Ubuntu (`.deb`), Fedora/RHEL (`.rpm`), and Arch Linux (`.pkg.tar.*`).
- Inspects ELF dependencies without executing archive contents.
- Resolves libraries by architecture and maps them to distribution packages when possible.

## Security approach

Unpacker treats downloaded archives and application metadata as untrusted input.

- External processes are started with `ProcessStartInfo.ArgumentList`; privileged package commands never pass through `sh -c`.
- ELF dependencies are inspected statically with `readelf` and resolved through `ldconfig`. Unpacker does not use `ldd`, which can execute untrusted binaries on some systems.
- Dependency resolution checks the ELF class and machine architecture and uses canonical library paths.
- System-wide package installation is delegated to PolicyKit through `pkexec --disable-internal-agent`.
- Unpacker never asks for or stores an administrator password.
- User-local installation uses direct .NET file operations, preserves source symlinks, and does not generate or run an installer shell script.

## How it works

```text
Avalonia UI (InstallActions)
        |
        +-- ArchiveInspector -------- executable and launcher detection
        +-- ElfDependencyInspector -- static ELF/dependency inspection
        +-- UserLocalInstaller ------ shell-free ~/.local integration
        +-- ProcessRunner ----------- structured external process execution
```

`InstallActions` coordinates the desktop workflow. The supporting services isolate archive inspection, dependency analysis, user-local installation, and process invocation so those behaviors can be tested independently of the UI.

## Requirements

### Build and run

- Linux (x64 or ARM64)
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- `tar`
- GNU binutils (`readelf`)
- `ldconfig` (normally provided by the distribution's libc tooling)

### System-wide packaging and installation

User-local installation does not require these additional tools. Native system packages require:

- [FPM](https://fpm.readthedocs.io/) (`gem install fpm`)
- Ruby and the build tools required to install the FPM gem
- `pkexec` and a graphical PolicyKit authentication agent
- A supported package manager: APT/dpkg, DNF/RPM, or pacman

## Build and run from source

```bash
git clone https://github.com/Semilore317/Unpacker.git
cd Unpacker
dotnet restore
dotnet build
dotnet run --project Unpacker/Unpacker.csproj
```

Run the test suite with:

```bash
dotnet test
```

The current suite contains 26 xUnit tests covering archive executable detection, static ELF dependency parsing and resolution, and shell-free user-local installation behavior.

## Release packages

The repository's release script publishes a self-contained `linux-x64` build and packages it for Debian/Ubuntu, Fedora/RHEL, and Arch Linux:

```bash
./build-release.sh 1.0.0
```

This command requires FPM and writes packages to `releases/`.

## Demo

> A current Linux desktop screenshot or short demo will be added here. The application must be run in a graphical Linux session to capture the full archive-to-install flow.

## Current limitations

- Unpacker is Linux-only; other desktop platforms are not currently supported.
- FPM is required for native system-package generation.
- Privileged installation requires `pkexec` and a working graphical PolicyKit authentication agent.
- WSL does not normally provide a graphical PolicyKit agent, so package generation can be tested there but the final elevation flow is not a valid end-to-end WSL test.
- Uninstall management is not implemented.
- Release-package generation currently targets `linux-x64`; source builds also declare `linux-arm64` as a supported runtime identifier.

## Tech stack

- C# and .NET 10
- Avalonia 11.3
- xUnit
- FPM
- Linux desktop integration and PolicyKit

## License

MIT License. See `LICENSE`.
