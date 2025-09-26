# Packaging Guide

## Overview

AGI.Kapster uses automated multi-platform packaging integrated with GitHub Actions to create installers and packages for Windows, macOS, and Linux distributions.

## Platform Support

| Platform | Package Format | Tools | Distribution |
|----------|----------------|-------|--------------|
| Windows | MSI | WiX Toolset | GitHub Releases |
| macOS | PKG, DMG | pkgbuild, hdiutil | GitHub Releases |
| Linux | DEB, RPM | dpkg-deb, rpmbuild | GitHub Releases |

## Automated Packaging

### GitHub Actions Integration

Packaging is fully automated through the release workflow:

```yaml
- name: Package Application
  uses: ./.github/actions/publish-package
  with:
    runtime: ${{ matrix.runtime }}
    configuration: Release
    output-path: ./artifacts/${{ matrix.runtime }}
```

### Build Matrix

```yaml
strategy:
  matrix:
    include:
      - os: windows-latest
        runtime: win-x64
        package-format: msi
      - os: macos-latest
        runtime: osx-x64
        package-format: pkg
      - os: ubuntu-latest
        runtime: linux-x64
        package-format: deb
```

## Windows Packaging

### MSI Creation with WiX

The Windows installer uses WiX Toolset for professional MSI packages:

```xml
<!-- AGI.Kapster.wxs -->
<Wix xmlns="http://schemas.microsoft.com/wix/2006/wi">
  <Product Id="*" 
           Name="AGI.Kapster" 
           Language="1033" 
           Version="!(bind.FileVersion.AGI.Kapster.Desktop.exe)"
           Manufacturer="AGI Build"
           UpgradeCode="12345678-1234-1234-1234-123456789012">
    
    <Package InstallerVersion="200" 
             Compressed="yes" 
             InstallScope="perMachine" />
             
    <MajorUpgrade DowngradeErrorMessage="A newer version is already installed." />
    
    <Directory Id="TARGETDIR" Name="SourceDir">
      <Directory Id="ProgramFilesFolder">
        <Directory Id="INSTALLFOLDER" Name="AGI.Kapster" />
      </Directory>
    </Directory>
    
    <ComponentGroup Id="ProductComponents" Directory="INSTALLFOLDER">
      <Component Id="MainExecutable">
        <File Id="AGI.Kapster.Desktop.exe" 
              Source="$(var.PublishDir)\AGI.Kapster.Desktop.exe" 
              KeyPath="yes" />
      </Component>
    </ComponentGroup>
    
    <Feature Id="ProductFeature" Title="AGI.Kapster" Level="1">
      <ComponentGroupRef Id="ProductComponents" />
    </Feature>
  </Product>
</Wix>
```

### Build Command
```bash
# Build MSI package
dotnet publish -c Release -r win-x64 --self-contained
candle AGI.Kapster.wxs -ext WixUtilExtension
light AGI.Kapster.wixobj -ext WixUtilExtension -out AGI.Kapster.msi
```

### MSI Features
- **Upgrade Support**: Automatic upgrades and downgrades handling
- **Registry Integration**: File associations and context menu
- **Start Menu**: Application shortcuts
- **Uninstall Support**: Clean removal through Control Panel

## macOS Packaging

### PKG Creation

macOS packages use the native pkgbuild toolchain:

```bash
# Create application bundle structure
mkdir -p AGI.Kapster.app/Contents/MacOS
mkdir -p AGI.Kapster.app/Contents/Resources

# Copy executable and resources
cp publish/AGI.Kapster.Desktop AGI.Kapster.app/Contents/MacOS/
cp logo.icns AGI.Kapster.app/Contents/Resources/

# Create Info.plist
cat > AGI.Kapster.app/Contents/Info.plist << EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleExecutable</key>
    <string>AGI.Kapster.Desktop</string>
    <key>CFBundleIdentifier</key>
    <string>com.agibuild.agikapster</string>
    <key>CFBundleName</key>
    <string>AGI.Kapster</string>
    <key>CFBundleVersion</key>
    <string>2024.9.23.1</string>
</dict>
</plist>
EOF

# Build PKG
pkgbuild --root ./AGI.Kapster.app --identifier com.agibuild.agikapster --install-location /Applications/AGI.Kapster.app AGI.Kapster.pkg
```

