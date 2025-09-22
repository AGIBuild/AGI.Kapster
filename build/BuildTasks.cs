using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tooling;
using static Nuke.Common.EnvironmentInfo;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

class BuildTasks : NukeBuild
{
    public static int Main() => Execute<BuildTasks>(x => x.Build);

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    [Parameter("Runtime identifiers for publish, comma-separated (e.g., win-x64,linux-x64,osx-x64,osx-arm64)")]
    readonly string Rids;

    [Parameter("Target framework override (default: auto-detect from project)")]
    readonly string Framework;

    [Parameter("Self-contained deployment")]
    readonly bool SelfContained = true;

    [Parameter("Single file publish")]
    readonly bool SingleFile = true;

    [Parameter("Enable IL trimming (use with caution for Avalonia apps)")]
    readonly bool Trim;

    [Parameter("Test filter expression (e.g., Category=Unit)")]
    readonly string TestFilter;

    [Parameter("Skip tests during build/publish operations")]
    readonly bool SkipTests;

    [Parameter("Enable code coverage collection")]
    readonly bool Coverage;

    [Solution(SuppressBuildProjectCheck = true)] readonly Solution Solution;
    [Parameter("Manual new version (display format: yyyy.M.d.HHmmss — month/day no leading zero, time HHmmss). If omitted auto-generate.")] readonly string NewVersion; // updated

    AbsolutePath VersionFile => RootDirectory / "version.json"; // added
    static (string Display, string Assembly, string File, string Info)? _versionCache; // added

    // Paths
    AbsolutePath ArtifactsDirectory => RootDirectory / "artifacts";
    AbsolutePath PublishDirectory => ArtifactsDirectory / "publish";
    AbsolutePath TestResultsDirectory => ArtifactsDirectory / "test-results";
    AbsolutePath CoverageDirectory => ArtifactsDirectory / "coverage";

    // Project references
    Project MainProject => Solution?.AllProjects?.FirstOrDefault(p => p.Name == "AGI.Captor.Desktop");
    Project[] TestProjects => Solution?.AllProjects?.Where(p => p.Name.Contains("Tests")).ToArray() ?? Array.Empty<Project>();

    // Runtime detection
    string CurrentRuntimeIdentifier => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win-x64" :
        RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ?
            (RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "osx-arm64" : "osx-x64") :
        RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "linux-x64" :
        throw new PlatformNotSupportedException("Unsupported platform");

    string[] PublishRuntimeIdentifiers =>
        string.IsNullOrWhiteSpace(Rids) ? new[] { CurrentRuntimeIdentifier } :
        Rids.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    string TargetFramework => GetTargetFramework();

    string GetTargetFramework()
    {
        if (!string.IsNullOrWhiteSpace(Framework))
            return Framework;

        try
        {
            return MainProject?.GetProperty("TargetFramework") ??
                   MainProject?.GetProperty("TargetFrameworks")?.Split(';').First() ??
                   "net9.0";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Failed to read target framework: {ex.Message}. Using default: net9.0");
            return "net9.0";
        }
    }

    Target Clean => _ => _
        .Before(Restore)
        .Executes(() =>
        {
            ArtifactsDirectory.CreateOrCleanDirectory();
            TestResultsDirectory.CreateOrCleanDirectory();
            CoverageDirectory.CreateOrCleanDirectory();
            PublishDirectory.CreateOrCleanDirectory();

            DotNetClean(s => s
                .SetProject(Solution)
                .SetConfiguration(Configuration));
        });

    Target Restore => _ => _
        .After(Clean)
        .Executes(() =>
        {
            DotNetRestore(s => s
                .SetProjectFile(Solution));
        });

    Target Build => _ => _
        .Description("Compile solution")
        .DependsOn(Restore)
        .Executes(() =>
        {
            DotNetBuild(s => s
                .SetProjectFile(Solution)
                .SetConfiguration(Configuration)
                .EnableNoRestore());
        });

    Target Test => _ => _
        .DependsOn(Build)
        .OnlyWhenDynamic(() => !SkipTests)
        .Executes(() =>
        {
            try
            {
                var testSettings = new DotNetTestSettings()
                    .SetProjectFile(Solution)
                    .SetConfiguration(Configuration)
                    .SetResultsDirectory(TestResultsDirectory)
                    .EnableNoBuild()
                    .EnableNoRestore();

                if (!string.IsNullOrWhiteSpace(TestFilter))
                    testSettings = testSettings.SetFilter(TestFilter);

                if (Coverage)
                {
                    testSettings = testSettings
                        .SetDataCollector("XPlat Code Coverage")
                        .SetLoggers($"console;verbosity=normal")
                        .SetResultsDirectory(CoverageDirectory);
                }

                DotNetTest(testSettings);

                Console.WriteLine("✅ All tests passed successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Test execution failed: {ex.Message}");
                throw;
            }
        });

