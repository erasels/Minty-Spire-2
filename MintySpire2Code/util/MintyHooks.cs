using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models.Relics;
using System.Reflection;
using MintySpire2.MintySpire2Code.relicreminders;
using MintySpire2.MintySpire2Code.relicreminders.endturnbutton;

namespace MintySpire2.MintySpire2Code.util;

public class MintyHooks
{
    /*
     * Welcome to my personal hell. Can't use custom hooks here because they cause modeldb hash mismatches,
     * thus I have to patch the hooks. This has the issue that you can't access params of an async method patch
     * because you basically patch a dynamically generated MoveNext() method. So I have to postfix the base method
     * and then wait for its execution to finish and execute my logic after.
     * I hope you enjoyed this small peak into the pains I go to, to make this a good mod.
     */

    [HarmonyPatch(typeof(Hook), nameof(Hook.AfterBlockGained))]
    public static class BlockGainHook
    {
        [HarmonyPostfix]
        public static void Postfix(Creature creature, ref Task __result)
        {
            __result = PostfixAsync(__result, creature);
        }

        private static async Task PostfixAsync(Task originalTask, Creature creature)
        {
            await originalTask;
            if (LocalContext.IsMe(creature))
                EndTurnRelicReminderService.NotifyRemindersMayHaveChanged();
        }
    }

    [HarmonyPatch(typeof(Hook), nameof(Hook.AfterCardDrawn))]
    public static class CardDrawHook
    {
        [HarmonyPostfix]
        public static void Postfix(ref Task __result)
        {
            __result = PostfixAsync(__result);
        }

        private static async Task PostfixAsync(Task originalTask)
        {
            await originalTask;
            EndTurnRelicReminderService.NotifyRemindersMayHaveChanged();
        }
    }

    [HarmonyPatch(typeof(Hook), nameof(Hook.AfterCardPlayed))]
    public static class AfterCardPlayedHook
    {
        [HarmonyPostfix]
        public static void Postfix(CardPlay cardPlay, ref Task __result)
        {
            __result = PostfixAsync(__result, cardPlay);
        }

        private static async Task PostfixAsync(Task originalTask, CardPlay cardPlay)
        {
            await originalTask;
            EndTurnRelicReminderService.NotifyRemindersMayHaveChanged();
            HistoryCourseTooltip.HistoryStartPulse(Wiz.p()?.GetRelic<HistoryCourse>(), cardPlay);
            ThresholdRelicCardOverlay.RefreshTrackedCardOverlays();
        }
    }

    [HarmonyPatch(typeof(Hook), nameof(Hook.AfterEnergySpent))]
    public static class AfterEnergySpentHook
    {
        [HarmonyPostfix]
        public static void Postfix(ref Task __result)
        {
            __result = PostfixAsync(__result);
        }

        private static async Task PostfixAsync(Task originalTask)
        {
            await originalTask;
            EndTurnRelicReminderService.NotifyRemindersMayHaveChanged();
        }
    }

    [HarmonyPatch(typeof(Hook), nameof(Hook.AfterSideTurnStart))]
    public static class AfterSideTurnStartHook
    {
        [HarmonyPostfix]
        public static void Postfix(CombatSide side, ref Task __result)
        {
            __result = PostfixAsync(__result, side);
        }

        private static async Task PostfixAsync(Task originalTask, CombatSide side)
        {
            await originalTask;
            if (side == CombatSide.Player)
                EndTurnRelicReminderService.NotifyRemindersMayHaveChanged();
        }
    }

    [HarmonyPatch(typeof(Hook), nameof(Hook.AfterPlayerTurnStart))]
    public static class AfterPlayerTurnStartHook
    {
        [HarmonyPostfix]
        public static void Postfix(ref Task __result)
        {
            __result = PostfixAsync(__result);
        }

        private static async Task PostfixAsync(Task originalTask)
        {
            await originalTask;
            EndTurnRelicReminderService.NotifyRemindersMayHaveChanged();
        }
    }

    [HarmonyPatch]
    public static class AfterSideTurnEndHook
    {
        public static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(Hook), "AfterSideTurnEnd")
                   ?? AccessTools.Method(typeof(Hook), "AfterTurnEnd")
                   ?? throw new MissingMethodException(typeof(Hook).FullName, "AfterSideTurnEnd/AfterTurnEnd");
        }

        [HarmonyPostfix]
        public static void Postfix(CombatSide side, ref Task __result)
        {
            __result = PostfixAsync(__result, side);
        }

        private static async Task PostfixAsync(Task originalTask, CombatSide side)
        {
            await originalTask;
            if (side == CombatSide.Player)
                EndTurnRelicReminderService.NotifyRemindersMayHaveChanged();
        }
    }

    [HarmonyPatch(typeof(Hook), nameof(Hook.AfterCombatEnd))]
    public static class AfterCombatEndHook
    {
        [HarmonyPostfix]
        public static void Postfix(ref Task __result)
        {
            __result = PostfixAsync(__result);
        }

        private static async Task PostfixAsync(Task originalTask)
        {
            await originalTask;
            HistoryCourseTooltip.HistoryStopPulseOnCombatEnd(Wiz.p()?.GetRelic<HistoryCourse>());
        }
    }

    [HarmonyPatch(typeof(Hook), nameof(Hook.AfterStarsSpent))]
    public static class AfterStarsSpentHook
    {
        [HarmonyPostfix]
        public static void Postfix(ref Task __result)
        {
            __result = PostfixAsync(__result);
        }

        private static async Task PostfixAsync(Task originalTask)
        {
            await originalTask;
            ThresholdRelicCardOverlay.RefreshTrackedCardOverlays();
        }
    }
}
