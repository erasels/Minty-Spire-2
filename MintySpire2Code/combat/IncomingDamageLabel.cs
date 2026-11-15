using Godot;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MintySpire2.MintySpire2Code.config;

namespace MintySpire2.MintySpire2Code.combat;

public partial class IncomingDamageLabel : MegaLabel
{
    public string Breakdown { get; set; } = "";

    private bool _hovered;
    private string? _shownBreakdown;

    public override void _Ready()
    {
        base._Ready();
        MouseEntered += () => _hovered = true;
        MouseExited += () =>
        {
            _hovered = false;
            RemoveTooltip();
        };
        VisibilityChanged += () =>
        {
            if (!IsVisibleInTree())
            {
                _hovered = false;
                RemoveTooltip();
            }
        };
        TreeExiting += RemoveTooltip;
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        var enabled = Config.ShowIncomingDamage && Config.ShowIncomingDamageTooltip;
        MouseFilter = enabled ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
        if (!enabled || !_hovered || !IsVisibleInTree() || Breakdown.Length == 0)
        {
            RemoveTooltip();
            return;
        }

        if (_shownBreakdown == Breakdown) return;
        RemoveTooltip();
        var tip = new HoverTip(new LocString("gameplay_ui", "MINTYSPIRE2.incomingDamage.title"), Breakdown);
        var tipSet = NHoverTipSet.CreateAndShow(this, tip, HoverTip.GetHoverTipAlignment(this));
        if (tipSet == null) return;
        tipSet.SetFollowOwner();
        _shownBreakdown = Breakdown;
    }

    private void RemoveTooltip()
    {
        if (_shownBreakdown == null) return;
        NHoverTipSet.Remove(this);
        _shownBreakdown = null;
    }
}
