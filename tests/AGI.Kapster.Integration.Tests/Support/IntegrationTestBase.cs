using System;
using System.IO;

namespace AGI.Kapster.Integration.Build.Tests.Support
{
    /// <summary>
    /// IntegrationTestBase provides repository helper utilities for tests (locating files, reading text).
    /// Named to reflect that helpers are intended for integration-style tests.
    /// </summary>
    public abstract class IntegrationTestBase
    {
        protected string FindRepoFile(string fileName)
        {
            var dir = AppContext.BaseDirectory;
            for (int i = 0; i < 12; i++)
            {
                var candidate = Path.Combine(dir, fileName);
                if (File.Exists(candidate))
                    return Path.GetFullPath(candidate);

                var parent = Directory.GetParent(dir);
                if (parent == null)
                    break;
                dir = parent.FullName;
            }

            // As a last resort, try repository root relative locations
            var alt = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", fileName));
            if (File.Exists(alt)) return alt;

            throw new FileNotFoundException($"Could not locate '{fileName}' in repository from '{AppContext.BaseDirectory}'");
        }

        protected string ReadRepoFileText(string fileName)
        {
            var path = FindRepoFile(fileName);
            return File.ReadAllText(path);
        }

        protected string FindRepoRoot()
        {
            var dir = AppContext.BaseDirectory;
            for (int i = 0; i < 12; i++)
            {
                if (File.Exists(Path.Combine(dir, "AGI.Kapster.sln")))
                    return dir;

                var parent = Directory.GetParent(dir);
                if (parent == null)
                    break;
                dir = parent.FullName;
            }

            // As a last resort
            return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        }
    }
}
