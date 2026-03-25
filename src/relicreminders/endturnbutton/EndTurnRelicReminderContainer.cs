using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace MintySpire2.relicreminders.endturnbutton;

public partial class EndTurnRelicReminderContainer : HBoxContainer
{
    [HarmonyPatch] 
    static class EndTurnRelicReminderContainerPatch
    {
        private const string ContainerNodeName = "MintyEndTurnRelicReminderContainer";

        [HarmonyPatch(typeof(NEndTurnButton), nameof(NEndTurnButton._Ready))]
        [HarmonyPostfix]
        public static void OnETBReady(NEndTurnButton __instance)
        {
            if (__instance.GetNodeOrNull<EndTurnRelicReminderContainer>(ContainerNodeName) != null)
                return;

            var container = new EndTurnRelicReminderContainer { Name = ContainerNodeName };
            __instance.AddChild(container);
        }
    }
    
    private const float IconScale = 0.4f;
    private const float FadeDuration = 0.2f;

    private readonly Dictionary<string, TextureRect> _icons = new();
    private readonly Dictionary<string, Tween> _tweens = new();

    public override void _Ready()
    {
        AnchorLeft = 0f;
        AnchorRight = 0f;
        AnchorTop = 1f;
        AnchorBottom = 1f;
        OffsetLeft = 20f;
        OffsetRight = 0f;
        OffsetTop = -2f;
        OffsetBottom = 20f;

        Alignment = AlignmentMode.Center;
        AddThemeConstantOverride("separation", 4);
        MouseFilter = MouseFilterEnum.Ignore;
        Visible = false;

        EndTurnRelicReminderService.RemindersChanged += Refresh;
        Refresh();
    }

    public override void _ExitTree()
    {
        EndTurnRelicReminderService.RemindersChanged -= Refresh;
    }

    private void Refresh()
    {
        var reminders = EndTurnRelicReminderService.GetCurrentReminders();
        var targetSet = reminders.Select(r => r.Id.Entry).ToHashSet();

        foreach (var existing in _icons.Keys.ToList())
        {
            if (!targetSet.Contains(existing))
                FadeOutAndRemove(existing);
        }

        foreach (var reminder in reminders)
        {
            var reminderId = reminder.Id.Entry;
            if (_icons.ContainsKey(reminderId))
                continue;

            var texture = reminder.Icon;

            var iconSize = new Vector2(texture.GetWidth(), texture.GetHeight()) * IconScale;
            var icon = new TextureRect
            {
                Texture = texture,
                ExpandMode = TextureRect.ExpandModeEnum.FitWidthProportional,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                CustomMinimumSize = iconSize,
                MouseFilter = MouseFilterEnum.Ignore,
                Modulate = new Color(1f, 1f, 1f, 0f),
            };

            AddChild(icon);
            _icons[reminderId] = icon;
            FadeTo(reminderId, icon, 1f);
        }

        Visible = _icons.Count > 0;
    }

    private void FadeOutAndRemove(string reminderId)
    {
        if (!_icons.TryGetValue(reminderId, out var icon))
            return;

        FadeTo(reminderId, icon, 0f, () =>
        {
            if (_icons.Remove(reminderId))
                icon.QueueFree();
            if (_icons.Count == 0)
                Visible = false;
        });
    }

    private void FadeTo(string reminderId, CanvasItem icon, float alpha, Action? onComplete = null)
    {
        if (_tweens.Remove(reminderId, out var activeTween))
            activeTween.Kill();

        var tween = CreateTween();
        tween.TweenProperty(icon, "modulate:a", alpha, FadeDuration);
        if (onComplete != null)
            tween.TweenCallback(Callable.From(onComplete));

        _tweens[reminderId] = tween;
    }
}
