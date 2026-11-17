using Godot;
using HarmonyLib;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Runs;
using MintySpire2.MintySpire2Code.util;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.ValueProps;
using MintySpire2.MintySpire2Code.config;

namespace MintySpire2.MintySpire2Code.combat;

/// <summary>
///     Adds a small text label to the Right of the health bar when the health bar is visible
///     and the owner creature is the player.
/// </summary>
[HarmonyPatch(typeof(NHealthBar))]
public static class SummedIncomingDamageRender
{
    private const string RightTextNodeName = "MintyIncomingDamageText";
    private const float RightPadding = 6f;

    private static readonly WeakNodeRegistry<NHealthBar> ValidBars = new();

    /// <summary>
    ///     After a creature is assigned, create label node if it doesn't exist.
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(nameof(NHealthBar.SetCreature))]
    public static void CatchBarSet(NHealthBar __instance)
    {
        if (LocalContext.IsMe(__instance._creature))
        {
            CreateLabelIfNotExist(__instance);
        }
    }

    /// <summary>
    ///     Whenever the bar is updated, update the text display (this is overkill)
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(nameof(NHealthBar.RefreshValues))]
    public static void CatchBarRefresh(NHealthBar __instance)
    {
        if (LocalContext.IsMe(__instance._creature))
        {
            CreateLabelIfNotExist(__instance);
            ValidBars.Register(__instance);
            RefreshVisibilityAndText(__instance);
        }
    }

    /// <summary>
    ///     When the container size changes, reposition the label.
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(NHealthBar), "SetHpBarContainerSizeWithOffsetsImmediately")]
    public static void CatchBarResize(NHealthBar __instance, Vector2 size)
    {
        if (LocalContext.IsMe(__instance._creature))
        {
            RepositionLabel(__instance, size);
        }
    }

    /// <summary>
    ///     Creates the label once and attach it near the HP bar container.
    /// </summary>
    /// <returns>bool: Was label created</returns>
    private static bool CreateLabelIfNotExist(NHealthBar bar)
    {
        if (bar.HasNode(RightTextNodeName))
            return false;

        var container = bar.HpBarContainer;

        var label = new IncomingDamageLabel
        {
            Name = RightTextNodeName,
            Text = "",
            AutoSizeEnabled = false,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false
        };

        var font = GD.Load<Font>("res://fonts/kreon_bold.ttf");
        if (font != null)
            label.AddThemeFontOverride((StringName)"font", font);
        label.HorizontalAlignment = HorizontalAlignment.Left;
        label.VerticalAlignment = VerticalAlignment.Center;
        label.AddThemeColorOverride("font_color", Colors.Salmon);
        label.AddThemeFontSizeOverride("font_size", Config.IncomingDamageSize);

        bar.AddChild(label);
        RepositionLabel(bar, container.Size);
        return true;
    }

    /// <summary>
    ///     Positions the label to the Right of the HP bar container.
    /// </summary>
    private static void RepositionLabel(NHealthBar bar, Vector2 newSize)
    {
        var label = bar.GetNode(RightTextNodeName) as Label;
        if (label == null) return;

        var container = bar.HpBarContainer;

        // Positioning for the label.
        var fontSize = Config.IncomingDamageSize;
        var font = label.GetThemeFont("font");

        // Use the font's real line height instead of the HP bar height.
        var labelWidth = Math.Max(20f, font.GetStringSize(label.Text, fontSize: fontSize).X);
        var labelHeight = font?.GetHeight(fontSize) ?? fontSize;

        label.Size = new Vector2(labelWidth, labelHeight);

        var containerOffset = new Vector2(
            newSize.X + RightPadding,
            (newSize.Y - labelHeight) / 2f
        );
        label.GlobalPosition = container.GetGlobalTransform() * containerOffset;
    }

    /// <summary>
    ///     Shows/hides the label and sets its text.
    ///     Only visible when the health bar is visible, the creature is the player, and it's their turn.
    /// </summary>
    private static void RefreshVisibilityAndText(NHealthBar bar)
    {
        if (bar.GetNode(RightTextNodeName) is not IncomingDamageLabel label)
            return;

        if (!Config.ShowIncomingDamage || CombatManager.Instance.IsEnemyTurnStarted)
        {
            label.Visible = false;
            return;
        }

        // Only show for the player-owned health bar.
        var creature = bar._creature;
        if (creature?.Player == null || creature.CombatState == null)
        {
            label.Visible = false;
            return;
        }

        var breakdown = new Dictionary<string, int>();
        var incomingDamage = CalculateIncomingDamage(creature, breakdown);
        label.Breakdown = string.Join("\n", breakdown.Select(entry => $"+ [blue]{entry.Value}[/blue] {entry.Key}"));
        if (incomingDamage > 0)
        {
            label.Text = $"←{incomingDamage}";
            label.AddThemeFontSizeOverride("font_size", Config.IncomingDamageSize);
            RepositionLabel(bar, bar.HpBarContainer.Size);
            label.Visible = true;
            return;
        }

        label.Visible = false;
    }

    /// <summary>
    ///     Refresh labels when a creature death is fired to recalculate incoming damage immediately.
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Creature), nameof(Creature.InvokeDiedEvent))]
    public static void CatchMonsterDeath()
    {
        ValidBars.ForEachLive(RefreshVisibilityAndText);
    }

    private static int GetIncomingCardDamage(Player player, Dictionary<string, int> breakdown)
    {
        if (!Config.ShowIncomingDamage) return 0;

        var handPile = CardPile.Get(PileType.Hand, player);
        if (handPile == null)
            return 0;

        int totalDamage = 0;

        foreach (var card in  handPile.Cards)
        {
            if (!CardTurnEndInspector.DoesTurnEndInHandCallDamage(card))
                continue;

            // Ignoring HP loss cards here
            var damage = card.DynamicVars.Values.OfType<DamageVar>().Sum(cvar => GetModifiedIncomingCardDamage(player, card, cvar));
            totalDamage += damage;
            AddDamage(breakdown, card.Title, damage);
        }

        return totalDamage;
    }

    private static int GetModifiedIncomingCardDamage(Player player, CardModel card, DamageVar damageVar)
    {
        var creature = player.Creature;
        if (creature.CombatState == null)
            return damageVar.IntValue;

        var damage = Hook.ModifyDamage(
            player.RunState,
            creature.CombatState,
            creature,
            creature,
            damageVar.BaseValue,
            damageVar.Props,
            card,
            null,
            ModifyDamageHookType.All,
            CardPreviewMode.None,
            out _);

        return Math.Max(0, (int)damage);
    }
    
    private static int GetModifiedIncomingPowerDamage(Creature creature, decimal baseDamage, ValueProp props, Creature? dealer)
    {
        if (creature.CombatState == null) return Math.Max(0, (int)baseDamage);

        var damage = Hook.ModifyDamage(
            RunManager.Instance.State,
            creature.CombatState,
            creature,
            dealer,
            baseDamage,
            props,
            null,
            null,
            ModifyDamageHookType.All,
            CardPreviewMode.None,
            out _);

        return Math.Max(0, (int)damage);
    }

    /// <summary>
    ///     Refresh labels if the hand changes in case end turn damage cards are added
    /// </summary>
    /// <param name="__instance"></param>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(CardPile), "InvokeContentsChanged")]
    private static void CatchHandChange(CardPile __instance)
    {
        if (!Config.ShowIncomingDamage) return;
        if (__instance is { Type: PileType.Hand })
        {
            var player = LocalContext.GetMe(RunManager.Instance.State);
            if (player?.PlayerCombatState == null) return;

            ValidBars.ForEachLive(RefreshVisibilityAndText);
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Wither), nameof(Wither.FakeUpgrade))]
    private static void CatchWitherFakeUpgrade(Wither __instance)
    {
        if (!Config.ShowIncomingDamage || __instance.Pile is not { Type: PileType.Hand }) return;

        var player = LocalContext.GetMe(RunManager.Instance.State);
        if (player?.PlayerCombatState == null || __instance.Owner != player) return;

        ValidBars.ForEachLive(RefreshVisibilityAndText);
    }

    /// <summary>
    ///     Calculate the incoming damage from common sources such as monsters and powers.
    /// </summary>
    /// <param name="creature">The Player creature that we'll calculate the incoming damage for.</param>
    /// <param name="breakdown">Dictionary in which incoming damage and source name is kept track of.</param>
    private static int CalculateIncomingDamage(Creature creature, Dictionary<string, int> breakdown)
    {
        if (creature.CombatState == null) return 0;

        // Collect incoming damage from all living monsters.
        var incomingDamage = 0;
        foreach (var enemy in creature.CombatState.Enemies.Where(enemy => enemy.IsAlive))
        {
            foreach (var intent in enemy.Monster.NextMove.Intents)
            {
                if (intent.IntentType is IntentType.Attack or IntentType.DeathBlow)
                    incomingDamage += ((AttackIntent)intent).GetTotalDamage([creature], enemy);
            }
        }

        AddDamage(breakdown, new LocString("gameplay_ui", "MINTYSPIRE2.incomingDamage.monsters").GetFormattedText(), incomingDamage);

        // Knowledge demon end of turn damage
        var disintegration = creature.GetPower<DisintegrationPower>();
        if (disintegration != null)
        {
            var powerDamage = GetModifiedIncomingPowerDamage(creature, disintegration.Amount, ValueProp.Unpowered, creature);
            incomingDamage += powerDamage;
            AddDamage(breakdown, disintegration.Title.GetFormattedText(), powerDamage);
        }

        // Constrict power
        var constrict = creature.GetPower<ConstrictPower>();
        if (constrict != null)
        {
            var powerDamage = GetModifiedIncomingPowerDamage(creature, constrict.Amount, ValueProp.Unpowered, creature);
            incomingDamage += powerDamage;
            AddDamage(breakdown, constrict.Title.GetFormattedText(), powerDamage);
        }

        if (CompatHelper.GetInsatiableDominionPower(creature) is { } dominion)
        {
            var damage = dominion.DynamicVars.Damage;
            var powerDamage = GetModifiedIncomingPowerDamage(creature, damage.BaseValue, damage.Props, dealer: null);
            incomingDamage += powerDamage;
            AddDamage(breakdown, dominion.Title.GetFormattedText(), powerDamage);
        }

        // End turn self damage cards can be affected by current powers.
        if (creature.Player != null)
            incomingDamage += GetIncomingCardDamage(creature.Player, breakdown);

        return incomingDamage;
    }
    private static void AddDamage(Dictionary<string, int> breakdown, string name, int damage)
    {
        if (damage > 0)
            breakdown[name] = breakdown.GetValueOrDefault(name) + damage;
    }
}
