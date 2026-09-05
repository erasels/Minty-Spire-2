using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using BaseLib.Abstracts;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using MintySpire2.MintySpire2Code.util;

namespace MintySpire2.MintySpire2Code.combat;

/**
 * Credits to kiooeht, this displays a second amount for powers that require tracking multiple values.
 */
[HarmonyPatch(typeof(NPower))]
static class TwoAmountPowers
{
    private const string SecondAmountLabelName = "MintyAmount2Label";
    private static readonly ConditionalWeakTable<PowerModel, HashSet<NPower>> Displays = new();

    private static bool Handles(PowerModel? power) => power != null && power is not IHasSecondAmount && DisplaySecondAmount.ContainsKey(power.GetType());

    private static readonly Dictionary<Type, Func<PowerModel, Amount2Data>> DisplaySecondAmount = new()
    {
        { typeof(PanachePower), power => power.Amount },
        { typeof(TheBombPower), power => power.DynamicVars.Damage.IntValue },
        { typeof(VoidFormPower), power => new Amount2Data(amount1: $"{Math.Max(0, power.Amount - power.GetInternalData<VoidFormPower.Data>().cardsPlayedThisTurn)}/{power.Amount}") },
        { typeof(JugglingPower), power => power.GetInternalData<JugglingPower.Data>().attacksPlayedThisTurn },
        {
            typeof(PaleBlueDotPower), power =>
            {
                // Displays X/5 as amount2 where X is cards played this turn
                var cardCount = CombatManager.Instance.History.CardPlaysFinished.Count(c =>
                    c.HappenedThisTurn(power.CombatState) &&
                    c.CardPlay.Card.Owner == power.Owner.Player);
                var threshold = power.DynamicVars[PaleBlueDotPower.cardPlayThresholdKey].IntValue;
                return $"{cardCount}/{threshold}";
            }
        },
        {
            typeof(VulnerablePower), power =>
            {
                // Displays Vulnerable's % increase if it's not 50%
                var player = LocalContext.GetMe(RunManager.Instance.State);
                var mult = power.ModifyDamageMultiplicative(power.Owner, 1M, ValueProp.Move, player?.Creature, null, null);
                if (mult != power.DynamicVars[VulnerablePower._damageIncrease].BaseValue)
                {
                    mult = (mult - 1M) * 100M;
                    return mult.ToString("0.##") + "%";
                }
                else
                {
                    return string.Empty;
                }
            }
        },
        {
            typeof(WeakPower), power =>
            {
                // Displays Weak's % decrease if it's not 25%
                var player = LocalContext.GetMe(RunManager.Instance.State);
                var mult = power.ModifyDamageMultiplicative(player?.Creature, 1M, ValueProp.Move, power.Owner, null, null);
                if (mult != power.DynamicVars[WeakPower._damageDecrease].BaseValue)
                {
                    mult = (1M - mult) * 100M;
                    return mult.ToString("0.##") + "%";
                }
                else
                {
                    return string.Empty;
                }
            }
        },
        { typeof(ToricToughnessPower), power => power.DynamicVars.Block.IntValue },
        {
            typeof(InfernoPower), power =>
            {
                var selfDamage = power.DynamicVars[InfernoPower._selfDamageKey].IntValue;
                return selfDamage != 0 ? new Amount2Data(amount2: selfDamage.ToString(), color2: PowerModel._debuffAmountLabelColor) : string.Empty;
            }
        },
        {
            typeof(CrimsonMantlePower), power =>
            {
                var selfDamage = power.DynamicVars[CrimsonMantlePower._selfDamageKey].IntValue;
                return selfDamage != 0 ? new Amount2Data(amount2: selfDamage.ToString(), color2: PowerModel._debuffAmountLabelColor) : string.Empty;
            }
        },
        {
            typeof(UnmovablePower), power =>
            {
                var usesLeft = power.Amount - CombatManager.Instance.History.Entries.OfType<BlockGainedEntry>()
                    .Count(e =>
                        e.HappenedThisTurn(power.CombatState)
                        && e.CardPlay != null && e.CardPlay.Player.Creature == power.Owner
                        && e.Props.IsCardOrMonsterMove());
                return new Amount2Data(amount1: $"{Math.Max(0, usesLeft)}/{power.DisplayAmount}");
            }
        },
        { typeof(FeralPower), power => new Amount2Data(amount1: $"{power.DisplayAmount}/{power.Amount}") },
    };

