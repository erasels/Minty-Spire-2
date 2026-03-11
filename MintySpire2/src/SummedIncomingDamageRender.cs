using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MintySpire2.MintySpire2.src.util;

namespace MintySpire2.MintySpire2.src;

/// <summary>
///     Adds a small text label to the Right of the health bar when the health bar is visible
///     and the owner creature is the player.
/// </summary>
[HarmonyPatch(typeof(NHealthBar))]
public static class SummedIncomingDamageRender
{
    private const string RightTextNodeName = "ModIncomingDamageText";
    private const float RightPadding = 6f;

    private static readonly FieldInfo CreatureField = AccessTools.Field(typeof(NHealthBar), "_creature");
    private static readonly WeakNodeRegistry<NHealthBar> ValidBars = new();

    private static Node GetLabelHost(NHealthBar bar)
    {
        var preferredHost = bar.HpBarContainer?.GetParent();
        return IncomingDamageDisplayPolicy.ResolveLabelHost(preferredHost, bar);
    }

    private static IEnumerable<Node> GetLabelHosts(NHealthBar bar)
    {
        var preferredHost = bar.HpBarContainer?.GetParent();
        if (preferredHost != null)
            yield return preferredHost;

        if (!ReferenceEquals(preferredHost, bar))
            yield return bar;
    }

    private static Label? GetLabel(NHealthBar bar)
    {
        foreach (var host in GetLabelHosts(bar))
        {
            var label = host.GetNodeOrNull<Label>(RightTextNodeName);
            if (label != null)
                return label;
        }

        return null;
    }

    /// <summary>
    ///     After a creature is assigned, create label node if it doesn't exist.
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(nameof(NHealthBar.SetCreature))]
    public static void SetCreature_Postfix(NHealthBar __instance)
    {
        CreateLabelIfNotExist(__instance);
    }
    
    /// <summary>
    ///     Refresh labels when a creature death is fired to recalculate incoming damage immediately.
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Creature), nameof(Creature.InvokeDiedEvent))]
    public static void InvokeDiedEvent_Postfix()
    {
        ValidBars.ForEachLive(RefreshVisibilityAndText);
    }


    /// <summary>
    ///     Whenever the bar is updated, update the text display (this is overkill)
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(nameof(NHealthBar.RefreshValues))]
    public static void RefreshValues_Postfix(NHealthBar __instance)
    {
        ValidBars.Register(__instance);
        RefreshVisibilityAndText(__instance);
    }

    /// <summary>
    ///     When the container size is about to change, reposition the label
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(NHealthBar), "SetHpBarContainerSizeWithOffsets")]
    public static void SetHpBarContainerSizeWithOffsets_Postfix(NHealthBar __instance, Vector2 size)
    {
        RepositionLabel(__instance, size);
    }

    /// <summary>
    ///     Creates the label once and attach it near the HP bar container.
    /// </summary>
    /// <returns>bool: Was label created</returns>
    private static bool CreateLabelIfNotExist(NHealthBar bar)
    {
        if (GetLabel(bar) != null)
            return false;

        // Parent to the same node that holds the bar so coordinates are consistent.
        var container = bar.HpBarContainer;
        if (container == null)
            return false;

        var label = new Label
        {
            Name = RightTextNodeName,
            Text = "",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false
        };
        
        var font = GD.Load<Font>("res://fonts/kreon_bold.ttf");
        if (font != null)
            label.AddThemeFontOverride((StringName)"font", font);
        label.HorizontalAlignment = HorizontalAlignment.Left;
        label.VerticalAlignment = VerticalAlignment.Center;
        label.AddThemeColorOverride("font_color", Colors.Salmon);
        label.AddThemeFontSizeOverride("font_size", 14);

        // Add to the same parent as the bar container so it's position relative to it.
        GetLabelHost(bar).AddChild(label);
        RepositionLabel(bar, container.Size);
        return true;
    }

    /// <summary>
    ///     Positions the label to the Right of the HP bar container.
    /// </summary>
    private static void RepositionLabel(NHealthBar bar, Vector2 newSize)
    {
        var label = GetLabel(bar);
        if (label == null) return;

        var container = bar.HpBarContainer;
        if (container == null)
            return;

        // Positioning for the label.
        var labelWidth = 20f;
        var labelHeight = newSize.Y;

        label.Size = new Vector2(labelWidth, labelHeight);
        label.Position = new Vector2(
            container.Position.X + newSize.X + RightPadding,
            container.Position.Y - labelHeight / 4
        );
    }

    /// <summary>
    ///     Shows/hides the label and sets its text.
    ///     Only visible when the health bar is visible, the creature is the player and its their turn.
    /// </summary>
    private static void RefreshVisibilityAndText(NHealthBar bar)
    {
        var label = GetLabel(bar);
        if (label == null)
            return;

        var creature = CreatureField?.GetValue(bar) as Creature;
        var combatManager = CombatManager.Instance;
        var shouldHideLabel = IncomingDamageDisplayPolicy.ShouldHideLabel(
            barVisible: bar.Visible,
            hasCombatManager: combatManager != null,
            isEnemyTurnStarted: combatManager?.IsEnemyTurnStarted ?? false,
            isPlayerOwnedBar: creature?.Player != null,
            hasCombatState: creature?.CombatState != null,
            hasHittableEnemies: creature?.CombatState?.HittableEnemies != null
        );

        if (shouldHideLabel)
        {
            label.Visible = false;
            return;
        }

        var incomingDamage = CalculateIncomingDamage(creature!);
        if (incomingDamage > 0)
        {
            label.Text = $"←{incomingDamage}";
            label.Visible = true;
            return;
        }

        label.Visible = false;
    }

    /// <summary>
    ///     Calculate the incoming damage from common sources such as monsters and powers.
    /// </summary>
    /// <param name="creature">The Player creature that we'll calculate the incoming damage for.</param>
    private static int CalculateIncomingDamage(Creature creature)
    {
        // Collect incoming damage from all hittable monsters (can untargetable monsters attack?).
        var incomingDamage = 0;
        var hittableEnemies = creature.CombatState?.HittableEnemies;
        if (hittableEnemies == null)
            return incomingDamage;

        foreach (var hittableEnemy in hittableEnemies)
        {
            if (hittableEnemy == null)
                continue;

            var intents = hittableEnemy.Monster?.NextMove?.Intents;
            if (intents == null)
                continue;

            foreach (var intent in intents)
            {
                if (intent is AttackIntent attackIntent && intent.IntentType is IntentType.Attack or IntentType.DeathBlow)
                    incomingDamage += attackIntent.GetTotalDamage([creature], hittableEnemy);
            }
        }

        // Knowledge demon end of turn damage
        incomingDamage += creature.Player?.Creature.GetPower<DisintegrationPower>()?.Amount ?? 0;

        return incomingDamage;
    }
}
