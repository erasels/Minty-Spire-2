using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Nodes.Rewards;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Rewards;

namespace MintySpire2.MintySpire2Code;

/**
 * Modifies the order of reward buttons on the reward screen.
 *
 * Moves Bowler Hat before the gold reward after combat, so you don't miss the 25% extra gold in the combat you obtain it.
 * Original credits: Mangochicken.
 *
 * moves war paint and whetstone after card rewards, so you are less likely to upgrade two strikes or two defends :)
 *
 * Changes the order of combat rewards so "special" card rewards (Thieving Hopper and Lantern Key) are below normal card
 * rewards. This is done because clicking rewards in their normal order causes a long delay while you wait for the
 * special card to stop covering up the middle card of the card reward.
 * Original credits: kiooeht.
 */
[HarmonyPatch]
static class RewardDisplayOrderPatch
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(NRewardsScreen), nameof(NRewardsScreen._Ready))]
    static void OnReady(NRewardsScreen __instance)
    {
        Apply(__instance);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(NRewardsScreen), "UpdateScreenState")]
    static void BeforeUpdateScreenState(NRewardsScreen __instance)
    {
        Apply(__instance);
    }

    private static void Apply(NRewardsScreen screen)
    {
        if (!Config.ChangeRewardOrder || screen._rewardButtons.Count <= 1)
            return;

        var reordered = screen._rewardButtons.ToList();

        MoveToFirst(reordered, button => button is NRewardButton { Reward: RelicReward { Relic: BowlerHat } });
        MoveToLast(reordered, button => button is NRewardButton { Reward: SpecialCardReward });
        MoveToLast(reordered, button => button is NRewardButton { Reward: RelicReward { Relic: WarPaint or Whetstone } });

        if (reordered.SequenceEqual(screen._rewardButtons))
            return;

        screen._rewardButtons.Clear();
        screen._rewardButtons.AddRange(reordered);

        for (var i = 0; i < reordered.Count; i++)
            screen._rewardsContainer.MoveChildSafely(reordered[i], i);
    }

    private static void MoveToFirst(List<Control> buttons, Func<Control, bool> shouldMove)
    {
        var moving = buttons.Where(shouldMove).ToList();
        if (moving.Count == 0)
            return;

        foreach (Control button in moving)
            buttons.Remove(button);

        buttons.InsertRange(0, moving);
    }

    private static void MoveToLast(List<Control> buttons, Func<Control, bool> shouldMove)
    {
        var moving = buttons.Where(shouldMove).ToList();
        if (moving.Count == 0)
            return;

        foreach (Control button in moving)
            buttons.Remove(button);

        buttons.AddRange(moving);
    }
}
