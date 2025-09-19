using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.GitVersion;
using Nuke.Common.Tooling;
using static Nuke.Common.EnvironmentInfo;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

class BuildTasks : NukeBuild
{
    /// <summary>
    /// AGI.Captor Cross-platform Build System
    /// Built with Nuke based on industry best practices
    /// </summary>
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
    [GitVersion(NoFetch = true, NoCache = true)] GitVersion GitVersion;

    // Ensure GitVersion is injected correctly; provide fallback if not.
    GitVersion GetGitVersionOrFallback()
    {
        if (GitVersion != null)
        {
            return GitVersion;
        }

        Console.WriteLine("⚠️ GitVersion injection failed. Using fallback version calculation.");
        return null;
    }

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

    /// <summary>
    /// Get target framework with safe fallback
    /// </summary>
    string GetTargetFramework()
    {
        if (!string.IsNullOrWhiteSpace(Framework))
            return Framework;

        try
        {
            return MainProject?.GetProperty("TargetFramework") ??
                   MainProject?.GetProperty("TargetFrameworks")?.Split(';').First() ??
                   "net9.0"; // 与主项目保持一致的默认值
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Failed to read target framework: {ex.Message}. Using default: net9.0");
            return "net9.0";
        }
    }

    /// <summary>
    /// Get version information from GitVersion with fallback support
    /// </summary>
    (string AssemblyVersion, string FileVersion, string InformationalVersion) GetVersionInfo()
    {
        var gitVersion = GetGitVersionOrFallback();
        
        if (gitVersion != null)
        {
            return (
                gitVersion.AssemblySemVer ?? "1.0.0.0",
                gitVersion.AssemblySemFileVer ?? "1.0.0.0",
                gitVersion.InformationalVersion ?? "1.0.0+local"
            );
        }

        // Fallback version when GitVersion fails
        var fallbackVersion = "1.3.0";
        var buildNumber = Environment.GetEnvironmentVariable("GITHUB_RUN_NUMBER") ?? "0";
        var sha = Environment.GetEnvironmentVariable("GITHUB_SHA")?.Substring(0, 7) ?? "local";
        
        Console.WriteLine($"📋 Using fallback version: {fallbackVersion}.{buildNumber}+{sha}");
        
        return (
            $"{fallbackVersion}.0",
            $"{fallbackVersion}.{buildNumber}",
            $"{fallbackVersion}.{buildNumber}+{sha}"
        );
    }

    /// <summary>
    /// Clean artifacts and bin/obj directories
    /// </summary>
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

    /// <summary>
    /// Restore NuGet packages
    /// </summary>
    Target Restore => _ => _
        .After(Clean)
        .Executes(() =>
        {
            DotNetRestore(s => s
                .SetProjectFile(Solution));
        });

    /// <summary>
    /// Build the solution
    /// </summary>
    Target Build => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            var version = GetVersionInfo();

            DotNetBuild(s => s
                .SetProjectFile(Solution)
                .SetConfiguration(Configuration)
                .SetAssemblyVersion(version.AssemblyVersion)
                .SetFileVersion(version.FileVersion)
                .SetInformationalVersion(version.InformationalVersion)
                .EnableNoRestore());

