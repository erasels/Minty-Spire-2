using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MintySpire2.MintySpire2Code.config;

namespace MintySpire2.MintySpire2Code;

/// <summary>
/// Lets each map legend item persist its normal hover highlight when clicked
/// and highlights the legend item. A second click removes the pinned hover.
/// </summary>
[HarmonyPatch]
public static class StickyMapLegendHighlights
{
    private const string PinOutlineNodeName = "MintySpirePinnedOutline";

    private static readonly ConditionalWeakTable<NMapScreen, HashSet<MapPointType>> PinnedTypesByScreen = new();

    [HarmonyPatch(typeof(NMapLegendItem), nameof(NMapLegendItem._Ready))]
    [HarmonyPostfix]
    public static void ConnectLegendClick(NMapLegendItem __instance, MapPointType ____pointType)
    {
        CreatePinOutline(__instance, ____pointType);
        __instance.Connect(
            NClickableControl.SignalName.Released,
            Callable.From<NClickableControl>(_ => ToggleHighlight(__instance, ____pointType)));
    }

    [HarmonyPatch(typeof(NNormalMapPoint), "OnHighlightPointType")]
    [HarmonyPostfix]
    public static void PreservePinnedHighlight(NNormalMapPoint __instance)
    {
        if (IsPinned(__instance, __instance.Point.PointType))
            __instance.AnimHover();
    }

    [HarmonyPatch(typeof(NNormalMapPoint), "OnUnfocus")]
    [HarmonyPostfix]
    public static void PreservePinnedHighlightAfterNodeHover(NNormalMapPoint __instance)
    {
        if (IsPinned(__instance, __instance.Point.PointType))
            __instance.AnimHover();
    }

    [HarmonyPatch(typeof(NNormalMapPoint), nameof(NNormalMapPoint._Ready))]
    [HarmonyPostfix]
    public static void HighlightNewMapPoint(NNormalMapPoint __instance)
    {
        if (IsPinned(__instance, __instance.Point.PointType))
            __instance.AnimHover();
    }

    [HarmonyPatch(typeof(NMapPoint), nameof(NMapPoint.RefreshVisualsInstantly))]
    [HarmonyPostfix]
    public static void PreservePinnedHighlightAfterVisualRefresh(NMapPoint __instance)
    {
        // Normally reopening the map recalculates travelability which tints unreachable nodes
        // that looks weird for highlighted nodes which are always full color, this fixes that.
        if (__instance is NNormalMapPoint normalPoint && IsPinned(normalPoint, normalPoint.Point.PointType))
            normalPoint.AnimHover();
    }

    private static void ToggleHighlight(NMapLegendItem legendItem, MapPointType pointType)
    {
        NMapScreen? screen = FindMapScreen(legendItem);
        if (!Config.StickyMapLegendHighlights || screen is null)
            return;

        HashSet<MapPointType> pinnedTypes = PinnedTypesByScreen.GetOrCreateValue(screen);
        bool isPinned = pinnedTypes.Add(pointType);
        if (!isPinned)
            pinnedTypes.Remove(pointType);

        TextureRect? pinOutline = legendItem.GetNodeOrNull<TextureRect>($"Icon/{PinOutlineNodeName}");
        if (pinOutline is not null)
            pinOutline.Visible = isPinned;

        // Re-run the current legend hover highlight. When the cursor/focus leaves,
        // the map-point postfix above reapplies every type that remains pinned.
        screen.HighlightPointType(pointType);
    }

    private static bool IsPinned(Node node, MapPointType pointType)
    {
        NMapScreen? screen = FindMapScreen(node);
        return Config.StickyMapLegendHighlights
               && screen is not null
               && PinnedTypesByScreen.TryGetValue(screen, out HashSet<MapPointType>? pinnedTypes)
               && pinnedTypes.Contains(pointType);
    }

    // Adds an outline to pinned legend items
    private static void CreatePinOutline(NMapLegendItem legendItem, MapPointType pointType)
    {
        TextureRect icon = legendItem.GetNode<TextureRect>("Icon");
        if (icon.HasNode(PinOutlineNodeName))
            return;

        string? outlinePath = GetOutlinePath(pointType);
        if (outlinePath is null)
            return;

        var outline = new TextureRect
        {
            Name = PinOutlineNodeName,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Texture = ResourceLoader.Load<Texture2D>(outlinePath),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            ShowBehindParent = true,
            Visible = IsPinned(legendItem, pointType)
        };
        icon.AddChild(outline);
        outline.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
    }

    private static string? GetOutlinePath(MapPointType pointType)
    {
        string? iconName = pointType switch
        {
            MapPointType.Shop => "map_shop",
            MapPointType.Treasure => "map_chest",
            MapPointType.RestSite => "map_rest",
            MapPointType.Monster => "map_monster",
            MapPointType.Elite => "map_elite",
            MapPointType.Unknown => "map_unknown",
            _ => null
        };
        
        return iconName is null
            ? null
            : $"res://images/atlases/compressed.sprites/map/{iconName}_outline.tres";
    }

    private static NMapScreen? FindMapScreen(Node node)
    {
        for (Node? current = node; current is not null; current = current.GetParent())
        {
            if (current is NMapScreen screen)
                return screen;
        }

        return null;
    }
}
