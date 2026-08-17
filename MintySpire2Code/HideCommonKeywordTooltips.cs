using System;
using System.Collections.Generic;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Nodes.HoverTips;

namespace MintySpire2.MintySpire2Code;

/// <summary>
/// Hides "common" keyword tooltips (Block, Strength, Vulnerable, Weak, Dexterity, Exhaust, Ethereal, Energy)
/// when hovering cards. Basically reimplements the scrapped feature from base game.
/// </summary>
[HarmonyPatch(typeof(NHoverTipSet), nameof(NHoverTipSet.CreateAndShow), typeof(Control), typeof(IEnumerable<IHoverTip>), typeof(HoverTipAlignment))]
public static class HideCommonKeywordTooltips
{
    // Each entry is a substring of the HoverTip.Id. Match is case-insensitive.
    private static readonly string[] _commonIdMarkers =
    [
        "card_keywords.EXHAUST.",
        "card_keywords.ETHEREAL.",
        "card_keywords.INNATE.",
        "card_keywords.RETAIN.",
        "static_hover_tips.BLOCK.",
        "static_hover_tips.ENERGY.",
        "static_hover_tips.CHANNELING.",
        "static_hover_tips.EVOKE.",
        "power.STRENGTH_POWER",
        "power.DEXTERITY_POWER",
        "power.FOCUS_POWER",
        "power.VULNERABLE_POWER",
        "power.WEAK_POWER",
        "power.FRAIL_POWER"
    ];

    [HarmonyPrefix]
    static bool Prefix(ref IEnumerable<IHoverTip> hoverTips)
    {
        if (!Config.HideCommonKeywordTooltips)
            return true;

        var filtered = new List<IHoverTip>();
        foreach (var tip in hoverTips)
        {
            if (tip == null)
                continue;
            if (!IsCommon(tip.Id))
                filtered.Add(tip);
        }
        hoverTips = filtered;
        return true;
    }

    private static bool IsCommon(string id)
    {
        return !string.IsNullOrEmpty(id) && _commonIdMarkers.Any(marker => id.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }
}