    static TwoAmountPowers()
    {
        // Outbreak was reworked in v0.110.0, still show this for v0.108/9.0
        var outbreak = typeof(PowerModel).Assembly.GetType("MegaCrit.Sts2.Core.Models.Powers.OutbreakPower");
        if (outbreak != null && ModManager._gameVersion != null &&
            (ModManager._gameVersion.Minor == 109 || ModManager._gameVersion.Minor == 108))
        {
            DisplaySecondAmount[outbreak] = power => power.Amount;
        }
    }

    private class Amount2Data(string? amount2 = null, string? amount1 = null, Color? color2 = null)
    {
        public readonly string? Amount1 = amount1;
        public readonly string? Amount2 = amount2;
        public readonly Color? Color2 = color2;

        public static implicit operator Amount2Data(int amount2) => new(amount2: amount2.ToString());
        public static implicit operator Amount2Data(string amount2) => new(amount2: amount2);
    }

    [HarmonyPatch(nameof(NPower.RefreshAmount))]
    [HarmonyPostfix]
    static void SetSecondAmountText(NPower __instance)
    {
        if (!__instance.IsNodeReady()) return;

        // Only ever hide minty's label
        var amount2Label = __instance.GetNodeOrNull<MegaLabel>(SecondAmountLabelName);
        if (amount2Label != null)
            amount2Label.Visible = false;
        if (!Handles(__instance._model))
            return;

        var power = __instance.Model;
        var amount2 = DisplaySecondAmount[power.GetType()](power);
        if (!string.IsNullOrEmpty(amount2.Amount1))
            __instance._amountLabel.SetTextAutoSize(amount2.Amount1);
        if (string.IsNullOrEmpty(amount2.Amount2))
            return;

        if (amount2Label == null)
        {
            amount2Label = (MegaLabel)__instance._amountLabel.Duplicate();
            amount2Label.Name = SecondAmountLabelName;
            amount2Label.UniqueNameInOwner = false;
            __instance.AddChild(amount2Label);
            __instance.MoveChild(amount2Label, __instance._amountLabel.GetIndex());
        }

        amount2Label.Visible = true;
        amount2Label.AddThemeColorOverride(ThemeConstants.Label.FontColor, amount2.Color2 ?? power.AmountLabelColor);
        amount2Label.SetTextAutoSize(amount2.Amount2);
        var fontSize = amount2Label.GetThemeFontSize(ThemeConstants.Label.FontSize);
        amount2Label.Position = __instance._amountLabel.Position + new Vector2(0, -(fontSize + 2));
    }

    [HarmonyPatch(nameof(NPower.SubscribeToModelEvents))]
    [HarmonyPostfix]
    static void Subscribe(NPower __instance)
    {
        if (Handles(__instance._model))
        {
            Displays.GetOrCreateValue(__instance.Model).Add(__instance);
        }
    }

    [HarmonyPatch(nameof(NPower.UnsubscribeFromModelEvents))]
    [HarmonyPostfix]
    static void Unsubscribe(NPower __instance)
    {
        if (__instance._model != null && Displays.TryGetValue(__instance.Model, out var displays))
        {
            displays.Remove(__instance);
            if (displays.Count == 0) Displays.Remove(__instance.Model);
        }
    }

    // Refresh the UI directly. A secondary counter change shouldn't emit DisplayAmountChanged
    private static void RefreshDisplays(PowerModel power)
    {
        if (!Displays.TryGetValue(power, out var displays)) 
            return;
        foreach (var display in displays.ToArray())
        {
            if (GodotObject.IsInstanceValid(display) && display.IsNodeReady() && display._model == power)
            {
                display.RefreshAmount();
            }
        }
    }

