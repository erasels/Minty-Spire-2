using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Runs;
using MintySpire2.MintySpire2Code;

namespace MintySpire2.MintySpire2Code.relicreminders.cardoverlay;

/// <summary>
///     Service for determining which relics/powers should be displayed by the renderer.
///     Which queries <see cref="GetActiveIcons"/> / <see cref="HasAnyActiveFor"/> and refreshes on
///     <see cref="OverlaysChanged"/>. Mirrors EndTurnRelicReminderService.
/// </summary>
public static class CardOverlayService
{
    private static readonly Dictionary<Type, Func<RelicModel, CardModel, bool>> _relicRules = new();
    private static readonly Dictionary<Type, Func<PowerModel, CardModel, bool>> _powerRules = new();
    private static readonly List<Texture2D> _scratch = new(4);

    static CardOverlayService()
    {
        RegisterRelic<PenNib>((r, c) => c.Type == CardType.Attack && r.Status == RelicStatus.Active);
        RegisterRelic<Nunchaku>((r, c) => c.Type == CardType.Attack && r.Status == RelicStatus.Active);
        RegisterRelic<TuningFork>((r, c) => c.Type == CardType.Skill && r.Status == RelicStatus.Active);
        RegisterRelic<GalacticDust>(ShouldShowGalacticDust);
        RegisterRelic<ThrowingAxe>((r, _) => r.Status == RelicStatus.Active);
        RegisterRelic<RainbowRing>((r, c) => c.Type == RainbowRingRemainingTypeRequired(r));

        RegisterPower<EchoFormPower>(ShouldShowEchoForm);
    }

    public static event Action? OverlaysChanged;

    public static void RegisterRelic<TR>(Func<TR, CardModel, bool> rule) where TR : RelicModel
        => _relicRules[typeof(TR)] = (r, c) => rule((TR)r, c);

    public static void RegisterPower<TP>(Func<TP, CardModel, bool> rule) where TP : PowerModel
        => _powerRules[typeof(TP)] = (p, c) => rule((TP)p, c);

    public static void NotifyOverlaysMayHaveChanged()
    {
        if (!Config.CardOverlayReminders) return;
        OverlaysChanged?.Invoke();
    }

    public static List<Texture2D> GetActiveIcons(CardModel card)
    {
        var icons = new List<Texture2D>(4);
        if (!Config.CardOverlayReminders) return icons;
        CollectActiveIcons(card, icons);
        return icons;
    }

    public static bool HasAnyActiveFor(CardModel card)
    {
        if (!Config.CardOverlayReminders) return false;
        CollectActiveIcons(card, _scratch);
        return _scratch.Count > 0;
    }

    private static void CollectActiveIcons(CardModel card, List<Texture2D> icons)
    {
        icons.Clear();

        var me = LocalContext.GetMe(RunManager.Instance?.State);
        if (me == null) return;

        foreach (var relic in me.Relics)
        {
            if (relic.IsMelted) continue;
            if (_relicRules.TryGetValue(relic.GetType(), out var rule) && rule(relic, card))
                icons.Add(relic.Icon);
        }

        foreach (var power in me.Creature.Powers)
        {
            if (_powerRules.TryGetValue(power.GetType(), out var rule) && rule(power, card))
                icons.Add(power.Icon);
        }
    }

    private static bool ShouldShowGalacticDust(GalacticDust gd, CardModel card)
    {
        var threshold = gd.DynamicVars.Stars.IntValue;
        if (threshold <= 0 || card.CurrentStarCost <= 0) return false;
        return (gd.StarsSpent % threshold) + card.CurrentStarCost >= threshold;
    }
    
    private static CardType? RainbowRingRemainingTypeRequired(RainbowRing rr)
    {
        if (rr.Status == RelicStatus.Normal) // RelicStatus only turns Active after activation
        {
            var attackPlayed = Math.Min(1, rr.AttacksPlayedThisTurn);
            var skillPlayed = Math.Min(1, rr.SkillsPlayedThisTurn);
            var powerPlayed = Math.Min(1, rr.PowersPlayedThisTurn);

            if (attackPlayed + skillPlayed + powerPlayed == 2)
            {
                if (attackPlayed == 0) return CardType.Attack;
                if (skillPlayed == 0)  return CardType.Skill;
                                       return CardType.Power;
            }
        }
        return null;
    }

    // Shows while the player still has unspent duplications this turn.
    private static bool ShouldShowEchoForm(EchoFormPower power, CardModel card)
    {
        if (card.Owner.Creature != power.Owner) return false;

        var firstInSeriesPlayed = CombatManager.Instance.History.CardPlaysStarted
            .Count(e => e.Actor == power.Owner && e.CardPlay.IsFirstInSeries && e.HappenedThisTurn(power.CombatState));

        return firstInSeriesPlayed < power.Amount;
    }
}