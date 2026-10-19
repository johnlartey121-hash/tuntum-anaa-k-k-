#!/usr/bin/env bash
# Signs the Unity-exported Xcode project, archives it and uploads the .ipa to TestFlight.
# Runs on the macOS job in .github/workflows/build.yml when the Apple secrets exist.
set -euo pipefail
PROJECT_DIR="$1"
WORK="$RUNNER_TEMP/ios"; mkdir -p "$WORK"

# 1. temporary keychain with the distribution certificate
KEYCHAIN="$WORK/build.keychain-db"; KEYCHAIN_PASS="$(uuidgen)"
echo "$APPLE_CERTIFICATE_P12_BASE64" | base64 --decode > "$WORK/cert.p12"
security create-keychain -p "$KEYCHAIN_PASS" "$KEYCHAIN"
security set-keychain-settings -lut 3600 "$KEYCHAIN"
security unlock-keychain -p "$KEYCHAIN_PASS" "$KEYCHAIN"
security import "$WORK/cert.p12" -P "$APPLE_CERTIFICATE_PASSWORD" -A -t cert -f pkcs12 -k "$KEYCHAIN"
security set-key-partition-list -S apple-tool:,apple: -k "$KEYCHAIN_PASS" "$KEYCHAIN" >/dev/null
security list-keychains -d user -s "$KEYCHAIN" $(security list-keychains -d user | tr -d '"')

# 2. provisioning profile
echo "$APPLE_PROVISIONING_PROFILE_BASE64" | base64 --decode > "$WORK/app.mobileprovision"
PROFILE_UUID=$(security cms -D -i "$WORK/app.mobileprovision" | plutil -extract UUID raw -)
mkdir -p "$HOME/Library/MobileDevice/Provisioning Profiles"
cp "$WORK/app.mobileprovision" "$HOME/Library/MobileDevice/Provisioning Profiles/$PROFILE_UUID.mobileprovision"

# 3. archive and export
xcodebuild -project "$PROJECT_DIR/Unity-iPhone.xcodeproj" -scheme Unity-iPhone -configuration Release \
  -archivePath "$WORK/Tuntum.xcarchive" -destination 'generic/platform=iOS' \
  DEVELOPMENT_TEAM="$APPLE_TEAM_ID" CODE_SIGN_STYLE=Manual CODE_SIGN_IDENTITY="Apple Distribution" \
  PROVISIONING_PROFILE_SPECIFIER="$PROFILE_UUID" archive | tail -20
cat > "$WORK/export.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
  <key>method</key><string>app-store</string>
  <key>teamID</key><string>$APPLE_TEAM_ID</string>
  <key>provisioningProfiles</key><dict><key>com.johnlartey.tuntum</key><string>$PROFILE_UUID</string></dict>
</dict></plist>
PLIST
xcodebuild -exportArchive -archivePath "$WORK/Tuntum.xcarchive" -exportOptionsPlist "$WORK/export.plist" -exportPath "$WORK/out" | tail -5

# 4. upload to TestFlight
mkdir -p "$HOME/.appstoreconnect/private_keys"
echo "$APP_STORE_CONNECT_KEY_P8_BASE64" | base64 --decode > "$HOME/.appstoreconnect/private_keys/AuthKey_$APP_STORE_CONNECT_KEY_ID.p8"
xcrun altool --upload-app -f "$WORK"/out/*.ipa -t ios --apiKey "$APP_STORE_CONNECT_KEY_ID" --apiIssuer "$APP_STORE_CONNECT_ISSUER_ID"
echo "Uploaded to TestFlight."