    Target Run => _ => _
        .DependsOn(Build)
        .Executes(() =>
        {
            if (MainProject == null)
            {
                throw new InvalidOperationException("Main project (AGI.Captor.Desktop) not found");
            }

            DotNetRun(s => s
                .SetProjectFile(MainProject)
                .SetConfiguration(Configuration));
        });

    Target Publish => _ => _
        .Description("Publish binaries")
        .DependsOn(Test)
        .Executes(() =>
        {
            var publishDir = RootDirectory / "artifacts" / "publish";
            DotNetPublish(s => s
                .SetProject(MainProject)
                .SetConfiguration(Configuration)
                .EnableNoRestore()
                .SetOutput(publishDir));
        });

    Target Info => _ => _
        .Executes(() =>
        {
            Console.WriteLine("🔍 Build Environment Information");
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            Console.WriteLine($"Configuration: {Configuration}");
            Console.WriteLine($"Current Platform: {CurrentRuntimeIdentifier}");
            Console.WriteLine($"Target Framework: {GetTargetFramework()}");
            Console.WriteLine($"Self-contained: {SelfContained}");
            Console.WriteLine($"Single File: {SingleFile}");
            Console.WriteLine($"Trim: {Trim}");
            Console.WriteLine($"Skip Tests: {SkipTests}");
            Console.WriteLine($"Coverage: {Coverage}");

            if (!string.IsNullOrWhiteSpace(TestFilter))
                Console.WriteLine($"Test Filter: {TestFilter}");

            if (!string.IsNullOrWhiteSpace(Rids))
                Console.WriteLine($"Target Runtimes: {Rids}");
            else
                Console.WriteLine($"Default Runtime: {CurrentRuntimeIdentifier}");

            Console.WriteLine();
            Console.WriteLine("📁 Paths:");
            Console.WriteLine($"Root: {RootDirectory}");
            Console.WriteLine($"Artifacts: {ArtifactsDirectory}");
            Console.WriteLine($"Publish: {PublishDirectory}");
            Console.WriteLine($"Test Results: {TestResultsDirectory}");

            if (MainProject != null)
            {
                Console.WriteLine();
                Console.WriteLine("📋 Projects:");
                Console.WriteLine($"Main: {MainProject.Name} ({MainProject.Path})");

                if (TestProjects.Any())
                {
                    Console.WriteLine("Test Projects:");
                    foreach (var test in TestProjects)
                        Console.WriteLine($"  - {test.Name}");
                }
            }

            var version = GetFixedVersionOrFallback();
            Console.WriteLine();
            Console.WriteLine("🏷️ Version Information:");
            Console.WriteLine($"Display: {version.Display}");
            Console.WriteLine($"Assembly: {version.Assembly}");
            Console.WriteLine($"File: {version.File}");
            Console.WriteLine($"Informational: {version.Info}");

            Console.WriteLine();
            Console.WriteLine("✅ Environment check completed!");
        });

    [Parameter("Code signing identity for macOS (Developer ID Application: Your Name)")]
    readonly string MacSigningIdentity;

    [Parameter("Apple ID for notarization")]
    readonly string AppleId;

    [Parameter("App-specific password for notarization")]
    readonly string AppPassword;

    [Parameter("Apple Team ID for notarization")]
    readonly string TeamId;

    [Parameter("macOS package formats to create (PKG,DMG,AppStore)")]
    readonly string MacOSFormats = "PKG,DMG";

    [Parameter("Windows code signing certificate thumbprint")]
    readonly string WindowsSigningThumbprint;

    [Parameter("Windows code signing certificate password")]
    readonly string WindowsSigningPassword;

    // Packaging paths
    AbsolutePath PackagingDirectory => RootDirectory / "packaging";
    AbsolutePath WindowsPackagingDirectory => PackagingDirectory / "windows";
    AbsolutePath MacPackagingDirectory => PackagingDirectory / "macos";
    AbsolutePath LinuxPackagingDirectory => PackagingDirectory / "linux";
    AbsolutePath PackageOutputDirectory => ArtifactsDirectory / "packages";

