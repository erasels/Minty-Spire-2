using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;

namespace MintySpire2.MintySpire2Code.combat;

/// <summary>
///     Patches MultiAttackIntent.GetIntentLabel to change the Repeat token from:
///     Repeat = Repeats
///     to:
///     Repeat = "Repeats (TotalDamage)"
///     Example: repeats=2, singleDamage=7 => "7x2 (14)".
/// </summary>
[HarmonyPatch(typeof(MultiAttackIntent), nameof(MultiAttackIntent.GetIntentLabel))]
public static class SumMultiDamage
{
    /// <summary>
    ///     Overwrite the original Repeat label with the new one containing total damage
    /// </summary>
    public static void Postfix(MultiAttackIntent __instance, IEnumerable<Creature> targets, Creature owner, ref LocString __result)
    {
        if(Config.ShowSumMultiDamage)
            __result.Add("Repeat", $"{__instance.Repeats} ({__instance.GetTotalDamage(targets, owner)})");
    }
}