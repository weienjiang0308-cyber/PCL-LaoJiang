using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.UI;

namespace PCL;

internal static class RestrictedFeaturePolicy
{
    // Third-party modification: capability is independent of language and Windows region.
    internal static bool IsRestricted => !ThirdPartyDistribution.GameDownloadsEnabled;

    internal static bool Check(string feature, bool showHint = true)
    {
        if (!IsRestricted) return true;

        ModBase.Log($"[ThirdParty] Downloads disabled by project configuration: {feature}");
        if (showHint)
        {
            HintService.Hint(Lang.Text("ThirdParty.Downloads.Disabled"), HintType.Error);
        }
        return false;
    }

    internal static void ThrowIfRestricted(string feature)
    {
        if (Check(feature, false)) return;
        throw new ModBase.CancelledException();
    }
}
