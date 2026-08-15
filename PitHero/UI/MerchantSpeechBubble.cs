using Microsoft.Xna.Framework;
using Nez;
using Nez.BitmapFonts;
using Nez.Textures;
using Nez.UI;
using System.Text;

namespace PitHero.UI
{
    /// <summary>
    /// Stage-space speech bubble for the Second Chance merchant (issue #385). Mirrors the look
    /// of <see cref="ECS.Components.SpeechBubbleComponent"/> (nine-patch bubble + tail +
    /// typewriter reveal) but lives on the UI stage, anchored above the merchant sprite, and is
    /// sized to the full wrapped text (no scroll-and-drop). Unlike world bubbles it never
    /// auto-hides: once the reveal completes the full text persists until <see cref="Hide"/>
    /// (shop close). The caller ticks it with unscaled time because the shop pauses gameplay
    /// while open and fast-forward scales <see cref="Time.DeltaTime"/>.
    /// </summary>
    public class MerchantSpeechBubble : Element
    {
        private const string AtlasPath = "Content/Atlases/UI.atlas";
        private const string BubbleSpriteName = "NinePatchSpeechBubble";
        private const string TailSpriteName = "SpeechBubbleTail";

        private const int TailSpriteW = 9;
        private const int TailSpriteH = 8;

        private const float TextWrapWidth = GameConfig.SpeechBubbleWidth - GameConfig.SpeechBubblePadding * 2;

        private static readonly Color BubbleColor = Color.White;
        private static readonly Color TextColor = new Color(50, 30, 20);

        // Null when asset loading failed — the bubble then stays inert
        private NinePatchDrawable _bubbleDrawable;
        private Sprite _tailSprite;
        private BitmapFont _font;

        // Tail-tip anchor in stage coordinates; bubble body is drawn centered above it
        private float _anchorX;
        private float _anchorY;

        // Reveal state
        private bool _active;
        private string _wrappedText;
        private readonly StringBuilder _visibleText = new StringBuilder(128);
        private int _revealed;
        private float _accumulated;
        private float _bubbleHeight;

        public MerchantSpeechBubble()
        {
            try
            {
                var uiAtlas = Core.Content.LoadSpriteAtlas(AtlasPath);
                var bubbleSprite = uiAtlas?.GetSprite(BubbleSpriteName);
                _tailSprite = uiAtlas?.GetSprite(TailSpriteName);
                _font = Core.Content.LoadBitmapFont(GameConfig.FontPathSpeechBubble);

                if (bubbleSprite == null || _tailSprite == null || _font == null)
                {
                    Debug.Warn("[MerchantSpeechBubble] Missing bubble sprites or font — bubble disabled");
                    return;
                }

                _bubbleDrawable = new NinePatchDrawable(
                    new NinePatchSprite(bubbleSprite.Texture2D, bubbleSprite.SourceRect, 4, 4, 4, 4));
            }
            catch (System.Exception ex)
            {
                Debug.Warn($"[MerchantSpeechBubble] Initialisation failed: {ex.Message}");
            }

            SetTouchable(Touchable.Disabled);
            SetVisible(false);
        }

        /// <summary>
        /// Sets the tail-tip position in stage coordinates. Call before <see cref="Show"/>.
        /// </summary>
        public void SetTailAnchor(float x, float y)
        {
            _anchorX = x;
            _anchorY = y;
        }

        /// <summary>
        /// Starts the typewriter reveal of <paramref name="localizedText"/>. The bubble is sized
        /// to the full wrapped text so every line stays visible once revealed.
        /// </summary>
        public void Show(string localizedText)
        {
            if (_bubbleDrawable == null || string.IsNullOrEmpty(localizedText))
                return;

            _wrappedText = _font.WrapText(localizedText, TextWrapWidth);

            int lineCount = 1;
            for (int i = 0; i < _wrappedText.Length; i++)
            {
                if (_wrappedText[i] == '\n')
                    lineCount++;
            }
            _bubbleHeight = GameConfig.SpeechBubblePadding * 2 + lineCount * _font.LineHeight;

            _visibleText.Clear();
            _revealed = 0;
            _accumulated = 0f;
            _active = true;

            // Real bounds so OutsideClickDismissal's envelope math sees the bubble
            float totalHeight = _bubbleHeight + TailSpriteH - GameConfig.SpeechBubbleTailOverlap;
            SetBounds(_anchorX - GameConfig.SpeechBubbleWidth / 2f, _anchorY - totalHeight,
                GameConfig.SpeechBubbleWidth, totalHeight);
            SetVisible(true);
        }

        /// <summary>
        /// Advances the typewriter reveal. Once the full text is revealed this becomes a no-op —
        /// the bubble persists until <see cref="Hide"/>.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (!_active || _wrappedText == null || _revealed >= _wrappedText.Length)
                return;

            float delay = 1f / GameConfig.SpeechBubbleCharsPerSecond;
            _accumulated += deltaTime;
            while (_accumulated >= delay && _revealed < _wrappedText.Length)
            {
                _accumulated -= delay;
                _visibleText.Append(_wrappedText[_revealed]);
                _revealed++;
            }
        }

        /// <summary>Immediately hides the bubble and clears its text.</summary>
        public void Hide()
        {
            _active = false;
            _visibleText.Clear();
            _wrappedText = null;
            SetVisible(false);
        }

        public override void Draw(Batcher batcher, float parentAlpha)
        {
            if (!_active || _bubbleDrawable == null)
                return;

            // Same layout math as SpeechBubbleComponent.Render at scale 1, in stage coordinates:
            //   tail bottom = anchor (tail tip), bubble bottom overlaps the tail top by 2 px
            float tailTopY      = _anchorY - TailSpriteH;
            float bubbleBottomY = tailTopY + GameConfig.SpeechBubbleTailOverlap;
            float bubbleTopY    = bubbleBottomY - _bubbleHeight;
            float bubbleX       = _anchorX - GameConfig.SpeechBubbleWidth / 2f;
            float tailX         = _anchorX - TailSpriteW / 2f;

            _bubbleDrawable.Draw(batcher, bubbleX, bubbleTopY,
                GameConfig.SpeechBubbleWidth, _bubbleHeight, BubbleColor);

            batcher.Draw(_tailSprite.Texture2D, new Vector2(tailX, tailTopY),
                _tailSprite.SourceRect, BubbleColor);

            if (_visibleText.Length > 0)
            {
                var textOrigin = new Vector2(
                    bubbleX + GameConfig.SpeechBubblePadding,
                    bubbleTopY + GameConfig.SpeechBubblePadding);
                _font.DrawInto(batcher, _visibleText, textOrigin, TextColor,
                    0f, Vector2.Zero, Vector2.One, Microsoft.Xna.Framework.Graphics.SpriteEffects.None, 0f);
            }
        }
    }
}
