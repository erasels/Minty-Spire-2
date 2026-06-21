using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using MintySpire2.MintySpire2Code.relicreminders;
using MintySpire2.MintySpire2Code.relicreminders.endturnbutton;

namespace MintySpire2.MintySpire2Code.util;

public class MintyHooker : CustomSingletonModel
{
    public MintyHooker() : base(HookType.Combat)
    { }
    
    
    

    public override Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side == CombatSide.Player)
            EndTurnRelicReminderService.NotifyRemindersMayHaveChanged();
        return Task.CompletedTask;
    }

    public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        EndTurnRelicReminderService.NotifyRemindersMayHaveChanged();
        return Task.CompletedTask;
    }

    public override Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side == CombatSide.Player)
            EndTurnRelicReminderService.NotifyRemindersMayHaveChanged();
        return Task.CompletedTask;
    }

    // History Course & Threshold relics
    public override Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
    {
        HistoryCourseTooltip.HistoryStartPulse(Wiz.p()?.GetRelic<HistoryCourse>(), cardPlay);
        ThresholdRelicCardOverlay.RefreshTrackedCardOverlays();
        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        HistoryCourseTooltip.HistoryStopPulseOnCombatEnd(Wiz.p()?.GetRelic<HistoryCourse>());
        return Task.CompletedTask;
    }

    public override Task AfterStarsSpent(int amount, Player spender)
    {
        ThresholdRelicCardOverlay.RefreshTrackedCardOverlays();
        return Task.CompletedTask;
    }
}