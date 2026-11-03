#!/usr/bin/env bash
# Run from anywhere. Requires the .NET 10 SDK and macOS.
set -euo pipefail
project_root="$(cd "$(dirname "$0")/.." && pwd)"
architecture="${1:-osx-arm64}"
case "$architecture" in osx-arm64|osx-x64) ;; *) echo 'Use osx-arm64 or osx-x64.'; exit 1 ;; esac
output_directory="$project_root/dist/$architecture"
package_stage="$(mktemp -d "${TMPDIR:-/tmp}/jobtracker.XXXXXX")"
trap 'rm -rf -- "$package_stage"' EXIT
bundle="$package_stage/JobTracker.app"
mkdir -p "$bundle/Contents/MacOS" "$bundle/Contents/Resources"
dotnet publish "$project_root/src/JobTracker/JobTracker.csproj" -c Release -r "$architecture" --self-contained true -o "$bundle/Contents/MacOS" -p:PublishTrimmed=false
cat > "$bundle/Contents/Info.plist" <<'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
<key>CFBundleName</key><string>JobTracker</string>
<key>CFBundleDisplayName</key><string>JobTracker</string>
<key>CFBundleIdentifier</key><string>com.aidanhuangg.jobtracker</string>
<key>CFBundleVersion</key><string>2.0.0</string>
<key>CFBundleShortVersionString</key><string>2.0.0</string>
<key>CFBundlePackageType</key><string>APPL</string>
<key>CFBundleExecutable</key><string>JobTracker</string>
<key>NSHighResolutionCapable</key><true/>
<key>NSPrincipalClass</key><string>NSApplication</string>
</dict></plist>
PLIST
chmod +x "$bundle/Contents/MacOS/JobTracker"
# Local ad-hoc signature; distribution to other Macs needs a Developer ID and notarization.
# Finder can attach display metadata to a newly created .app; it cannot be signed with that attribute.
xattr -dr com.apple.FinderInfo "$bundle" 2>/dev/null || true
codesign --force --deep --sign - "$bundle"
mkdir -p "$output_directory"
# Only replace the generated app in this script's own architecture-specific output folder.
rm -rf -- "$output_directory/JobTracker.app"
ditto "$bundle" "$output_directory/JobTracker.app"
# Preserve extended code-signing attributes on .NET assemblies in the distributable ZIP.
ditto -c -k --sequesterRsrc --keepParent "$bundle" "$output_directory/JobTracker-$architecture.zip"
echo "App ready: $output_directory/JobTracker.app"
echo "ZIP ready: $output_directory/JobTracker-$architecture.zip"
