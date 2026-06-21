using Godot;
using HarmonyLib;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;

namespace MintySpire2.MintySpire2Code;

/**
 * Credits to Ind-E, displays a small remaining gold (after hypothetical purchase) label below your current gold when hovering an item in the shop.
 */
class ShopCostPreview
{
    static MegaLabel CostPreviewLabel = new MegaLabel();
    static bool initDone = false;

    static void init()
    {
        if (NRun.Instance?.GlobalUi?.TopBar?.Gold?._goldLabel is not MegaLabel goldLabel)
            return;

        var label = (MegaLabel)goldLabel.Duplicate();

        label.Visible = false;
        label.AddThemeColorOverride(ThemeConstants.Label.FontColor, StsColors.red);
        label.SetFontSize(18); // original font size is 32

        var parent = goldLabel.GetParent().GetParent().GetParent();
        parent.AddChild(label);

        label.TopLevel = true;
        label.GlobalPosition = goldLabel.GlobalPosition + Vector2.Down * 25;

        CostPreviewLabel = label;
        initDone = true;
    }

    [HarmonyPatch(typeof(NMerchantSlot), "OnFocus")]
    class OnFocusPatch
    {
        static void Prefix(NMerchantSlot __instance)
        {
            if (!initDone || !GodotObject.IsInstanceValid(CostPreviewLabel))
                init();

            if (__instance.Player is not Player player)
                return;

            var previewGold = player.Gold - __instance.Entry.Cost;
            if (previewGold >= 0)
            {
                CostPreviewLabel.SetTextAutoSize($"↳ {previewGold}");
                CostPreviewLabel.Visible = true;
            }
        }
    }

    [HarmonyPatch(typeof(NMerchantSlot), "OnUnfocus")]
    class OnUnfocusPatch
    {
        static void Prefix(NMerchantSlot __instance) => CostPreviewLabel.Visible = false;
    }
}
