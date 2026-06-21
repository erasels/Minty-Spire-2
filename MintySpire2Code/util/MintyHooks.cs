using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using MintySpire2.MintySpire2Code.relicreminders.endturnbutton;

namespace MintySpire2.MintySpire2Code.util;

public class MintyHooks
{
    // End turn relics
    [HarmonyPatch(typeof(Hook), nameof(Hook.AfterBlockGained), MethodType.Async)]
    [HarmonyPostfix]
    public static void BlockGainHook(ICombatState combatState, Creature creature, decimal amount, ValueProp props, CardModel? cardSource)
    {
        if (LocalContext.IsMe(creature))
            EndTurnRelicReminderService.NotifyRemindersMayHaveChanged();
    }
    
    [HarmonyPatch(typeof(Hook), nameof(Hook.AfterCardDrawn), MethodType.Async)]
    [HarmonyPostfix]
    public static void CardDrawHook(ICombatState combatState, PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
    {
        EndTurnRelicReminderService.NotifyRemindersMayHaveChanged();
    }
    
    [HarmonyPatch(typeof(Hook), nameof(Hook.AfterCardPlayed), MethodType.Async)]
    [HarmonyPostfix]
    public static void AfterCardPlayerHook(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        EndTurnRelicReminderService.NotifyRemindersMayHaveChanged();
    }
    
    [HarmonyPatch(typeof(Hook), nameof(Hook.AfterEnergySpent), MethodType.Async)]
    [HarmonyPostfix]
    public static void AfterEnergySpentHook(CardModel card, int amount)
    {
        EndTurnRelicReminderService.NotifyRemindersMayHaveChanged();
    }
    
    
}