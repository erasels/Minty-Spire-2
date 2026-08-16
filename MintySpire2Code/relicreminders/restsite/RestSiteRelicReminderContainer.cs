using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Nodes.RestSite;

namespace MintySpire2.MintySpire2Code.relicreminders.restsite;

/// <summary>
///     A row of small relic icons shown below a <see cref="NRestSiteButton"/>, indicating which owned
///     relics affect that option. Applicable relics are defined in <see cref="RestSiteRelicReminderService"/>.
/// </summary>
public partial class RestSiteRelicReminderContainer : HBoxContainer
{
    [HarmonyPatch]
    private static class RestSiteRelicReminderContainerPatch
    {
        private const string ContainerNodeName = "MintyRestSiteRelicReminderContainer";

        [HarmonyPatch(typeof(NRestSiteButton), nameof(NRestSiteButton._Ready))]
        [HarmonyPostfix]
        public static void OnButtonReady(NRestSiteButton __instance)
        {
            EnsureAndRefresh(__instance);
        }

        [HarmonyPatch(typeof(NRestSiteButton), "Reload")]
        [HarmonyPostfix]
        public static void OnButtonReload(NRestSiteButton __instance)
        {
            EnsureAndRefresh(__instance);
        }

        private static void EnsureAndRefresh(NRestSiteButton button)
        {
            if (!Config.OutOfCombatReminders)
                return;

            var container = button.GetNodeOrNull<RestSiteRelicReminderContainer>(ContainerNodeName);
            if (container == null)
            {
                container = new RestSiteRelicReminderContainer { Name = ContainerNodeName };
                button.AddChild(container);
            }

            container.Refresh();
        }
    }

    private const float IconSize = 40f;

    private readonly Dictionary<string, TextureRect> _icons = new();

    public override void _Ready()
    {
        // Sits just below the option label (aka "Heal" for Rest)
        AnchorLeft = 0f;
        AnchorRight = 1f;
        AnchorTop = 1f;
        AnchorBottom = 1f;
        OffsetLeft = 0f;
        OffsetRight = 0f;
        OffsetTop = 48f;
        OffsetBottom = 88f;

        Alignment = AlignmentMode.Center;
        AddThemeConstantOverride("separation", 4);
        MouseFilter = MouseFilterEnum.Ignore;
        Visible = false;
    }
    
    public void Refresh()
    {
        if (GetParent() is not NRestSiteButton button)
        {
            ClearIcons();
            return;
        }

        RestSiteOption option;
        try
        {
            option = button.Option;
        }
        catch (InvalidOperationException)
        {
            // Option accessed before being set; nothing to show yet.
            ClearIcons();
            return;
        }

        var player = option.Owner;
        if (!LocalContext.IsMe(player))
        {
            ClearIcons();
            return;
        }

        var reminders = RestSiteRelicReminderService.GetReminders(option.OptionId, player);
        var targetSet = reminders.Select(r => r.Id.Entry).ToHashSet();

        // Remove icons that are no longer relevant
        foreach (var relicId in _icons.Keys.ToList())
        {
            if (!targetSet.Contains(relicId))
            {
                _icons[relicId].QueueFree();
                _icons.Remove(relicId);
            }
        }

        // Add icons for relevant relics
        foreach (var relic in reminders)
        {
            var relicId = relic.Id.Entry;
            if (_icons.ContainsKey(relicId))
                continue;

            var icon = new TextureRect
            {
                Texture = relic.Icon,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                CustomMinimumSize = new Vector2(IconSize, IconSize),
                MouseFilter = MouseFilterEnum.Ignore,
            };

            AddChild(icon);
            _icons[relicId] = icon;
        }

        Visible = _icons.Count > 0;
    }

    private void ClearIcons()
    {
        foreach (var icon in _icons.Values)
            icon.QueueFree();
        _icons.Clear();
        Visible = false;
    }
}