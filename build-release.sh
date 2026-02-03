#!/bin/bash
# Build script for Unpacker - creates packages for Arch, Debian, and Fedora
# Usage: ./build-release.sh [version]

set -e

VERSION="${1:-1.0.0}"
APP_NAME="unpacker"
DISPLAY_NAME="Unpacker"
DESCRIPTION="Simplify installation of applications from tarballs on Linux"
MAINTAINER="Unpacker <unpacker@local>"
URL="https://github.com/semilore317/Unpacker"
LICENSE="MIT"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$SCRIPT_DIR/Unpacker"
BUILD_DIR="$SCRIPT_DIR/build"
PUBLISH_DIR="$BUILD_DIR/publish"
STAGING_DIR="$BUILD_DIR/staging"
OUTPUT_DIR="$SCRIPT_DIR/releases"

echo "=========================================="
echo "  Building Unpacker v$VERSION"
echo "=========================================="

# Clean previous builds
rm -rf "$BUILD_DIR" "$OUTPUT_DIR"
mkdir -p "$BUILD_DIR" "$PUBLISH_DIR" "$OUTPUT_DIR"

# Check prerequisites
echo ""
echo "[1/5] Checking prerequisites..."

if ! command -v dotnet &> /dev/null; then
    echo "ERROR: dotnet SDK is not installed"
    exit 1
fi

if ! command -v fpm &> /dev/null; then
    echo "ERROR: fpm is not installed"
    echo "Install with: gem install fpm"
    exit 1
fi

echo "  ✓ dotnet $(dotnet --version)"
echo "  ✓ fpm $(fpm --version | head -1)"

# Build the application
echo ""
echo "[2/5] Building application..."

dotnet publish "$PROJECT_DIR/Unpacker.csproj" \
    -c Release \
    -r linux-x64 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:PublishTrimmed=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -o "$PUBLISH_DIR"

# Verify build
if [ ! -f "$PUBLISH_DIR/Unpacker" ]; then
    echo "ERROR: Build failed - Unpacker binary not found"
    exit 1
fi

echo "  ✓ Binary built: $(du -h "$PUBLISH_DIR/Unpacker" | cut -f1)"

# Create staging directory structure
echo ""
echo "[3/5] Creating staging directory..."

mkdir -p "$STAGING_DIR/opt/$APP_NAME"
mkdir -p "$STAGING_DIR/usr/bin"
mkdir -p "$STAGING_DIR/usr/share/applications"
mkdir -p "$STAGING_DIR/usr/share/icons/hicolor/256x256/apps"

# Copy files
cp "$PUBLISH_DIR/Unpacker" "$STAGING_DIR/opt/$APP_NAME/$APP_NAME"
chmod +x "$STAGING_DIR/opt/$APP_NAME/$APP_NAME"

# Create symlink script (FPM will handle this)
ln -sf "/opt/$APP_NAME/$APP_NAME" "$STAGING_DIR/usr/bin/$APP_NAME"

# Copy icon
cp "$PROJECT_DIR/Assets/unpacker.png" "$STAGING_DIR/usr/share/icons/hicolor/256x256/apps/$APP_NAME.png"

# Create .desktop file
cat > "$STAGING_DIR/usr/share/applications/$APP_NAME.desktop" << EOF
[Desktop Entry]
Name=$DISPLAY_NAME
Comment=$DESCRIPTION
Exec=/opt/$APP_NAME/$APP_NAME %u
Icon=$APP_NAME
Type=Application
StartupNotify=true
Categories=Utility;Archiving;
StartupWMClass=$APP_NAME
Terminal=false
EOF

echo "  ✓ Staging directory created"

# Create post-install script
POST_INSTALL="$BUILD_DIR/post-install.sh"
cat > "$POST_INSTALL" << 'EOF'
#!/bin/bash
# Update caches
if command -v update-desktop-database &> /dev/null; then
    update-desktop-database -q /usr/share/applications 2>/dev/null || true
fi
if command -v gtk-update-icon-cache &> /dev/null; then
    gtk-update-icon-cache -q -t -f /usr/share/icons/hicolor 2>/dev/null || true
fi
exit 0
EOF
chmod +x "$POST_INSTALL"

# Build packages
echo ""
echo "[4/5] Building packages..."

# Common FPM options
FPM_COMMON=(
    -s dir
    -n "$APP_NAME"
    -v "$VERSION"
    --description "$DESCRIPTION"
    --maintainer "$MAINTAINER"
    --url "$URL"
    --license "$LICENSE"
    --after-install "$POST_INSTALL"
    -C "$STAGING_DIR"
)

# Debian (.deb)
echo "  Building .deb package..."
fpm "${FPM_COMMON[@]}" \
    -t deb \
    -p "$OUTPUT_DIR/${APP_NAME}_${VERSION}_amd64.deb" \
    --depends "libx11-6" \
    --depends "libfontconfig1" \
    .
echo "  ✓ $(basename "$OUTPUT_DIR"/${APP_NAME}_${VERSION}_amd64.deb)"

# Fedora/RHEL (.rpm)
echo "  Building .rpm package..."
fpm "${FPM_COMMON[@]}" \
    -t rpm \
    -p "$OUTPUT_DIR/${APP_NAME}-${VERSION}-1.x86_64.rpm" \
    --depends "libX11" \
    --depends "fontconfig" \
    .
echo "  ✓ $(basename "$OUTPUT_DIR"/${APP_NAME}-${VERSION}-1.x86_64.rpm)"

# Arch Linux (.pkg.tar.zst)
echo "  Building Arch package..."
fpm "${FPM_COMMON[@]}" \
    -t pacman \
    -p "$OUTPUT_DIR/${APP_NAME}-${VERSION}-1-x86_64.pkg.tar.zst" \
    --depends "libx11" \
    --depends "fontconfig" \
    .
echo "  ✓ $(basename "$OUTPUT_DIR"/${APP_NAME}-${VERSION}-1-x86_64.pkg.tar.zst)"

# Summary
echo ""
echo "[5/5] Build complete!"
echo ""
echo "=========================================="
echo "  Release packages created in: $OUTPUT_DIR"
echo "=========================================="
ls -lh "$OUTPUT_DIR"
echo ""
echo "Installation commands:"
echo "  Debian/Ubuntu: sudo dpkg -i ${APP_NAME}_${VERSION}_amd64.deb"
echo "  Fedora/RHEL:   sudo dnf install ${APP_NAME}-${VERSION}-1.x86_64.rpm"
echo "  Arch Linux:    sudo pacman -U ${APP_NAME}-${VERSION}-1-x86_64.pkg.tar.zst"

# Cleanup
rm -rf "$BUILD_DIR"
echo ""
echo "Done!"
