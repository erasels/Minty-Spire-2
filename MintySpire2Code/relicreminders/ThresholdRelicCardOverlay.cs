using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MintySpire2.MintySpire2Code.util;

namespace MintySpire2.MintySpire2Code.relicreminders;

/// <summary>
///     Credits to Book and erasels.
///     Adds relic reminder icons to cards in hand when threshold relics are primed.
///     Also makes affected cards glow gold.
/// </summary>
[HarmonyPatch]
public static class ThresholdRelicCardOverlay
{
    private const string IconContainerNodeName = "MintyThresholdRelicIcons";
    private static readonly List<Texture2D> _icons = new(4);
    
    // Dynamically keep track of applicable relics
    [HarmonyPatch(typeof(CombatRoom), "StartCombat")]
    [HarmonyPostfix]
    static void CatchCombatStart(IRunState? runState)
    {
        var me = LocalContext.GetMe(runState);
        if (me == null) return;
        ClearRelicTrackers();
        foreach (var relic in me.Relics)
        {
            IdentifyThresholdRelic(relic);
        }

        me.RelicObtained -= IdentifyThresholdRelic;
        me.RelicObtained += IdentifyThresholdRelic;
    }

    [HarmonyPatch(typeof(CombatRoom), "OnCombatEnded")]
    [HarmonyPostfix]
    static void CatchCombatEnd()
    {
        var me = Wiz.p();
        if (me != null) me.RelicObtained -= IdentifyThresholdRelic;
    }

    // Called whenever a card is added to the hand and destroyed when moved from it
    [HarmonyPatch(typeof(NHandCardHolder), "Create")]
    [HarmonyPostfix]
    private static void OnHandHolderCreate_Postfix(NHandCardHolder __result)
    {
        RefreshCardOverlay(__result);
    }

    [HarmonyPatch(typeof(CardModel), "ShouldGlowGold", MethodType.Getter)]
    [HarmonyPostfix]
    public static void OverrideGoldGlow(CardModel __instance, ref bool __result)
    {
        if (!__result)
            __result = HasAnyActiveThresholdIcon(__instance);
    }


    public static void RefreshTrackedCardOverlays()
    {
        if(!HasAny()) return;
        foreach (var holder in GetActiveHolders())
        {
            RefreshCardOverlay(holder);
        }
    }

    private static void RefreshCardOverlay(NHandCardHolder holder)
    {
        if (!GodotObject.IsInstanceValid(holder)) return;
        var card = holder.CardNode;
        if (card == null) return;
        var model = card.Model;
        if (model == null || !HasAny())
        {
            HideIcons(holder);
            return;
        }
        
        CollectActiveIcons(model, _icons);

        if (_icons.Count == 0)
        {
            HideIcons(holder);
            return;
        }

        var container = EnsureIconContainer(holder, _icons.Count);
        if (container == null) return;

        for (var i = 0; i < _icons.Count; i++)
            SetIcon(container.GetChild<TextureRect>(i), _icons[i]);

        for (var i = _icons.Count; i < container.GetChildCount(); i++)
            SetIcon(container.GetChild<TextureRect>(i), null);

        container.Visible = true;
    }

    private static IReadOnlyList<NHandCardHolder> GetActiveHolders()
    {
        return NPlayerHand.Instance?.ActiveHolders ?? [];
    }

    private static bool HasAnyActiveThresholdIcon(CardModel? card)
    {
        if (!HasAny() || card == null) return false;
        
        CollectActiveIcons(card, _icons);
        return _icons.Count > 0;
    }

    private static void CollectActiveIcons(CardModel card, List<Texture2D> icons)
    {
        icons.Clear();

        if (!HasAny())
            return;
        
        if (card.Type == CardType.Attack && _penNib?.Status == RelicStatus.Active) icons.Add(_penNib.Icon);

        if (card.Type == CardType.Attack && _nunchaku?.Status == RelicStatus.Active) icons.Add(_nunchaku.Icon);

        if (card.Type == CardType.Skill && _tuningFork?.Status == RelicStatus.Active) icons.Add(_tuningFork.Icon);

        if (ShouldShowGalacticDust(card)) icons.Add(_galacticDust!.Icon);
    }

    private static bool ShouldShowGalacticDust(CardModel card)
    {
        if (_galacticDust == null) return false;

        var threshold = _galacticDust.DynamicVars.Stars.IntValue;
        if (threshold <= 0 || card.CurrentStarCost <= 0) return false;

        return (_galacticDust.StarsSpent % threshold) + card.CurrentStarCost >= threshold;
    }

    // Icon container management
    private static Control? EnsureIconContainer(NHandCardHolder holder, int requiredIconSlots)
    {
        var container = holder.GetNodeOrNull<Control>(IconContainerNodeName);
        if (container == null)
        {
            container = new Control
            {
                Name = IconContainerNodeName,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                AnchorLeft = 1f,
                AnchorRight = 1f,
                AnchorTop = 0f,
                AnchorBottom = 0f,
                Visible = false,
            };

            holder.AddChild(container);
        }

        while (container.GetChildCount() < requiredIconSlots)
            container.AddChild(MakeIconSlot(container.GetChildCount()));

        return container;
    }

    private static TextureRect MakeIconSlot(int index)
    {
        const float horizontalSpacing = 32f;
        var horizontalOffset = index * horizontalSpacing;

        return new TextureRect
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            AnchorLeft = 1f,
            AnchorRight = 1f,
            AnchorTop = 0f,
            AnchorBottom = 0f,
            OffsetLeft = 112f - horizontalOffset,
            OffsetRight = 160f - horizontalOffset,
            OffsetTop = -218f,
            OffsetBottom = -170f,
            Visible = false,
        };
    }

    private static void SetIcon(TextureRect iconRect, Texture2D? texture)
    {
        iconRect.Texture = texture;
        iconRect.Visible = texture != null;
    }

    private static void HideIcons(NHandCardHolder holder)
    {
        var container = holder.GetNodeOrNull<Control>(IconContainerNodeName);
        if (container == null) return;

        container.Visible = false;

        for (var i = 0; i < container.GetChildCount(); i++)
            SetIcon(container.GetChild<TextureRect>(i), null);
    }

    
    // Relic management
    private static PenNib? _penNib;
    private static Nunchaku? _nunchaku;
    private static TuningFork? _tuningFork;
    private static GalacticDust? _galacticDust;
    
    private static bool HasAny()
    {
        return _penNib != null || _galacticDust != null || _nunchaku != null || _tuningFork != null;
    }

    private static void ClearRelicTrackers()
    {
        _penNib = null;
        _galacticDust = null;
        _nunchaku = null;
        _tuningFork = null;
    }
    
    private static void IdentifyThresholdRelic(RelicModel relic)
    {
        if (relic.IsMelted) return;
        switch (relic)
        {
            case PenNib pn:
                _penNib = pn;
                break;
            case Nunchaku n:
                _nunchaku = n;
                break;
            case TuningFork tf:
                _tuningFork = tf;
                break;
            case GalacticDust gd:
                _galacticDust = gd;
                break;
        }
    }
}
