using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models.Potions;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Potions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MintySpire2.MintySpire2Code.util;

namespace MintySpire2.MintySpire2Code;

/// <summary>
/// When the local player enters a shop while carrying a FoulPotion, the merchant uses a speech bubble
/// hinting that they've got potions to hand over. They also Bounce :)
/// </summary>
[HarmonyPatch(typeof(NMerchantRoom), "_Ready")]
public static class FoulPotionReminderPatch
{
    private const string LocTable = "merchant_room";
    private const string FoulPotionReminderKey = "MINTYSPIRE2.foulPotionReminder";

    private const double DelaySeconds = 0.6;
    private const double ReminderDuration = 6.0;
    private const double BounceInterval = 1.0;
    private const string _bounceTimerName = "MintyFoulPotionBounceLoop";

    [HarmonyPostfix]
    static void Postfix(NMerchantRoom __instance)
    {
        if (!Config.OutOfCombatReminders) return;
        if (!Config.EnableJokes) return;

        var me = Wiz.p();
        if (me == null) return;
        if (!me.Potions.Any(p => p is FoulPotion)) return;

        var line = LocString.GetIfExists(LocTable, FoulPotionReminderKey);

        var timer = __instance.GetTree().CreateTimer(DelaySeconds);
        timer.Timeout += () =>
        {
            if (!GodotObject.IsInstanceValid(__instance)) return;

            BounceFoulPotions();

            if (line != null)
            {
                var button = __instance.MerchantButton;
                if (GodotObject.IsInstanceValid(button))
                    button.PlayDialogue(line, ReminderDuration);
            }

            StartBounceLoop(__instance);
        };
    }

    /// <summary>
    /// Starts a looping Timer (child of the room) that bounces the foul potions every second.
    /// The timer is freed automatically when the merchant room leaves the tree.
    /// </summary>
    private static void StartBounceLoop(NMerchantRoom room)
    {
        if (!GodotObject.IsInstanceValid(room)) return;
        if (room.HasNode(_bounceTimerName)) return;

        var loop = new Godot.Timer
        {
            Name = _bounceTimerName,
            WaitTime = BounceInterval,
            Autostart = true,
        };
        loop.Timeout += () =>
        {
            if (!GodotObject.IsInstanceValid(room)) return;
            BounceFoulPotions();
        };
        room.AddChild(loop);
    }
    
    private static void BounceFoulPotions()
    {
        var container = NRun.Instance?.GlobalUi?.TopBar?.PotionContainer;

        var holdersRoot = container?.GetNodeOrNull<Control>("MarginContainer/PotionHolders");
        if (holdersRoot == null) return;

        foreach (var child in holdersRoot.GetChildren())
        {
            if (child is not NPotionHolder holder) continue;
            var potion = holder.Potion;
            if (potion?.Model is not FoulPotion) continue;
            potion.DoBounce();
        }
    }
}
