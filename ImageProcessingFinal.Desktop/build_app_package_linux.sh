# Use appimage, deb, rpm, dmg, msix or exe when you want a different artifact.
#
#
# .AppImage 	Portable Linux desktop apps 	Supported
# .deb 	Debian, Ubuntu, Mint, Raspberry Pi OS 	Supported
# .rpm 	Fedora, openSUSE, RHEL-like distros 	Supported
# .dmg 	macOS drag-and-drop installers 	Experimental
# .msix 	Windows app packages 	Experimental
# .exe 	Windows self-extracting installers 	Preview

dotnetpackager $1 from-directory  --directory ./bin/Release/net9.0/linux-x64/publish --arch x64  --output ./artifacts/PixAlchemy.$1 --comment "Video and image editor"   --application-name "PixAlchemy" --summary "PixAlchemy is an Avalonia desktop app for video and image effects powered by OpenCV (Emgu CV)"  --version 1.0.0 --icon ./Assets/package_icon.png --homepage "https://github.com/yodalego99/pixalchemy"
