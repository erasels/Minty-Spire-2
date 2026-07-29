using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.sts2.Core.Nodes.TopBar;
using MegaCrit.Sts2.Core.Runs;

namespace MintySpire2.MintySpire2Code;

/**
 * Credits to kiooeht, this displays tooltips for each active ascension when hovering the player icon in the topbar.
 */

[HarmonyPatch]
public static class AscHoverTooltips
{
    private static List<IHoverTip> _myTips = [];

    [HarmonyPatch(typeof(NTopBarPortraitTip), nameof(NTopBarPortraitTip.Initialize))]
    public class InitializeTip
    {
        [HarmonyPostfix]
        public static void Init(IRunState runState, IHoverTip ____hoverTip)
        {
            _myTips.Add(____hoverTip);
            for (int i = 1; i <= runState.AscensionLevel; ++i)
            {
                _myTips.Add(new HoverTip(
                    AscensionHelper.GetTitle(i),
                    AscensionHelper.GetDescription(i)
                ));
            }
        }
    }

    [HarmonyPatch(typeof(NTopBarPortraitTip), "OnFocus")]
    public class ShowTip
    {
        [HarmonyPrefix]
        public static bool Prefix(NTopBarPortraitTip __instance)
        {
            if (!Config.AscHoverTooltip)
                return true;

            if (!__instance.ShowTip)
                return false;
            NHoverTipSet.CreateAndShow(__instance, _myTips).GlobalPosition = __instance.GlobalPosition + new Vector2(0, __instance.Size.Y + 20);

            return false;
        }
    }
}
