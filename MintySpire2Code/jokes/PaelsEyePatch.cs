using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Nodes.Events;
using MegaCrit.Sts2.Core.Nodes.Relics;
using MegaCrit.Sts2.Core.Nodes.Screens.InspectScreens;
using MegaCrit.Sts2.Core.Nodes.Screens.RelicCollection;

namespace MintySpire2.MintySpire2Code.jokes;

/**
 * Credits to Mangochicken, adds a pupil to the Pael's eye relic that follows the cursor.
 */

[HarmonyPatch]
public class PaelsLookingEyePatch()
{
    private static Texture2D? PaelsEyeBase {
        get {
            var loaded = ProjectSettings.LoadResourcePack(OS.GetExecutablePath().GetBaseDir().PathJoin("mods/MintySpire2/MintySpire2.pck"));
            if (loaded) {
                var eye = ResourceLoader.Load<Texture2D>("res://MintySpire2/images/relics/paels_eye_base.png");
                return eye;
            }
            else {
                MainFile.Logger.Info("Oh my god its broken again");
            }

            return null;
        }
    }

    private static PackedScene LookingEyeScene => ResourceLoader.Load<PackedScene>("res://MintySpire2/scenes/looking_eye/looking_eye.tscn");

    private const string _LookingEyeNode = "LookingEye";
    private const string _PupilNode = "Pupil";

    private static TextureRect? HasLookingEyeScene(Node parent)
    {
        foreach (var child in parent.GetChildren())
        {
            if (child.GetNodeOrNull<TextureRect>(_PupilNode) != null)
                return (TextureRect)child;
        }
        return null;
    }

    // Inventory visuals
    [HarmonyPatch(typeof(NRelicInventoryHolder), nameof(NRelicInventoryHolder._Ready))]
    [HarmonyPostfix]
    static void OnInventoryRelicReady(NRelicInventoryHolder __instance)
    {
        if (!Config.EnableJokes) return;
        
        var relic = __instance.Relic;
        var model = relic.Model;

        if (model is PaelsEye)
        {
            var icon_node = relic.Icon;
            icon_node.Texture = PaelsEyeBase;
            if (HasLookingEyeScene(icon_node) == null)
                icon_node.AddChild(LookingEyeScene.Instantiate());
        }
    }

    // Holder for shops i think??
    [HarmonyPatch(typeof(NRelicBasicHolder), nameof(NRelicBasicHolder._Ready))]
    [HarmonyPostfix]
    static void OnBaseRelicReady(NRelicBasicHolder __instance)
    {
        if (!Config.EnableJokes) return;

        var relic = __instance.Relic;
        var model = relic.Model;

        if (model is PaelsEye)
        {
            var icon_node = relic.Icon;
            icon_node.Texture = PaelsEyeBase;
            if (HasLookingEyeScene(icon_node) == null)
                icon_node.AddChild(LookingEyeScene.Instantiate());
        }
    }

    // Ancient Event visual
    [HarmonyPatch(typeof(NEventOptionButton), nameof(NEventOptionButton._Ready))]
    [HarmonyPostfix]
    static void OnEventOptionButtonReady(NEventOptionButton __instance)
    {
        if (!Config.EnableJokes) return;

        if (__instance.Event is AncientEventModel && __instance.Option.Relic is PaelsEye)
        {
            var icon_node = __instance.GetNode<TextureRect>("%RelicIcon");
            // The options are slightly rectangular, this allows my child nodes with FullRect layouts to function properly
            icon_node.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter; 
            icon_node.Texture = PaelsEyeBase;
            if (HasLookingEyeScene(icon_node) == null)
                icon_node.AddChild(LookingEyeScene.Instantiate());
        }
    }

    // Relic Collection
    [HarmonyPatch(typeof(NRelicCollectionEntry), nameof(NRelicCollectionEntry._Ready))]
    [HarmonyPostfix]
    static void OnRelicCollectionEntryReady(NRelicCollectionEntry __instance)
    {
        if (!Config.EnableJokes) return;

        if (__instance.relic is PaelsEye && __instance.ModelVisibility == ModelVisibility.Visible)
        {
            var icon_node = __instance._relicNode.GetChild(0) as TextureRect;
            icon_node.Texture = PaelsEyeBase;
            if (HasLookingEyeScene(icon_node) == null)
                icon_node.AddChild(LookingEyeScene.Instantiate());
        }
    }

    // Large inspect relic view
    [HarmonyPatch(typeof(NInspectRelicScreen), nameof(NInspectRelicScreen._Ready))]
    [HarmonyPostfix]
    static void OnInspectRelicReady(NInspectRelicScreen __instance)
    {
        if (!Config.EnableJokes) return;

        var icon_node = __instance._relicImage;
        if (HasLookingEyeScene(icon_node) == null)
            icon_node.AddChild(LookingEyeScene.Instantiate());
    }

    [HarmonyPatch(typeof(NInspectRelicScreen), nameof(NInspectRelicScreen.UpdateRelicDisplay))]
    [HarmonyPostfix]
    static void OnInspectRelicRedraw(NInspectRelicScreen __instance)
    {
        if (!Config.EnableJokes) return;

        var relicModel = __instance._relics[__instance._index];
        var icon_node = __instance._relicImage;
        var is_paels_eye = false;
        if (relicModel is PaelsEye)
        {
            // Not overwriting the base file, ideally allows toggling individual sections.
            icon_node.Texture = PaelsEyeBase;
            is_paels_eye = true;
        }
        else
        {
            is_paels_eye = false;
        }

        var child = icon_node.GetNode<TextureRect>(_LookingEyeNode);
        if (child != null)
        {
            child.Set("visible", is_paels_eye);
        }
    }
}