    [HarmonyPatch]
    static class ExtraRefreshAmountCalls
    {
        [HarmonyPostfix]
        static void CallRefreshAmount(PowerModel __instance)
        {
            RefreshDisplays(__instance);
        }

        [HarmonyTargetMethods]
        static IEnumerable<MethodBase> MethodsToPostfixRefreshAmount2()
        {
            return
            [
                typeof(VoidFormPower).Method(nameof(VoidFormPower.AfterCardPlayed)),
                typeof(VoidFormPower).Method(nameof(VoidFormPower.BeforeSideTurnStart)),
                typeof(JugglingPower).Method(nameof(JugglingPower.AfterApplied)),
                // Juggling increments the counter before its first await
                typeof(JugglingPower).Method(nameof(JugglingPower.BeforeCardPlayed)),
                typeof(JugglingPower).Method(nameof(JugglingPower.AfterSideTurnEnd)),
                typeof(ToricToughnessPower).Method(nameof(ToricToughnessPower.SetBlock)),
                typeof(InfernoPower).Method(nameof(InfernoPower.IncrementSelfDamage)),
                typeof(CrimsonMantlePower).Method(nameof(CrimsonMantlePower.IncrementSelfDamage)),
            ];
        }

        [HarmonyPatch]
        static class SpecificFixes
        {
            private static readonly Dictionary<MethodBase, HashSet<Type>> AfterHookPowers = new()
            {
                { typeof(Hook).Method(nameof(Hook.AfterCardPlayed)).PatchAsync(), [typeof(PaleBlueDotPower)] },
                { typeof(Hook).Method(nameof(Hook.AfterPowerAmountChanged)).PatchAsync(), [typeof(VulnerablePower), typeof(WeakPower)] },
                { typeof(Hook).Method(nameof(Hook.AfterBlockGained)).PatchAsync(), [typeof(UnmovablePower)] },
                { typeof(Hook).Method(nameof(Hook.AfterPlayerTurnStart)).PatchAsync(), [typeof(UnmovablePower)] },
            };

            private static void AfterHook(AbstractModel model, MethodBase method)
            {
                // Get original, unpatched method so we can use it as a lookup key properly
                if (method is MethodInfo methodInfo)
                    method = Harmony.GetOriginalMethod(methodInfo);

                if (model is PowerModel power)
                {
                    if (AfterHookPowers.TryGetValue(method, out var powers) && powers.Contains(power.GetType()))
                        CallRefreshAmount(power);
                }
            }

            [HarmonyTargetMethods]
            static IEnumerable<MethodBase> HooksToRefreshAmount2After()
            {
                return AfterHookPowers.Keys;
            }

            [HarmonyTranspiler]
            static IEnumerable<CodeInstruction> RefreshAfterCardPlayed(IEnumerable<CodeInstruction> instructions)
            {
                var codeMatcher = new CodeMatcher(instructions);

                codeMatcher
                    .MatchStartForward(
                        CodeMatch.Calls(typeof(AbstractModel).Method(nameof(AbstractModel.InvokeExecutionFinished)))
                    )
                    .ThrowIfInvalid("Failed to find InvokeExecutionFinished()")
                    .InsertAndAdvance(
                        new CodeInstruction(OpCodes.Dup),
                        CodeInstruction.Call(() => MethodBase.GetCurrentMethod()),
                        new CodeInstruction(OpCodes.Call, typeof(SpecificFixes).Method(nameof(AfterHook)))
                    );

                return codeMatcher.Instructions();
            }

            [HarmonyPatch(typeof(PowerModel), nameof(PowerModel.RemoveInternal))]
            static class VulnWeakOnDebilitateRemoval
            {
                [HarmonyPostfix]
                static void RefreshVulnWeak(PowerModel __instance)
                {
                    if (__instance is not DebilitatePower) return;
                    foreach (var powerModel in __instance.Owner.Powers.Where(p => p is VulnerablePower or WeakPower))
                    {
                        CallRefreshAmount(powerModel);
                    }
                }
            }
        }
    }
}