using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MintySpire2.MintySpire2Code;

namespace MintySpire2.MintySpire2Code.relicreminders.cardoverlay;

/// <summary>
///     Credits to Book and erasels.
///     Renders relic/power reminder icons on cards in hand and makes them glow gold.
///     Icon selection is delegated to <see cref="CardOverlayService"/>, this class only
///     manages the icon container nodes and refreshes when the service raises
///     <see cref="CardOverlayService.OverlaysChanged"/>.
/// </summary>
[HarmonyPatch]
public static class CardOverlayRenderer
{
    private const string IconContainerNodeName = "MintyThresholdRelicIcons";

    static CardOverlayRenderer()
    {
        CardOverlayService.OverlaysChanged += RefreshTrackedCardOverlays;
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
        if (!Config.RelicCardGlow) return;
        if (!__result)
            __result = CardOverlayService.HasAnyActiveFor(__instance);
    }

    private static void RefreshTrackedCardOverlays()
    {
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
        if (model == null)
        {
            HideIcons(holder);
            return;
        }

        var icons = CardOverlayService.GetActiveIcons(model);

        if (icons.Count == 0)
        {
            HideIcons(holder);
            return;
        }

        var container = EnsureIconContainer(holder, icons.Count);
        if (container == null) return;

        for (var i = 0; i < icons.Count; i++)
            SetIcon(container.GetChild<TextureRect>(i), icons[i]);

        for (var i = icons.Count; i < container.GetChildCount(); i++)
            SetIcon(container.GetChild<TextureRect>(i), null);

        container.Visible = true;
    }

    private static IReadOnlyList<NHandCardHolder> GetActiveHolders()
    {
        return NPlayerHand.Instance?.ActiveHolders ?? [];
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
}
