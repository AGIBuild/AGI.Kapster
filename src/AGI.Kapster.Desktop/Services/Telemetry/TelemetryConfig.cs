using System;
using System.Text;

namespace AGI.Kapster.Desktop.Services.Telemetry;

/// <summary>
/// Telemetry configuration with basic obfuscation
/// The connection string is injected at build time via MSBuild property
/// </summary>
internal static class TelemetryConfig
{
    // Connection string is set via MSBuild DefineConstants at build time
    // In source code, this is an empty placeholder
    // During CI/CD build, it's replaced with the actual obfuscated value
    private const string ObfuscatedConnectionString = "";

    /// <summary>
    /// Gets the Application Insights connection string
    /// Returns null if not configured
    /// </summary>
    public static string? GetConnectionString()
    {
        // First check environment variable (highest priority, useful for development)
        var envValue = Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING");
        if (!string.IsNullOrWhiteSpace(envValue))
        {
            return envValue;
        }

        // Then use the embedded obfuscated value
        if (string.IsNullOrWhiteSpace(ObfuscatedConnectionString))
        {
            return null;
        }

        return Deobfuscate(ObfuscatedConnectionString);
    }

    /// <summary>
    /// Obfuscates a connection string for embedding in source code
    /// This is NOT encryption - just makes casual inspection harder
    /// </summary>
    public static string Obfuscate(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return string.Empty;

        var bytes = Encoding.UTF8.GetBytes(plainText);
        // Simple XOR with rotating key + Base64
        var key = GetObfuscationKey();
        for (int i = 0; i < bytes.Length; i++)
        {
            bytes[i] ^= key[i % key.Length];
        }
        return Convert.ToBase64String(bytes);
    }

    /// <summary>
    /// Deobfuscates the connection string
    /// </summary>
    private static string Deobfuscate(string obfuscated)
    {
        if (string.IsNullOrEmpty(obfuscated)) return string.Empty;

        try
        {
            var bytes = Convert.FromBase64String(obfuscated);
            var key = GetObfuscationKey();
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] ^= key[i % key.Length];
            }
            return Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Gets the obfuscation key
    /// Must match the key used in GitHub workflow for build-time injection
    /// </summary>
    private static byte[] GetObfuscationKey()
    {
        // Fixed key derived from application identity
        // This is NOT security - just makes casual inspection harder
        const string keySource = "AGI.Kapster.Telemetry.2024";
        return Encoding.UTF8.GetBytes(keySource);
    }
}
