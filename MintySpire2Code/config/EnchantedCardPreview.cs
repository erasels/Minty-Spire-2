using Godot;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards;

namespace MintySpire2.MintySpire2Code;

internal partial class EnchantedCardPreview : Control
{
    private const float PreviewScale = 0.80f;
    private const float PreviewVerticalPadding = 20f;

    private NCard? _cardNode;
    private Color _lastTitleColor;
    private Color _lastOutlineColor;
    private bool _lastHighlightEnabled;

    public EnchantedCardPreview()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SizeFlagsHorizontal = SizeFlags.Fill | SizeFlags.Expand;
        SizeFlagsVertical = SizeFlags.ShrinkBegin;
    }

    public override void _Ready()
    {
        base._Ready();

        _cardNode = NCard.Create(ModelDb.Card<StrikeIronclad>());
        if (_cardNode == null)
            return;

        _cardNode.MouseFilter = MouseFilterEnum.Ignore;
        _cardNode.Scale = Vector2.One * PreviewScale;
        AddChild(_cardNode);

        SetProcess(true);
        UpdatePreview();
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        UpdatePreview();
    }

    private void UpdatePreview()
    {
        if (_cardNode == null)
            return;

        _cardNode.Visible = Config.HighlightEnchants;

        if (!Config.HighlightEnchants)
        {
            CustomMinimumSize = Vector2.Zero;
            _lastHighlightEnabled = false;
            return;
        }

        var scaledCardSize = NCard.defaultSize * PreviewScale;
        CustomMinimumSize = new Vector2(0f, scaledCardSize.Y + PreviewVerticalPadding);

        var cardX = Size.X * 0.5f;
        var cardY = PreviewVerticalPadding + scaledCardSize.Y * 0.5f;
        _cardNode.Position = new Vector2(cardX, cardY);

        var titleColor = new Color(Config.HighlightEnchantsColor);
        var outlineColor = new Color(Config.HighlightEnchantsOutlineColor);

        // Skip reapplying visuals if nothing changed
        if (titleColor == _lastTitleColor && outlineColor == _lastOutlineColor && Config.HighlightEnchants == _lastHighlightEnabled)
            return;

        _lastTitleColor = titleColor;
        _lastOutlineColor = outlineColor;
        _lastHighlightEnabled = Config.HighlightEnchants;

        _cardNode.UpdateVisuals(PileType.None, CardPreviewMode.Normal);

        var titleLabel = _cardNode.GetNodeOrNull<MegaLabel>("%TitleLabel");
        if (titleLabel == null)
            return;

        titleLabel.AddThemeColorOverride(ThemeConstants.Label.FontColor, titleColor);
        titleLabel.AddThemeColorOverride(ThemeConstants.Label.FontOutlineColor, outlineColor);
    }
}
