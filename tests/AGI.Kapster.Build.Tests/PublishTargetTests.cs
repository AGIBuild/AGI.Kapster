using System.IO;
using Xunit;

namespace AGI.Kapster.Build.Tests;

public class PublishTargetTests
{
    [Fact]
    public void PublishUsesSelfExtractPropertiesWhenSingleFileEnabled()
    {
        // Locate repository root by walking up from current directory until we find build/BuildTasks.cs
        string FindBuildScript()
        {
            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            for (int i = 0; i < 10 && dir != null; i++)
            {
                var candidate = Path.Combine(dir.FullName, "build", "BuildTasks.cs");
                if (File.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }
            return null;
        }

        var path = FindBuildScript();
        Assert.True(path != null, "Build script not found in repository tree");
        var txt = File.ReadAllText(path);

        // Ensure the publish code sets IncludeNativeLibrariesForSelfExtract when single-file
        Assert.Contains("IncludeNativeLibrariesForSelfExtract", txt);
        Assert.Contains("IncludeAllContentForSelfExtract", txt);

        // Ensure we did not leave process-kill logic in the publish target
        Assert.DoesNotContain("GetProcessesByName(\"AGI.Kapster.Desktop\")", txt);
        Assert.DoesNotContain("p.Kill()", txt);
    }
}