    Target Package => _ => _
        .DependsOn(Publish, CheckVersionLocked)
        .Produces(PackageOutputDirectory / "*")
        .Executes(() =>
        {
            if (Directory.Exists(PackageOutputDirectory))
                Directory.Delete(PackageOutputDirectory, true);
            Directory.CreateDirectory(PackageOutputDirectory);

            foreach (var rid in PublishRuntimeIdentifiers)
            {
                var publishPath = PublishDirectory / rid;
                if (!Directory.Exists(publishPath))
                {
                    Console.WriteLine($"⚠️ Skipping {rid} - publish directory not found: {publishPath}");
                    continue;
                }

                Console.WriteLine($"📦 Creating package for {rid}...");

                switch (rid)
                {
                    case "win-x64":
                    case "win-arm64":
                        CreateWindowsPackage(publishPath, rid);
                        break;

                    case "osx-x64":
                    case "osx-arm64":
                        CreateMacPackage(publishPath, rid);
                        break;

                    case "linux-x64":
                    case "linux-arm64":
                        CreateLinuxPackage(publishPath, rid);
                        break;

                    default:
                        Console.WriteLine($"⚠️ No packaging strategy for {rid}");
                        break;
                }
            }

            Console.WriteLine("✅ Package creation completed!");
        });

    void CreateWindowsPackage(AbsolutePath publishPath, string rid)
    {
        try
        {
            var version = GetFixedVersionOrFallback();
            var packageName = $"AGI.Captor-{version.File}-{rid}.msi";
            var packagePath = PackageOutputDirectory / packageName;

            Console.WriteLine($"🪟 Creating Windows MSI package: {packageName}");

            // Check if WiX v4+ is available
            string wixPath = null;
            try
            {
                // Try to find wix.exe (WiX v4+)
                var process = ProcessTasks.StartProcess("wix", "--version", logOutput: false);
                process.AssertZeroExitCode();
                wixPath = "wix";
            }
            catch
            {
                try
                {
                    // Fallback to older WiX toolset
                    wixPath = ToolPathResolver.GetPathExecutable("heat") ??
                             ToolPathResolver.GetPathExecutable("candle");
                }
                catch
                {
                    // WiX not available
                }
            }

            if (wixPath == null)
            {
                Console.WriteLine("⚠️ WiX Toolset not found. Creating portable ZIP instead...");
                CreatePortableZip(publishPath, rid, version.File);
                return;
            }

            // Use WiX v4+ syntax
            if (wixPath == "wix")
            {
                CreateMsiWithWixV4(publishPath, rid, packagePath, (version.Assembly, version.File, version.Info));
            }
            else
            {
                Console.WriteLine("⚠️ WiX v3 detected but v4+ required for MSI creation. Creating portable ZIP...");
                CreatePortableZip(publishPath, rid, version.File);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Windows package creation failed: {ex.Message}");
            // Fallback to portable ZIP
            var version = GetFixedVersionOrFallback();
            CreatePortableZip(publishPath, rid, version.File);
        }
    }

    void CreateMsiWithWixV4(AbsolutePath publishPath, string rid, AbsolutePath packagePath, (string AssemblyVersion, string FileVersion, string InformationalVersion) version)
    {
        var wxsFile = WindowsPackagingDirectory / "AGI.Captor.v4.wxs";
        var wixobjFile = WindowsPackagingDirectory / "AGI.Captor.wixobj";

        // Ensure WXS file exists
        if (!File.Exists(wxsFile))
        {
            Console.WriteLine($"❌ WiX source file not found: {wxsFile}");
            CreatePortableZip(publishPath, rid, version.FileVersion);
            return;
        }

        try
        {
            Console.WriteLine($"🔨 Compiling WiX source...");

            // Step 1: Compile .wxs to .wixobj
            var compileArgs = new List<string>
            {
                "build",
                "-arch", rid == "win-arm64" ? "arm64" : "x64",
                "-define", $"SourceDir={publishPath}",
                "-define", $"ProductVersion={version.FileVersion}",
                "-out", packagePath,
                wxsFile
            };

            // Create ProcessStartInfo for safer argument handling
            var processInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "wix",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            // Add arguments safely without manual escaping using foreach for better readability
            var wixArgs = new string[]
            {
                "build",
                "-arch",
                rid == "win-arm64" ? "arm64" : "x64",
                "-define",
                $"SourceDir={publishPath}",
                "-define",
                $"ProductVersion={version.FileVersion}",
                "-out",
                packagePath,
                wxsFile
            };

            foreach (var arg in wixArgs)
            {
                processInfo.ArgumentList.Add(arg);
            }

            using var process = System.Diagnostics.Process.Start(processInfo);
            if (process == null)
                throw new InvalidOperationException("Failed to start WiX process");

            // Read outputs to prevent deadlock
            var outputBuilder = new System.Text.StringBuilder();
            var errorBuilder = new System.Text.StringBuilder();

            process.OutputDataReceived += (sender, e) => { if (e.Data != null) outputBuilder.AppendLine(e.Data); };
            process.ErrorDataReceived += (sender, e) => { if (e.Data != null) errorBuilder.AppendLine(e.Data); };

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                var error = errorBuilder.ToString();
                throw new InvalidOperationException($"WiX process failed with exit code {process.ExitCode}: {error}");
            }

            Console.WriteLine($"✅ Created: {packagePath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ WiX compilation failed: {ex.Message}");
            CreatePortableZip(publishPath, rid, version.FileVersion);
        }
    }

