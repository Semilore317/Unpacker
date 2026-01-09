#!/bin/bash
APP_NAME="Unpacker"
VERSION="1.0.0"
DESCRIPTION="A lightweight Linux Utility to simplify installation from archives."
MAINTAINER="Abraham Bankole <abraham.o.bankole@gmail.com>"
URL="[https://github.com/semilore317/unpacker](https://github.com/semilore317/unpackr)"

# Input Directory (Where dotnet published to)
INPUT_DIR="./dist/linux-x64"

# Output Directory (Where .deb/.rpm go)
OUTPUT_DIR="./release"
mkdir -p $OUTPUT_DIR

echo "Packaging for Debian/Ubuntu (.deb)..."
fpm -s dir -t deb \
    -n "$APP_NAME" \
    -v "$VERSION" \
    -m "$MAINTAINER" \
    --url "$URL" \
    --description "$DESCRIPTION" \
    --license "MIT" \
    --architecture x86_64 \
    --depends libgl1 \
    --depends libice6 \
    --depends libsm6 \
    --depends libfontconfig1 \
    "$INPUT_DIR/=/opt/$APP_NAME" \
    "./assets/$APP_NAME.desktop=/usr/share/applications/$APP_NAME.desktop" \
    "./assets/icon.svg=/usr/share/icons/hicolor/scalable/apps/$APP_NAME.svg" \
    -p "$OUTPUT_DIR/"

echo "Packaging for Fedora/RHEL (.rpm)..."
fpm -s dir -t rpm \
    -n "$APP_NAME" \
    -v "$VERSION" \
    -m "$MAINTAINER" \
    --url "$URL" \
    --description "$DESCRIPTION" \
    --license "MIT" \
    --architecture x86_64 \
    --rpm-rpmbuild-define "_build_id_links none" \
    --depends libX11 \
    --depends libICE \
    --depends libSM \
    "$INPUT_DIR/=/opt/$APP_NAME" \
    "./assets/$APP_NAME.desktop=/usr/share/applications/$APP_NAME.desktop" \
    "./assets/icon.svg=/usr/share/icons/hicolor/scalable/apps/$APP_NAME.svg" \
    -p "$OUTPUT_DIR/"

echo "Done! Packages are in $OUTPUT_DIR"
