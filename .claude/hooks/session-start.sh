#!/bin/bash
# Installs the .NET 10 SDK and restores the Core/SkiaSharp projects so their
# builds and tests run in Claude Code cloud sessions. The MAUI project targets
# android/ios/maccatalyst only and isn't built here; CI builds it on Windows.
set -euo pipefail

if [ "${CLAUDE_CODE_REMOTE:-}" != "true" ]; then
  exit 0
fi

export DEBIAN_FRONTEND=noninteractive

# Ubuntu's own dotnet-sdk-10.0 package; fonts-noto-color-emoji matches CI so
# the SkiaSharp emoji tests don't return early.
if ! command -v dotnet >/dev/null 2>&1 || ! dotnet --list-sdks | grep -q '^10\.'; then
  apt-get update -qq
  apt-get install -y -qq dotnet-sdk-10.0 fonts-noto-color-emoji >/dev/null
elif ! dpkg -s fonts-noto-color-emoji >/dev/null 2>&1; then
  apt-get update -qq
  apt-get install -y -qq fonts-noto-color-emoji >/dev/null
fi

{
  echo 'export DOTNET_CLI_TELEMETRY_OPTOUT=1'
  echo 'export DOTNET_NOLOGO=1'
} >> "${CLAUDE_ENV_FILE:-/dev/null}"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1

cd "$CLAUDE_PROJECT_DIR"
for proj in src/MintedTextEditor.Core src/MintedTextEditor.SkiaSharp \
            tests/MintedTextEditor.Core.Tests tests/MintedTextEditor.SkiaSharp.Tests; do
  dotnet restore "$proj"
done