### DMG Creation (Optional)
```bash
# Create DMG for drag-and-drop installation
hdiutil create -size 100m -fs HFS+ -volname "AGI.Kapster" temp.dmg
hdiutil attach temp.dmg
cp -R AGI.Kapster.app /Volumes/AGI.Kapster/
hdiutil detach /Volumes/AGI.Kapster
hdiutil convert temp.dmg -format UDZO -o AGI.Kapster.dmg
```

### Code Signing
```bash
# Sign application (requires Apple Developer certificate)
codesign --sign "Developer ID Application: Your Name" AGI.Kapster.app
codesign --verify --verbose AGI.Kapster.app

# Notarize for distribution
xcrun altool --notarize-app --primary-bundle-id com.agibuild.agikapster --file AGI.Kapster.pkg
```

## Linux Packaging

### DEB Package Creation

Debian packages for Ubuntu and other Debian-based distributions:

```bash
# Create package structure
mkdir -p agi-kapster_2024.9.23.1/DEBIAN
mkdir -p agi-kapster_2024.9.23.1/usr/bin
mkdir -p agi-kapster_2024.9.23.1/usr/share/applications
mkdir -p agi-kapster_2024.9.23.1/usr/share/pixmaps

# Create control file
cat > agi-kapster_2024.9.23.1/DEBIAN/control << EOF
Package: agi-kapster
Version: 2024.9.23.1
Architecture: amd64
Maintainer: AGI Build <support@agibuild.com>
Description: Cross-platform screen capture and annotation tool
 AGI.Kapster provides advanced screen capture capabilities with
 overlay annotation support for desktop productivity.
Depends: libgtk-3-0, libx11-6
EOF

# Copy files
cp publish/AGI.Kapster.Desktop agi-kapster_2024.9.23.1/usr/bin/agi-kapster
chmod +x agi-kapster_2024.9.23.1/usr/bin/agi-kapster

# Create desktop entry
cat > agi-kapster_2024.9.23.1/usr/share/applications/agi-kapster.desktop << EOF
[Desktop Entry]
Name=AGI.Kapster
Comment=Screen capture and annotation tool
Exec=/usr/bin/agi-kapster
Icon=agi-kapster
Type=Application
Categories=Graphics;Photography;
EOF

# Build DEB package
dpkg-deb --build agi-kapster_2024.9.23.1
```

### RPM Package Creation

Red Hat packages for CentOS, RHEL, and Fedora:

```spec
# agi-kapster.spec
Name: agi-kapster
Version: 2024.9.23.1
Release: 1%{?dist}
Summary: Cross-platform screen capture and annotation tool
License: MIT
URL: https://github.com/AGIBuild/AGI.Kapster
Source0: agi-kapster-%{version}.tar.gz

BuildRequires: dotnet-sdk-9.0
Requires: gtk3, libX11

%description
AGI.Kapster provides advanced screen capture capabilities with
overlay annotation support for desktop productivity.

%prep
%setup -q

%build
dotnet publish -c Release -r linux-x64 --self-contained

%install
mkdir -p %{buildroot}/usr/bin
mkdir -p %{buildroot}/usr/share/applications
mkdir -p %{buildroot}/usr/share/pixmaps

cp publish/AGI.Kapster.Desktop %{buildroot}/usr/bin/agi-kapster
cp packaging/linux/agi-kapster.desktop %{buildroot}/usr/share/applications/
cp packaging/linux/agi-kapster.png %{buildroot}/usr/share/pixmaps/

%files
/usr/bin/agi-kapster
/usr/share/applications/agi-kapster.desktop
/usr/share/pixmaps/agi-kapster.png

%changelog
* Mon Sep 23 2024 AGI Build <support@agibuild.com> - 2024.9.23.1-1
- Initial package release
```

```bash
# Build RPM
rpmbuild -ba agi-kapster.spec
```

