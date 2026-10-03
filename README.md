# Unpacker

Unpacker is a Linux desktop app for installing software distributed as compressed tarball archives.

## Description

I built Unpacker because installing Linux applications distributed as tarballs is often a frustrating manual process. You may need to extract an archive, locate the correct executable, inspect missing dependencies, copy files into the appropriate directories, and create a desktop launcher yourself. Unpacker brings those steps together in a graphical workflow.

It can install an application locally under `~/.local` without administrator access, create a launcher and desktop entry, or build native `.deb`, `.rpm`, and Arch packages using FPM. Unpacker also checks ELF library dependencies with `readelf` rather than executing programs from an extracted archive. System-wide installation uses `pkexec` and PolicyKit when elevated privileges are required.

## Screenshots

<img width="1433" height="747" alt="Unpacker installation screen" src="https://github.com/user-attachments/assets/f7470f4e-e16c-4b71-95ea-42b35dbc136b" />

## What it does

- Opens `.tar`, `.tar.gz`, `.tar.xz`, `.tar.bz2`, and `.tgz` archives.
- Finds ELF executables and script launchers. It checks ELF library dependencies with `readelf` without running the extracted program.
- Creates a user-local launcher and desktop entry, with an icon if you choose one.
- Builds `.deb`, `.rpm`, and Arch packages with FPM. System installation uses `pkexec` and a graphical PolicyKit agent.

## Getting started

### Dependencies

Unpacker requires a 64-bit Linux desktop with `tar`, `readelf`, and `ldconfig`. System-wide installation also requires `pkexec` and a working graphical PolicyKit agent. FPM is only required when you use Unpacker to build native packages.

### Installing

Download the package for your distribution from the [latest release](https://github.com/Semilore317/Unpacker/releases/tag/v1.0.0-alpha.2), open a terminal in your Downloads folder, and run the matching command:

**Debian or Ubuntu**

```bash
sudo apt install ./unpacker_1.0.0-alpha.2_amd64.deb
```

**Fedora or RHEL**

```bash
sudo dnf install ./unpacker-1.0.0-alpha.2-1.x86_64.rpm
```

**Arch Linux**

```bash
sudo pacman -U ./unpacker-1.0.0-alpha.2-1-x86_64.pkg.tar.zst
```

### Executing Unpacker

Open **Unpacker** from your desktop application menu, or run it from a terminal:

```bash
unpacker
```

Choose a supported tarball, review the detected application details, select a user-local or system-wide installation, and start the installation.

## Run from source

To build Unpacker yourself, install the .NET 10 SDK along with `tar`, `readelf`, and `ldconfig`.

```bash
dotnet build
dotnet test
dotnet run --project Unpacker/Unpacker.csproj
```

For native package builds, install [FPM](https://fpm.readthedocs.io/) and the package manager for your distribution. The root `build-release.sh` script publishes a `linux-x64` build and writes packages to `releases/`.

## Help

- User-local installation does not require administrator access.
- System-wide installation needs `pkexec` and a graphical PolicyKit agent. A plain WSL session usually does not provide the required agent.
- Native package generation requires FPM and the packaging tools for your distribution.
- Unpacker does not manage uninstalls yet.

If you find a problem, [open an issue](https://github.com/Semilore317/Unpacker/issues) and include your Linux distribution, the archive format, and the installation log.

## License

Unpacker is licensed under the [MIT License](LICENSE).
