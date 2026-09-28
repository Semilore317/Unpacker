# Unpacker

Unpacker is a Linux desktop app for installing software shipped as tarballs. It can set an app up in `~/.local` without admin access, or build a native package with FPM.

<img width="1433" height="747" alt="image" src="https://github.com/user-attachments/assets/f7470f4e-e16c-4b71-95ea-42b35dbc136b" />

## What it does

- Opens `.tar`, `.tar.gz`, `.tar.xz`, `.tar.bz2`, and `.tgz` archives.
- Finds ELF executables and script launchers. It checks ELF library dependencies with `readelf` without running the extracted program.
- Creates a user-local launcher and desktop entry, with an icon if you choose one.
- Builds `.deb`, `.rpm`, and Arch packages with FPM. System installation uses `pkexec` and a graphical PolicyKit agent.

## Run from source

You need Linux, the .NET 10 SDK, `tar`, `readelf`, and `ldconfig`.

```bash
dotnet build
dotnet test
dotnet run --project Unpacker/Unpacker.csproj
```

For native packages, install [FPM](https://fpm.readthedocs.io/) and the package manager for your distribution. The root `build-release.sh` script publishes a `linux-x64` build and writes packages to `releases/`.]
