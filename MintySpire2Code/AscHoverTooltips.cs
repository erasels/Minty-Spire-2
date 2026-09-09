using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.sts2.Core.Nodes.TopBar;
using MegaCrit.Sts2.Core.Runs;
using MintySpire2.MintySpire2Code.config;

namespace MintySpire2.MintySpire2Code;

/**
 * Credits to kiooeht, this displays tooltips for each active ascension when hovering the player icon in the topbar.
 */

[HarmonyPatch]
public static class AscHoverTooltips
{
    private static readonly ConditionalWeakTable<NTopBarPortraitTip, List<IHoverTip>> CustomTips = [];

    [HarmonyPatch(typeof(NTopBarPortraitTip), nameof(NTopBarPortraitTip.Initialize))]
    public class InitializeTip
    {
        [HarmonyPostfix]
        public static void Init(NTopBarPortraitTip __instance, IRunState runState, IHoverTip ____hoverTip)
        {
            if (!__instance.ShowTip)
                return;

            List<IHoverTip> tips = [____hoverTip];
            for (int i = 1; i <= runState.AscensionLevel; ++i)
            {
                tips.Add(new HoverTip(
                    AscensionHelper.GetTitle(i),
                    AscensionHelper.GetDescription(i)
                ));
            }

            CustomTips.AddOrUpdate(__instance, tips);
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

            if (!CustomTips.TryGetValue(__instance, out var tips))
                return true;

            var tipSet = NHoverTipSet.CreateAndShow(__instance, tips);
            if (tipSet != null)
                tipSet.GlobalPosition = __instance.GlobalPosition + new Vector2(0, __instance.Size.Y + 20);

            return false;
        }
    }
}