    void CreatePortableZip(AbsolutePath publishPath, string rid, string version)
    {
        var zipName = $"AGI.Captor-{version}-{rid}-portable.zip";
        var zipPath = PackageOutputDirectory / zipName;

        Console.WriteLine($"📁 Creating portable ZIP: {zipName}");

        if (File.Exists(zipPath))
            File.Delete(zipPath);

        System.IO.Compression.ZipFile.CreateFromDirectory(
            publishPath,
            zipPath,
            System.IO.Compression.CompressionLevel.Optimal,
            false);

        Console.WriteLine($"✅ Created: {zipPath}");
    }

    void CreateMacPackage(AbsolutePath publishPath, string rid)
    {
        try
        {
                var version = GetFixedVersionOrFallback();
            var requestedFormats = MacOSFormats?.Split(',')
                .Select(f => f.Trim().ToUpperInvariant())
                .ToArray() ?? new[] { "PKG", "DMG" };

            Console.WriteLine($"🍎 Creating macOS packages for {rid}...");
            Console.WriteLine($"   Requested formats: {string.Join(", ", requestedFormats)}");

            // Create traditional PKG and DMG
            if (requestedFormats.Contains("PKG") || requestedFormats.Contains("DMG"))
            {
                var standardScript = MacPackagingDirectory / "create-pkg.sh";
                if (File.Exists(standardScript))
                {
                    Console.WriteLine("📦 Creating standard PKG and DMG packages...");

                        var args = $"{publishPath} {version.File}";
                    if (!string.IsNullOrWhiteSpace(MacSigningIdentity))
                        args += $" \"{MacSigningIdentity}\"";

                    using var process = ProcessTasks.StartProcess(
                        "bash",
                        $"{standardScript} {args}",
                        MacPackagingDirectory);

                    process.AssertZeroExitCode();

                    // Move packages to output directory
                    if (requestedFormats.Contains("PKG"))
                    {
                            var pkgPattern = $"AGI.Captor-{version.File}.pkg";
                        foreach (var file in Directory.GetFiles(MacPackagingDirectory, pkgPattern))
                        {
                            var targetPath = PackageOutputDirectory / Path.GetFileName(file);
                            File.Move(file, targetPath);
                            Console.WriteLine($"✅ Created: {targetPath}");
                        }
                    }

                    if (requestedFormats.Contains("DMG"))
                    {
                            var dmgPattern = $"AGI.Captor-{version.File}.dmg";
                        foreach (var file in Directory.GetFiles(MacPackagingDirectory, dmgPattern))
                        {
                            var targetPath = PackageOutputDirectory / Path.GetFileName(file);
                            File.Move(file, targetPath);
                            Console.WriteLine($"✅ Created: {targetPath}");
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"⚠️ Standard macOS packaging script not found: {standardScript}");
                }
            }

            // Create App Store version
            if (requestedFormats.Contains("APPSTORE"))
            {
                var appStoreScript = MacPackagingDirectory / "create-appstore.sh";
                if (File.Exists(appStoreScript))
                {
                    if (string.IsNullOrWhiteSpace(MacSigningIdentity))
                    {
                        Console.WriteLine("⚠️ App Store packaging requires signing identity, skipping...");
                    }
                    else
                    {
                        Console.WriteLine("🏪 Creating App Store package...");

                            var args = $"{publishPath} {version.File} \"{MacSigningIdentity}\"";

                        using var process = ProcessTasks.StartProcess(
                            "bash",
                            $"{appStoreScript} {args}",
                            MacPackagingDirectory);

                        process.AssertZeroExitCode();

                        // Move App Store package to output directory
                            var appStorePkgPattern = $"AGI.Captor-{version.File}-AppStore.pkg";
                        foreach (var file in Directory.GetFiles(MacPackagingDirectory, appStorePkgPattern))
                        {
                            var targetPath = PackageOutputDirectory / Path.GetFileName(file);
                            File.Move(file, targetPath);
                            Console.WriteLine($"✅ Created: {targetPath}");
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"⚠️ App Store packaging script not found: {appStoreScript}");
                }
            }

            // Run notarization if credentials provided and not App Store only
            if (!string.IsNullOrWhiteSpace(AppleId) &&
                !string.IsNullOrWhiteSpace(AppPassword) &&
                !string.IsNullOrWhiteSpace(TeamId) &&
                (requestedFormats.Contains("PKG") || requestedFormats.Contains("DMG")))
            {
                Console.WriteLine("🔐 Starting notarization process...");
                    RunMacNotarization(version.File);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ macOS package creation failed: {ex.Message}");
                var version = GetFixedVersionOrFallback();
                CreatePortableZip(publishPath, rid, version.File);
        }
    }

    void RunMacNotarization(string version)
    {
        var scriptPath = MacPackagingDirectory / "notarize.sh";
        if (!File.Exists(scriptPath))
        {
            Console.WriteLine("⚠️ Notarization script not found");
            return;
        }

        var pkgFile = PackageOutputDirectory / $"AGI.Captor-{version}.pkg";
        if (File.Exists(pkgFile))
        {
            var args = $"{pkgFile} {AppleId} {AppPassword} {TeamId}";

            using var process = ProcessTasks.StartProcess(
                "bash",
                $"{scriptPath} {args}",
                MacPackagingDirectory);

            process.AssertZeroExitCode();
        }
    }

    void CreateLinuxPackage(AbsolutePath publishPath, string rid)
    {
        try
        {
            var version = GetFixedVersionOrFallback();
            var arch = rid.Contains("arm") ? "arm64" : "amd64";

            Console.WriteLine($"🐧 Creating Linux packages for {rid}...");

            // Create DEB package
            var debScript = LinuxPackagingDirectory / "create-deb.sh";
            if (File.Exists(debScript))
            {
                using var debProcess = ProcessTasks.StartProcess(
                    "bash",
                    $"{debScript} {publishPath} {version.File} {arch}",
                    LinuxPackagingDirectory);

                debProcess.AssertZeroExitCode();

                // Move DEB to output directory
                var debPattern = $"agi-captor_{version.File}_{arch}.deb";
                foreach (var file in Directory.GetFiles(LinuxPackagingDirectory, debPattern))
                {
                    var targetPath = PackageOutputDirectory / Path.GetFileName(file);
                    File.Move(file, targetPath);
                    Console.WriteLine($"✅ Created: {targetPath}");
                }
            }

            // Create RPM package  
            var rpmScript = LinuxPackagingDirectory / "create-rpm.sh";
            if (File.Exists(rpmScript))
            {
                var rpmArch = rid.Contains("arm") ? "aarch64" : "x86_64";

                using var rpmProcess = ProcessTasks.StartProcess(
                    "bash",
                    $"{rpmScript} {publishPath} {version.File} {rpmArch}",
                    LinuxPackagingDirectory);

                rpmProcess.AssertZeroExitCode();

                // Move RPM to output directory
                var rpmPattern = $"agi-captor-{version.File}-1.{rpmArch}.rpm";
                foreach (var file in Directory.GetFiles(LinuxPackagingDirectory, rpmPattern))
                {
                    var targetPath = PackageOutputDirectory / Path.GetFileName(file);
                    File.Move(file, targetPath);
                    Console.WriteLine($"✅ Created: {targetPath}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Linux package creation failed: {ex.Message}");
            var version = GetFixedVersionOrFallback();
            CreatePortableZip(publishPath, rid, version.File);
        }
    }

    (string Display, string Assembly, string File, string Info) GetFixedVersionOrFallback()
    {
        if (_versionCache.HasValue) return _versionCache.Value;

        if (File.Exists(VersionFile))
        {
            try
            {
                var json = JsonDocument.Parse(File.ReadAllText(VersionFile));
                var display = json.RootElement.GetProperty("version").GetString();
                if (!string.IsNullOrWhiteSpace(display))
                {
                    _versionCache = (display, display, display, display);
                    Console.WriteLine($"ℹ️ Using version.json: {display}");
                    return _versionCache.Value;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Failed to parse version.json: {ex.Message}");
            }
        }

        if (MainProject != null)
        {
            try
            {
                var v = MainProject.GetProperty("Version");
                if (!string.IsNullOrWhiteSpace(v))
                {
                    _versionCache = (v, v, v, v);
                    Console.WriteLine($"ℹ️ Using project Version: {v}");
                    return _versionCache.Value;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Failed to read project Version: {ex.Message}");
            }
        }

        var fallback = GenerateTimestampVersion();
        _versionCache = (fallback, fallback, fallback, fallback);
        Console.WriteLine($"ℹ️ Using generated version: {fallback}");
        return _versionCache.Value;
    }

    string GenerateTimestampVersion()
    {
        var utc = DateTime.UtcNow;
        return $"{utc:yyyy}.{utc.Month}.{utc.Day}.{utc:HHmm}";
    }

    bool IsValidDisplayVersion(string v)
        => System.Text.RegularExpressions.Regex.IsMatch(v ?? string.Empty, "^\\d{4}\\.[1-9]\\d?\\.[1-9]\\d?\\.[0-2]\\d[0-5]\\d$");

    (string Display, string Assembly, string File, string Info) BuildVersionModel(string display)
        => (display, display, display, display);

    void WriteVersionResources((string Display, string Assembly, string File, string Info) model)
    {
        var payload = new
        {
            version = model.Display,
            assemblyVersion = model.Assembly,
            fileVersion = model.File,
            informationalVersion = model.Info,
            generatedUtc = DateTime.UtcNow.ToString("O")
        };
        System.IO.File.WriteAllText(VersionFile,
            JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));

        if (MainProject != null && System.IO.File.Exists(MainProject.Path))
        {
            var xdoc = XDocument.Load(MainProject.Path);
            var ns = xdoc.Root?.Name.Namespace ?? XNamespace.None;
            XElement Ensure(string name)
            {
                var el = xdoc.Descendants(ns + name).FirstOrDefault();
                if (el != null) return el;
                var pg = xdoc.Root!.Elements(ns + "PropertyGroup").FirstOrDefault();
                if (pg == null) { pg = new XElement(ns + "PropertyGroup"); xdoc.Root!.Add(pg); }
                el = new XElement(ns + name);
                pg.Add(el);
                return el;
            }
            Ensure("Version").Value = model.Display;
            Ensure("AssemblyVersion").Value = model.Assembly;
            Ensure("FileVersion").Value = model.File;
            Ensure("InformationalVersion").Value = model.Info;
            xdoc.Save(MainProject.Path);
        }
        Console.WriteLine($"📝 Locked version: {model.Display}");
    }

    Target UpgradeVersion => _ => _
        .Description("Lock a new timestamp version or use --NewVersion")
        .Executes(() =>
        {
            var display = !string.IsNullOrWhiteSpace(NewVersion) ? NewVersion : GenerateTimestampVersion();
            
            if (!IsValidDisplayVersion(display))
                throw new Exception($"Invalid version format: {display}");

            var model = BuildVersionModel(display);
            WriteVersionResources(model);
        });

    Target CheckVersionLocked => _ => _
        .Description("Validate locked version file exists and is consistent")
        .Executes(() =>
        {
            if (!File.Exists(VersionFile))
                throw new Exception("version.json not found. Run 'nuke UpgradeVersion' first.");

            try
            {
                var json = JsonDocument.Parse(File.ReadAllText(VersionFile));
                var display = json.RootElement.GetProperty("version").GetString();

                if (string.IsNullOrWhiteSpace(display) || !IsValidDisplayVersion(display))
                    throw new Exception($"Invalid version in version.json: {display}");

                if (MainProject != null && File.Exists(MainProject.Path))
                {
                    var xdoc = XDocument.Load(MainProject.Path);
                    var ns = xdoc.Root?.Name.Namespace ?? XNamespace.None;
                    var projectVersion = xdoc.Descendants(ns + "Version").FirstOrDefault()?.Value;
                    
                    if (projectVersion != display)
                        throw new Exception($"Version mismatch: version.json={display}, project={projectVersion}");
                }

                Console.WriteLine($"✅ Version validated: {display}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Version validation failed: {ex.Message}");
            }
        });
}
