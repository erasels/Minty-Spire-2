using Godot;
using HarmonyLib;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MintySpire2.MintySpire2Code.config;

namespace MintySpire2.MintySpire2Code;
/**
 * Credits to Plüschgiraffe, this mod highlights the name/title of enchanted cards purple.
 * Originally published via this mod: https://steamcommunity.com/sharedfiles/filedetails/?id=3762778574
 */

[HarmonyPatch(typeof(NCard), "UpdateTitleLabel")]
internal static class HighlightEnchantedCardNames
{

    private static string _previousColor = Config.HighlightEnchantsColor;
    private static string _previousOutlineColor = Config.HighlightEnchantsOutlineColor;
    private static Color _highlightColor = new(Config.HighlightEnchantsColor);
    private static Color _highlightOutlineColor = new(Config.HighlightEnchantsOutlineColor);
    
    [HarmonyPostfix]
    private static void Postfix(NCard __instance, MegaLabel ____titleLabel)
    {
        // if instance is not enchanted card return
        CardModel? card = __instance.Model;
        if (!Config.HighlightEnchants || card?.Enchantment is null) return;

        if (_previousColor != Config.HighlightEnchantsColor || _previousOutlineColor != Config.HighlightEnchantsOutlineColor)
        {
            _previousColor = Config.HighlightEnchantsColor;
            _highlightColor = new Color(Config.HighlightEnchantsColor);
            _previousOutlineColor = Config.HighlightEnchantsOutlineColor;
            _highlightOutlineColor = new Color(Config.HighlightEnchantsOutlineColor);
        }

        // change title color of enchanted cards
        ____titleLabel.AddThemeColorOverride(
            ThemeConstants.Label.FontColor,
            _highlightColor);

        // change title outline color of enchanted cards
        ____titleLabel.AddThemeColorOverride(
            ThemeConstants.Label.FontOutlineColor,
            _highlightOutlineColor);
    }
}