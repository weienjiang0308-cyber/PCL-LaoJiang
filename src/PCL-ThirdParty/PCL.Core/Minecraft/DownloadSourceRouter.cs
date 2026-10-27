using System;
using System.Collections.Generic;
using System.Linq;

namespace PCL.Core.Minecraft;

/// <summary>Maps only known public artifact hosts; authentication URLs are never rewritten.</summary>
public static class DownloadSourceRouter
{
    public const string MirrorHost = "bmclapi2.bangbang93.com";

    public static string? GetMirrorUrl(string original)
    {
        if (!Uri.TryCreate(original, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) ||
            !uri.IsDefaultPort || !string.IsNullOrEmpty(uri.UserInfo)) return null;
        var prefix = uri.Host.ToLowerInvariant() switch
        {
            "resources.download.minecraft.net" => "/assets",
            "libraries.minecraft.net" => "/maven",
            "maven.fabricmc.net" => "/maven",
            "maven.minecraftforge.net" => "/maven",
            "files.minecraftforge.net" when uri.AbsolutePath.StartsWith("/maven/", StringComparison.Ordinal) => "",
            "maven.neoforged.net" when uri.AbsolutePath.StartsWith("/releases/", StringComparison.Ordinal) => "/maven",
            "meta.fabricmc.net" => "/fabric-meta",
            "piston-meta.mojang.com" or "piston-data.mojang.com" or
            "launchermeta.mojang.com" or "launcher.mojang.com" => "",
            _ => null
        };
        if (prefix is null) return null;
        var path = uri.Host == "maven.neoforged.net" ? uri.AbsolutePath["/releases".Length..] : uri.AbsolutePath;
        return "https://" + MirrorHost + prefix + path + uri.Query;
    }

    /// <param name="selection">0: BMCLAPI then official; 1: official then BMCLAPI; 2: official only.</param>
    public static IEnumerable<string> GetUrls(string original, int selection)
    {
        var mirror = GetMirrorUrl(original);
        if (mirror is null || selection == 2) return [original];
        return (selection == 0 ? new[] { mirror, original } : new[] { original, mirror })
            .Distinct(StringComparer.Ordinal);
    }

    public static string GetSourceName(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return "Unknown";
        return uri.Host switch
        {
            MirrorHost => "BMCLAPI community mirror",
            "piston-meta.mojang.com" or "piston-data.mojang.com" or "launchermeta.mojang.com" or
            "launcher.mojang.com" or "libraries.minecraft.net" or "resources.download.minecraft.net" => "Mojang official",
            "cdn.modrinth.com" or "api.modrinth.com" => "Modrinth",
            "edge.forgecdn.net" or "mediafilez.forgecdn.net" or "api.curseforge.com" => "CurseForge",
            "meta.fabricmc.net" or "maven.fabricmc.net" => "Fabric official",
            _ => uri.Host
        };
    }
}
