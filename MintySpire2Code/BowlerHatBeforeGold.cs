using HarmonyLib;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Rewards;

namespace MintySpire2.MintySpire2Code;
/**
 * By Mangochicken.
 * Moves Bowler Hat before the gold reward after combat,
 * so you don't miss the 25% extra gold in the combat you obtain it
 */
[HarmonyPatch(typeof(RelicReward), nameof(RelicReward.RewardsSetIndex), methodType: MethodType.Getter)]
public static class BowlerHatBeforeGold
{
    [HarmonyPostfix]
    static void BeforeGold(RelicReward __instance, ref int __result)
    {
        // potentially split into unique config
        if (Config.ChangeRewardOrder && __instance.Relic is BowlerHat)
            __result = 0;
    }
}
