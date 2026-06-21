using HarmonyLib;
using MegaCrit.Sts2.Core.Rewards;

namespace MintySpire2.MintySpire2Code;

/**
 * Credits to kiooeht.
 * Changes the order of combat rewards so "special" card rewards (Thieving Hopper and Lantern Key) are below normal card
 * rewards. This is done because clicking rewards in their normal order causes a long delay while you wait for the
 * special card to stop covering up the middle card of the card reward.
 */
[HarmonyPatch(typeof(SpecialCardReward), nameof(SpecialCardReward.RewardsSetIndex), MethodType.Getter)]
static class SpecialCardRewardOrder
{
    [HarmonyPostfix]
    static void AfterNormalCardRewards(SpecialCardReward __instance, ref int __result)
    {
        if(Config.ChangeRewardOrder)
            __result = 6;
    }
}
