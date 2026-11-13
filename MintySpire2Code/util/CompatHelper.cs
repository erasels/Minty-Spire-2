using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace MintySpire2.MintySpire2Code.util;

internal static class CompatHelper
{
    // [COMPAT] For v0.107: Look up the v0.113 Dominion power by ID.
    private static readonly ModelId InsatiableDominionPowerId = new("POWER", "INSATIABLE_DOMINION_POWER");

    public static PowerModel? GetInsatiableDominionPower(Creature creature) => creature.GetPower(InsatiableDominionPowerId);

    // [COMPAT] For v0.107: Check for Blitz only when the run exposes GameMode.
    private static readonly PropertyInfo? GameModeProperty = typeof(IRunState).GetProperty("GameMode");

    public static bool IsBlitzMode(IRunState run) => GameModeProperty?.GetValue(run)?.ToString() == "Blitz";

    // [COMPAT] For v0.107: Use the card owner when CardPlay lacks Player
    private static readonly Func<CardPlay, Player>? PlayedBy = typeof(CardPlay).GetProperty("Player")?.GetMethod?.CreateDelegate<Func<CardPlay, Player>>();

    public static Player? GetPlayer(CardPlay play) => PlayedBy?.Invoke(play) ?? play.Card.Owner;

    // [COMPAT] For v0.107: ModifyDamageMultiplicative doesn't have the CardPlay parameter
    private static readonly MethodInfo DamageHook = typeof(AbstractModel).GetMethod("ModifyDamageMultiplicative")!;
    private static readonly bool TakesCardPlay = DamageHook.GetParameters().Length == 6;

    public static decimal DamageMultiplier(PowerModel power, Creature? target, Creature? dealer)
    {
        object?[] args = TakesCardPlay
            ? [target, 1M, ValueProp.Move, dealer, null, null]
            : [target, 1M, ValueProp.Move, dealer, null];
        return (decimal)DamageHook.Invoke(power, args)!;
    }

    // [COMPAT] For v0.107: Hook.ModifyDamage doesn't have the CardPlay parameter
    private static readonly MethodInfo ModifyDamageMethod = AccessTools.GetDeclaredMethods(typeof(Hook))
        .Single(method => method.Name == nameof(Hook.ModifyDamage)
                          && method.GetParameters() is { Length: 10 or 11 } parameters
                          && parameters[^1].IsOut);

    public static decimal ModifyDamage(
        IRunState runState,
        object combatState,
        Creature? target,
        Creature? dealer,
        decimal damage,
        ValueProp props,
        CardModel? cardSource,
        CardPlay? cardPlay,
        ModifyDamageHookType modifyDamageHookType,
        CardPreviewMode previewMode)
    {
        var parameters = ModifyDamageMethod.GetParameters();
        var args = parameters.Length == 11
            ? new object?[]
            {
                runState, combatState, target, dealer, damage, props, cardSource, cardPlay, modifyDamageHookType, previewMode, null
            }
            : new object?[]
            {
                runState, combatState, target, dealer, damage, props, cardSource, modifyDamageHookType, previewMode, null
            };

        return (decimal)ModifyDamageMethod.Invoke(null, args)!;
    }

    // [COMPAT] For v0.107: AfterSideTurnEnd was named AfterTurnEnd.
    public static MethodBase GetAfterSideTurnEndMethod()
    {
        return AccessTools.Method(typeof(Hook), "AfterSideTurnEnd")
               ?? AccessTools.Method(typeof(Hook), "AfterTurnEnd")
               ?? throw new MissingMethodException(typeof(Hook).FullName, "AfterSideTurnEnd/AfterTurnEnd");
    }

    // [COMPAT] For v0.108-v0.109: Show the pre-rework Outbreak counter.
    public static Type? GetLegacyOutbreakType() => ModManager._gameVersion?.Minor is 108 or 109
        ? typeof(PowerModel).Assembly.GetType("MegaCrit.Sts2.Core.Models.Powers.OutbreakPower")
        : null;

    // [COMPAT] For v0.107-v0.108: History Course also replays skills.
    public static bool HistoryCourseReplays(CardType type) =>
        type == CardType.Attack || (type == CardType.Skill && ModManager._gameVersion?.Minor is not >= 109);
}
