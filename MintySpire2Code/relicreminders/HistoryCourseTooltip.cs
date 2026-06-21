using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;

namespace MintySpire2.MintySpire2Code.relicreminders;

/**
 * Credits to kiooeht.
 * Makes History Course pulse when it will activate next turn and display a card tooltip of the card it will play.
 */
[HarmonyPatch]
static class HistoryCourseTooltip
{
    /*
     * This logic is copied from HistoryCourse's lambda, it will have to change if History Course ever changes.
     * ILSpy was required to decompile it as dotPeek and Rider produce "reference to compiler-generated method" errors.
     */
    private static bool IsAppropriateCard(RelicModel relic, CardPlay cardPlay)
    {
        bool isThisPlayer = cardPlay.Card.Owner == relic.Owner;
        bool isAttackOrSkill = cardPlay.Card.Type is CardType.Attack or CardType.Skill;
        bool isDupe = cardPlay.Card.IsDupe;
        return isThisPlayer && isAttackOrSkill && !isDupe;
    }
    
    [HarmonyPatch(typeof(RelicModel), "ExtraHoverTips", MethodType.Getter)]
    [HarmonyPostfix]
    static IEnumerable<IHoverTip> AddCardTooltip(IEnumerable<IHoverTip> __result, RelicModel __instance)
    {
        foreach (var tip in __result) {
            yield return tip;
        }
        
        if (__instance is not HistoryCourse) yield break;

        var card = CombatManager.Instance.History.CardPlaysFinished.LastOrDefault(
            e => {
                bool wasPlayedThisRound = e.RoundNumber == __instance.Owner.Creature.CombatState?.RoundNumber;
                return wasPlayedThisRound && IsAppropriateCard(__instance, e.CardPlay);
            })?.CardPlay.Card;
        if (card != null) {
            yield return HoverTipFactory.FromCard(card);
        }
    }

    [HarmonyPatch(typeof(HistoryCourse), nameof(HistoryCourse.AfterAutoPrePlayPhaseEntered))]
    [HarmonyPostfix]
    static void StopPulseOnTurnStart(HistoryCourse __instance)
    {
        __instance.Status = RelicStatus.Normal;
    }
    
    public static void HistoryStartPulse(HistoryCourse? historyCourse, CardPlay cardPlay)
    {
        if (historyCourse != null && IsAppropriateCard(historyCourse, cardPlay)) {
            historyCourse.Status = RelicStatus.Active;
        }
    }

    public static void HistoryStopPulseOnCombatEnd(HistoryCourse? historyCourse)
    {
        if (historyCourse != null)
            historyCourse.Status = RelicStatus.Normal;
    }
}
