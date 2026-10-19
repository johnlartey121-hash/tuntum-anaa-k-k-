#!/bin/bash
# Double-click on a Mac with Xcode: downloads the Tuntum iPhone project and opens it in Xcode.
cd "$HOME/Desktop" || exit 1
rm -rf Tuntum-iPhone && mkdir Tuntum-iPhone && cd Tuntum-iPhone
curl -L -o project.zip https://github.com/johnlartey121-hash/tuntum-anaa-k-k-/releases/download/latest/Tuntum-iOS-Xcode-project.zip
unzip -q project.zip
open App/ios/App/App.xcworkspace
echo "Xcode is opening the Tuntum project."
