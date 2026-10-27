using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft;

namespace PCL.Core.Test;

[TestClass]
public class ThirdPartySourceTests
{
    [TestMethod]
    public void AuthenticationAndLookalikeHostsMustNeverBeMapped()
    {
        foreach (var url in new[] {
            "https://login.microsoftonline.com/consumers/oauth2/v2.0/token",
            "https://api.minecraftservices.com/authentication/login_with_xbox",
            "https://piston-data.mojang.com.attacker.invalid/client.jar",
            "https://piston-data.mojang.com@attacker.invalid/client.jar",
            "https://attacker@piston-data.mojang.com/client.jar",
            "https://piston-data.mojang.com:444/client.jar",
            "file:///D:/sample.jar"
        }) Assert.IsNull(DownloadSourceRouter.GetMirrorUrl(url), url);
    }

    [TestMethod]
    public void MirrorMappingsMustPreserveResourcePaths()
    {
        Assert.AreEqual("https://bmclapi2.bangbang93.com/assets/ab/abcdef", DownloadSourceRouter.GetMirrorUrl("https://resources.download.minecraft.net/ab/abcdef"));
        Assert.AreEqual("https://bmclapi2.bangbang93.com/maven/net/fabricmc/a.jar", DownloadSourceRouter.GetMirrorUrl("https://maven.fabricmc.net/net/fabricmc/a.jar"));
        Assert.AreEqual("https://bmclapi2.bangbang93.com/maven/net/neoforged/a.jar", DownloadSourceRouter.GetMirrorUrl("https://maven.neoforged.net/releases/net/neoforged/a.jar"));
        Assert.IsNull(DownloadSourceRouter.GetMirrorUrl("https://maven.neoforged.net/other/a.jar"));
    }

    [TestMethod]
    public void ProviderSelectionMustBeHonored()
    {
        const string original = "https://piston-data.mojang.com/v1/objects/hash/client.jar";
        CollectionAssert.AreEqual(new[] { original }, DownloadSourceRouter.GetUrls(original, 2).ToArray());
        Assert.AreEqual(original, DownloadSourceRouter.GetUrls(original, 1).First());
        Assert.AreEqual("bmclapi2.bangbang93.com", new System.Uri(DownloadSourceRouter.GetUrls(original, 0).First()).Host);
        CollectionAssert.AreEqual(new[] { "https://cdn.modrinth.com/data/file.jar" }, DownloadSourceRouter.GetUrls("https://cdn.modrinth.com/data/file.jar", 0).ToArray());
    }
}
