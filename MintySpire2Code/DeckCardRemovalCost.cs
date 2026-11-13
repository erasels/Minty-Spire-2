using Godot;
using HarmonyLib;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Rooms;
using MintySpire2.MintySpire2Code.config;
using MintySpire2.MintySpire2Code.util;

namespace MintySpire2.MintySpire2Code;

/**
 * Displays the shop card removal cost above the upgradeed toggle in the deck view.
 */
[HarmonyPatch(typeof(NDeckViewScreen), nameof(NDeckViewScreen._Ready))]
class DeckCardRemovalCost
{
    [HarmonyPostfix]
    static void Postfix(NDeckViewScreen __instance, Player ____player)
    {
        var run = ____player.RunState;
        if (!Config.ShowCardRemovalCost || CompatHelper.IsBlitzMode(run))
            return;

        var entry = new MerchantCardRemovalEntry(____player);
        int cost = entry.Cost;
        // The cost getter applies merchant discounts only while inside a shop
        if (run.CurrentRoom is not MerchantRoom)
            cost = (int)Hook.ModifyMerchantPrice(run, ____player, entry, cost);

        var caption = new LocString("merchant_room", "MINTYSPIRE2.cardRemovalCost");
        var upgrades = __instance.GetNode<Control>("ViewUpgrades");
        var template = __instance.GetNode<MegaLabel>("%ViewUpgradesLabel");
        // Place above upgrades toggle (bottom left)
        var row = new HBoxContainer
        {
            AnchorLeft = upgrades.AnchorLeft,
            AnchorRight = upgrades.AnchorLeft,
            AnchorTop = upgrades.AnchorTop,
            AnchorBottom = upgrades.AnchorTop,
            OffsetLeft = upgrades.OffsetLeft,
            OffsetRight = upgrades.OffsetLeft,
            OffsetTop = upgrades.OffsetTop - 36,
            OffsetBottom = upgrades.OffsetTop,
            Scale = upgrades.Scale,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        __instance.AddChild(row);

        AddLabel(caption.GetFormattedText());
        // Show gold symbol
        row.AddChild(new TextureRect
        {
            Texture = GD.Load<Texture2D>("res://images/atlases/ui_atlas.sprites/top_bar/top_bar_gold.tres"),
            CustomMinimumSize = new Vector2(32, 32),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        });
        AddLabel(cost.ToString());

        void AddLabel(string text)
        {
            var label = (MegaLabel)template.Duplicate();
            label.MouseFilter = Control.MouseFilterEnum.Ignore;
            row.AddChild(label);
            label.SetTextAutoSize(text);
        }
    }
}