            Console.WriteLine($"✅ Build completed - Version: {version.InformationalVersion}");
        });

    /// <summary>
    /// Run unit tests (excluding UI tests)
    /// </summary>
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

    /// <summary>
    /// Run the main application
    /// </summary>
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

    /// <summary>
    /// Publish applications for specified runtime identifiers
    /// </summary>
    Target Publish => _ => _
        .DependsOn(SkipTests ? Build : Test)
        .Executes(() =>
        {
            if (MainProject == null)
            {
                throw new InvalidOperationException("Main project (AGI.Captor.Desktop) not found");
            }

            var version = GetVersionInfo();

            foreach (var rid in PublishRuntimeIdentifiers)
            {
                var outputDir = PublishDirectory / rid;
                outputDir.CreateOrCleanDirectory();

                Console.WriteLine($"📦 Publishing for {rid}...");

                try
                {
                    // 检查项目是否启用了AOT编译
                    var publishAot = MainProject?.GetProperty("PublishAot")?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;
                    var effectiveTrim = publishAot || Trim; // AOT编译时必须启用Trim

                    if (publishAot && !Trim)
                    {
                        Console.WriteLine($"⚠️  AOT compilation enabled, trimming will be automatically enabled for {rid}");
                    }

                    DotNetPublish(s => s
                        .SetProject(MainProject)
                        .SetConfiguration(Configuration)
                        .SetFramework(TargetFramework)
                        .SetRuntime(rid)
                        .SetSelfContained(SelfContained)
                        .SetPublishSingleFile(SingleFile)
                        .SetPublishTrimmed(effectiveTrim)
                        .SetOutput(outputDir)
                        .SetAssemblyVersion(version.AssemblyVersion)
                        .SetFileVersion(version.FileVersion)
                        .SetInformationalVersion(version.InformationalVersion)
                        .EnableNoRestore());

                    Console.WriteLine($"✅ Published {rid} to: {outputDir}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Failed to publish {rid}: {ex.Message}");
                    throw;
                }
            }

            Console.WriteLine($"🎉 All publishing completed successfully! Check: {PublishDirectory}");
        });

    /// <summary>
    /// Display build environment information and diagnostics
    /// </summary>
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

            var version = GetVersionInfo();
            Console.WriteLine();
            Console.WriteLine("🏷️ Version Information:");
            Console.WriteLine($"Assembly: {version.AssemblyVersion}");
            Console.WriteLine($"File: {version.FileVersion}");
            Console.WriteLine($"Informational: {version.InformationalVersion}");

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

    /// <summary>
    /// Create installation packages for all supported platforms
    /// </summary>
    Target Package => _ => _
        .DependsOn(Publish)
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

    /// <summary>
    /// Create Windows MSI installer using WiX
    /// </summary>
    void CreateWindowsPackage(AbsolutePath publishPath, string rid)
    {
        try
        {
            var version = GetVersionInfo();
            var packageName = $"AGI.Captor-{version.FileVersion}-{rid}.msi";
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
                CreatePortableZip(publishPath, rid, version.FileVersion);
                return;
            }

            // Use WiX v4+ syntax
            if (wixPath == "wix")
            {
                CreateMsiWithWixV4(publishPath, rid, packagePath, version);
            }
            else
            {
                Console.WriteLine("⚠️ WiX v3 detected but v4+ required for MSI creation. Creating portable ZIP...");
                CreatePortableZip(publishPath, rid, version.FileVersion);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Windows package creation failed: {ex.Message}");
            // Fallback to portable ZIP
            var version = GetVersionInfo();
            CreatePortableZip(publishPath, rid, version.FileVersion);
        }
    }

    /// <summary>
    /// Create MSI using WiX v4+
    /// </summary>
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

            // Add arguments safely without manual escaping
            processInfo.ArgumentList.Add("build");
            processInfo.ArgumentList.Add("-arch");
            processInfo.ArgumentList.Add(rid == "win-arm64" ? "arm64" : "x64");
            processInfo.ArgumentList.Add("-define");
            processInfo.ArgumentList.Add($"SourceDir={publishPath}");
            processInfo.ArgumentList.Add("-define");
            processInfo.ArgumentList.Add($"ProductVersion={version.FileVersion}");
            processInfo.ArgumentList.Add("-out");
            processInfo.ArgumentList.Add(packagePath);
            processInfo.ArgumentList.Add(wxsFile);

            using var process = System.Diagnostics.Process.Start(processInfo);
            if (process == null)
                throw new InvalidOperationException("Failed to start WiX process");
            
            process.WaitForExit();
            
            if (process.ExitCode != 0)
            {
                var error = process.StandardError.ReadToEnd();
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

    /// <summary>
    /// Create portable ZIP package
    /// </summary>
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

    /// <summary>
    /// Create macOS packages based on specified formats
    /// </summary>
    void CreateMacPackage(AbsolutePath publishPath, string rid)
    {
        try
        {
            var version = GetVersionInfo();
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

                    var args = $"{publishPath} {version.FileVersion}";
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
                        var pkgPattern = $"AGI.Captor-{version.FileVersion}.pkg";
                        foreach (var file in Directory.GetFiles(MacPackagingDirectory, pkgPattern))
                        {
                            var targetPath = PackageOutputDirectory / Path.GetFileName(file);
                            File.Move(file, targetPath);
                            Console.WriteLine($"✅ Created: {targetPath}");
                        }
                    }

                    if (requestedFormats.Contains("DMG"))
                    {
                        var dmgPattern = $"AGI.Captor-{version.FileVersion}.dmg";
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

                        var args = $"{publishPath} {version.FileVersion} \"{MacSigningIdentity}\"";

                        using var process = ProcessTasks.StartProcess(
                            "bash",
                            $"{appStoreScript} {args}",
                            MacPackagingDirectory);

                        process.AssertZeroExitCode();

                        // Move App Store package to output directory
                        var appStorePkgPattern = $"AGI.Captor-{version.FileVersion}-AppStore.pkg";
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
                RunMacNotarization(version.FileVersion);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ macOS package creation failed: {ex.Message}");
            var version = GetVersionInfo();
            CreatePortableZip(publishPath, rid, version.FileVersion);
        }
    }

    /// <summary>
    /// Run macOS notarization process
    /// </summary>
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

    /// <summary>
    /// Create Linux DEB and RPM packages
    /// </summary>
    void CreateLinuxPackage(AbsolutePath publishPath, string rid)
    {
        try
        {
            var version = GetVersionInfo();
            var arch = rid.Contains("arm") ? "arm64" : "amd64";

            Console.WriteLine($"🐧 Creating Linux packages for {rid}...");

            // Create DEB package
            var debScript = LinuxPackagingDirectory / "create-deb.sh";
            if (File.Exists(debScript))
            {
                using var debProcess = ProcessTasks.StartProcess(
                    "bash",
                    $"{debScript} {publishPath} {version.FileVersion} {arch}",
                    LinuxPackagingDirectory);

                debProcess.AssertZeroExitCode();

                // Move DEB to output directory
                var debPattern = $"agi-captor_{version.FileVersion}_{arch}.deb";
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
                    $"{rpmScript} {publishPath} {version.FileVersion} {rpmArch}",
                    LinuxPackagingDirectory);

                rpmProcess.AssertZeroExitCode();

                // Move RPM to output directory
                var rpmPattern = $"agi-captor-{version.FileVersion}-1.{rpmArch}.rpm";
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
            var version = GetVersionInfo();
            CreatePortableZip(publishPath, rid, version.FileVersion);
        }
    }
}