## Package Verification

### Automated Testing

Each package format includes automated verification:

```yaml
- name: Test Windows Package
  if: matrix.os == 'windows-latest'
  run: |
    msiexec /i AGI.Kapster.msi /quiet /norestart
    & "C:\Program Files\AGI.Kapster\AGI.Kapster.Desktop.exe" --version
    
- name: Test macOS Package
  if: matrix.os == 'macos-latest'
  run: |
    sudo installer -pkg AGI.Kapster.pkg -target /
    /Applications/AGI.Kapster.app/Contents/MacOS/AGI.Kapster.Desktop --version
    
- name: Test Linux Package
  if: matrix.os == 'ubuntu-latest'
  run: |
    sudo dpkg -i agi-kapster.deb
    agi-kapster --version
```

### Manual Verification Checklist

#### Windows MSI
- [ ] Installs without errors
- [ ] Creates Start Menu shortcut
- [ ] Registers file associations
- [ ] Uninstalls cleanly

#### macOS PKG
- [ ] Installs to /Applications
- [ ] Launches from Finder
- [ ] Appears in Launchpad
- [ ] Notarization status valid

#### Linux DEB/RPM
- [ ] Installs dependencies correctly
- [ ] Creates desktop entry
- [ ] Executable permissions correct
- [ ] Removes cleanly

## Distribution Strategy

### GitHub Releases

All packages are automatically uploaded to GitHub Releases:

```yaml
- name: Create Release
  uses: actions/create-release@v1
  with:
    tag_name: ${{ github.ref }}
    release_name: Release ${{ github.ref }}
    draft: false
    prerelease: false

- name: Upload Release Assets
  uses: actions/upload-release-asset@v1
  with:
    upload_url: ${{ steps.create_release.outputs.upload_url }}
    asset_path: ./artifacts/${{ matrix.package-name }}
    asset_name: ${{ matrix.package-name }}
    asset_content_type: application/octet-stream
```

### Package Repositories

Future distribution channels:

#### Windows
- **Microsoft Store**: UWP packaging consideration
- **Chocolatey**: Community package repository
- **Winget**: Windows Package Manager

#### macOS
- **Mac App Store**: App Store distribution
- **Homebrew**: Package manager integration

#### Linux
- **APT Repository**: Ubuntu/Debian official repository
- **YUM Repository**: Red Hat/CentOS repository
- **Snap Store**: Universal Linux packages
- **Flatpak**: Sandboxed application distribution

## Security Considerations

### Code Signing

All packages should be signed for security and trust:

- **Windows**: Authenticode signing with certificate
- **macOS**: Apple Developer ID signing and notarization
- **Linux**: GPG signing of packages and repositories

### Integrity Verification

SHA256 checksums are generated for all packages:

```bash
# Generate checksums
sha256sum *.msi *.pkg *.deb *.rpm > checksums.txt

# Verify downloads
sha256sum -c checksums.txt
```

### Vulnerability Scanning

Packages are scanned for vulnerabilities before release:

```yaml
- name: Security Scan
  uses: securecodewarrior/github-action-add-sarif@v1
  with:
    sarif-file: security-scan-results.sarif
```

## Troubleshooting

### Common Build Issues

#### Windows WiX Problems
```bash
# Install WiX Toolset
dotnet tool install --global wix

# Verify installation
candle -?
light -?
```

#### macOS Signing Issues
```bash
# List available certificates
security find-identity -v -p codesigning

# Verify signing
codesign --verify --verbose=4 AGI.Kapster.app
spctl --assess --type exec AGI.Kapster.app
```

#### Linux Dependency Issues
```bash
# Check DEB dependencies
dpkg-deb --info agi-kapster.deb

# Verify RPM dependencies
rpm -qpR agi-kapster.rpm
```

### Package Testing

```bash
# Test package installation
docker run --rm -v $(pwd):/workspace ubuntu:latest bash -c "
  cd /workspace &&
  apt update &&
  dpkg -i agi-kapster.deb || apt install -f -y &&
  agi-kapster --version
"
```