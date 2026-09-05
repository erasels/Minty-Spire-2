using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Nodes.Cards;

namespace MintySpire2.MintySpire2Code.config;

internal partial class EnchantedCardPreview : Control
{
    private const float PreviewScale = 0.80f;
    private const float PreviewVerticalPadding = 20f;

    private NCard? _cardNode;
    private string _previousColorHex = Config.HighlightEnchantsColor;
    private string _previousOutlineColorHex =  Config.HighlightEnchantsOutlineColor;
    private bool _previousHighlightEnabled = Config.HighlightEnchants;
    private bool _isFirstProcessPass = true;

    public EnchantedCardPreview()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SizeFlagsHorizontal = SizeFlags.Fill | SizeFlags.Expand;
        SizeFlagsVertical = SizeFlags.ShrinkBegin;
    }

    public override void _Ready()
    {
        base._Ready();

        var previewCard = ModelDb.Card<StrikeIronclad>().ToMutable();
        CardCmd.Enchant<Sharp>(previewCard, 1m);

        _cardNode = NCard.Create(previewCard);
        if (_cardNode == null)
            return;

        _cardNode.MouseFilter = MouseFilterEnum.Ignore;
        _cardNode.Scale = Vector2.One * PreviewScale;
        AddChild(_cardNode);

        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        base._Process(delta);

        // Need to update visuals on the first time through so the card displays properly
        // If we call this in the _Ready() function, the container layout isn't updated yet so Size is zero, and it displays on the left
        if (_isFirstProcessPass)
        {
            _isFirstProcessPass = false;
            UpdatePreview();
            return;
        }
        
        // Reapply visuals if something changed
        if (Config.HighlightEnchantsColor != _previousColorHex || Config.HighlightEnchantsOutlineColor != _previousOutlineColorHex || Config.HighlightEnchants != _previousHighlightEnabled)
            UpdatePreview();
    }

    private void UpdatePreview()
    {
        if (_cardNode == null)
            return;

        _cardNode.Visible = Config.HighlightEnchants;
        
        if (!Config.HighlightEnchants)
        {
            // remove empty space when card is not visible
            CustomMinimumSize = Vector2.Zero;
            _previousHighlightEnabled = false;
            return;
        }

        var scaledCardSize = NCard.defaultSize * PreviewScale;
        CustomMinimumSize = new Vector2(0f, scaledCardSize.Y + PreviewVerticalPadding);

        // centering the card
        var cardX = Size.X * 0.5f;
        var cardY = PreviewVerticalPadding + scaledCardSize.Y * 0.5f;
        _cardNode.Position = new Vector2(cardX, cardY);

        _previousColorHex = Config.HighlightEnchantsColor;
        _previousOutlineColorHex = Config.HighlightEnchantsOutlineColor;
        _previousHighlightEnabled = Config.HighlightEnchants;
        
        _cardNode.UpdateVisuals(PileType.None, CardPreviewMode.Normal);
    }
}
