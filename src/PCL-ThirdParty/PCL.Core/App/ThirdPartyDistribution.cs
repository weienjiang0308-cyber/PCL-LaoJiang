using System;
using System.IO;

namespace PCL.Core.App;

/// <summary>Independent distribution identity and process-local configuration.</summary>
public static class ThirdPartyDistribution
{
    public const string Name = "Plain Craft Launcher (PCL) 老蒋定制版";
    public const string ShortName = "PCL老蒋定制版";
    public const string DisplayVersion = "P1";
    public const string UserAgent = "PCL-LaoJiang/1.0.0 (P1; independent-community-fork)";
    public static string DataRoot { get; } = ResolveDataRoot();
    public static bool GameDownloadsEnabled =>
        !string.Equals(Environment.GetEnvironmentVariable("PCL_THIRDPARTY_DISABLE_GAME_DOWNLOADS"), "1", StringComparison.Ordinal);

    public static string ReadCredential(string name) =>
        Environment.GetEnvironmentVariable("PCL_THIRDPARTY_" + name)?.Trim() ?? string.Empty;

    private static string ResolveDataRoot()
    {
        var configured = Environment.GetEnvironmentVariable("PCL_THIRDPARTY_DATA_ROOT");
        var directory = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(AppContext.BaseDirectory, "ThirdPartyData")
            : configured;
        return Path.GetFullPath(directory, AppContext.BaseDirectory);
    }
}
