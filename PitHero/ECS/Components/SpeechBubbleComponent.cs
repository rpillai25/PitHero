using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nez;
using Nez.BitmapFonts;
using Nez.Textures;
using Nez.UI;
using PitHero.Services;
using System.Collections;
using System.Text;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Renders a nine-patch speech bubble with a tail above the entity's head.
    /// Drawn in screen space (<see cref="GameConfig.RenderLayerSpeechBubble"/>) so the
    /// bubble holds its 128×48 screen-pixel size at any camera zoom; the tail tip is
    /// re-anchored to the entity's head each frame via the world camera's
    /// WorldToScreenPoint. Text is revealed via a typewriter effect; the bubble
    /// auto-hides after the reveal completes plus a brief linger period. Pause-aware —
    /// the reveal and linger freeze while <see cref="PauseService.IsPaused"/> is true.
    /// </summary>
    /// <remarks>
    /// Attach to the hero entity (or any paperdoll entity whose sprite occupies
    /// 32 px above origin). Call <see cref="Say"/> to display dialogue.
    /// Entity-agnostic: the same component works on mercenaries later.
    /// </remarks>
    public sealed class SpeechBubbleComponent : RenderableComponent
    {
        // Atlas and sprite keys
        private const string AtlasPath = "Content/Atlases/UI.atlas";
        private const string BubbleSpriteName = "NinePatchSpeechBubble";
        private const string TailSpriteName = "SpeechBubbleTail";

        // Tail sprite dimensions (pixels)
        private const int TailSpriteW = 9;
        private const int TailSpriteH = 8;

        // Text wrap width = BubbleWidth - Padding * 2 = 120
        private const float TextWrapWidth = GameConfig.SpeechBubbleWidth - GameConfig.SpeechBubblePadding * 2;

        // Colors
        private static readonly Color BubbleColor = Color.White;
        private static readonly Color TextColor = new Color(50, 30, 20);

        // Drawables and font (null until OnAddedToEntity succeeds)
        private NinePatchDrawable _bubbleDrawable;
        private Sprite _tailSprite;
        private BitmapFont _font;
        private PauseService _pauseService;

        // State
        private bool _active;
        private string _wrappedText;
        private readonly StringBuilder _visibleText = new StringBuilder(256);
        private ICoroutine _revealRoutine;

        // Pre-allocated tail destination rectangle (mutated in Render, no per-frame alloc)
        private Rectangle _tailDestRect;

        /// <summary>
        /// Optional body renderer whose sprite height anchors the bubble. When set, the tail
        /// tip offset is derived per-frame from the sprite height (workers vary 28–90 px).
        /// Null = hero/patron default (uses <see cref="GameConfig.SpeechBubbleTailTipOffsetY"/>).
        /// </summary>
        public Nez.Sprites.SpriteRenderer AnchorRenderer;

        /// <inheritdoc/>
        /// Screen pixels. Bounds is not used for culling — see the IsVisibleFromCamera override.
        public override float Width => GameConfig.SpeechBubbleWidth;

        /// <inheritdoc/>
        /// Full visual height in screen pixels: bubble body + tail sprite height - tail overlap with bubble border.
        public override float Height =>
            GameConfig.SpeechBubbleHeight + TailSpriteH - GameConfig.SpeechBubbleTailOverlap;

        public override void OnAddedToEntity()
        {
            base.OnAddedToEntity();

            try
            {
                var uiAtlas = Core.Content.LoadSpriteAtlas(AtlasPath);
                if (uiAtlas == null)
                {
                    Debug.Warn("[SpeechBubble] Failed to load UI.atlas — atlas is null");
                    return;
                }

                var bubbleSprite = uiAtlas.GetSprite(BubbleSpriteName);
                if (bubbleSprite == null)
                {
                    Debug.Warn($"[SpeechBubble] Sprite '{BubbleSpriteName}' not found in UI.atlas");
                    return;
                }

                _tailSprite = uiAtlas.GetSprite(TailSpriteName);
                if (_tailSprite == null)
                {
                    Debug.Warn($"[SpeechBubble] Sprite '{TailSpriteName}' not found in UI.atlas");
                    return;
                }

                _bubbleDrawable = new NinePatchDrawable(
                    new NinePatchSprite(bubbleSprite, 4, 4, 4, 4));

                _font = Core.Content.LoadBitmapFont(GameConfig.FontPathSpeechBubble);
                if (_font == null)
                {
                    Debug.Warn("[SpeechBubble] Failed to load speech bubble font");
                    return;
                }

                _pauseService = Core.Services.GetService<PauseService>();
                SetRenderLayer(GameConfig.RenderLayerSpeechBubble);
            }
            catch (System.Exception ex)
            {
                Debug.Warn($"[SpeechBubble] Initialisation failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Displays <paramref name="localizedText"/> in the speech bubble with a
        /// typewriter reveal. Interrupts any currently active bubble.
        /// </summary>
        /// <param name="localizedText">Already-localized text to display.</param>
        public void Say(string localizedText)
        {
            if (_bubbleDrawable == null || _font == null || string.IsNullOrEmpty(localizedText))
                return;

            _revealRoutine?.Stop();
            _revealRoutine = null;

            _visibleText.Clear();
            _wrappedText = _font.WrapText(localizedText, TextWrapWidth);
            _active = true;

            _revealRoutine = Core.StartCoroutine(RevealRoutine());
        }

        /// <summary>
        /// Immediately hides the bubble and cancels any active reveal or linger coroutine.
        /// </summary>
        public void Hide()
        {
            _revealRoutine?.Stop();
            _revealRoutine = null;
            _active = false;
        }

        public override void OnRemovedFromEntity()
        {
            Hide();
            base.OnRemovedFromEntity();
        }

        /// <summary>
        /// Returns the Y offset from the entity's origin to the tip of the speech bubble tail.
        /// When an <see cref="AnchorRenderer"/> is set, the value is computed from the sprite's
        /// height so the bubble clears the top of the monster sprite by 4 px. Falls back to
        /// <see cref="GameConfig.SpeechBubbleTailTipOffsetY"/> for hero/patron paperdolls.
        /// No allocation: the null-conditional on Sprite is a reference comparison.
        /// </summary>
        private float GetTailTipOffsetY()
        {
            return AnchorRenderer?.Sprite != null
                ? (GameConfig.TileSize / 2 - AnchorRenderer.Sprite.SourceRect.Height - 4)
                : GameConfig.SpeechBubbleTailTipOffsetY;
        }

        /// <summary>
        /// Called by the ScreenSpaceRenderer with its own static camera — ignored. The bubble
        /// is anchored to a world entity, so visibility is decided by the world camera: the
        /// bubble shows only while its speaker is within the scene camera's view (expanded by
        /// one tile so it doesn't pop off while the sprite is still partially on-screen).
        /// </summary>
        public override bool IsVisibleFromCamera(Camera camera)
        {
            if (!_active)
                return false;

            var sceneCamera = Entity.Scene?.Camera;
            if (sceneCamera == null)
                return false;

            var p = Entity.Position;
            var cam = sceneCamera.Bounds;
            const float margin = GameConfig.TileSize;
            return p.X >= cam.X - margin && p.X <= cam.Right + margin
                && p.Y >= cam.Y - margin && p.Y <= cam.Bottom + margin;
        }

        public override void Render(Batcher batcher, Camera camera)
        {
            if (!_active || _bubbleDrawable == null || _font == null)
                return;

            // camera is the screen-space renderer's static camera — positioning instead derives
            // from the world camera so the bubble tracks its speaker while staying a constant
            // screen-pixel size at any zoom.
            var anchor = Entity.Scene.Camera.WorldToScreenPoint(
                new Vector2(Entity.Position.X, Entity.Position.Y + GetTailTipOffsetY()));
            var p = anchor;

            // Position math (screen pixels):
            //   tail bottom Y = anchor (tail tip, 4 world px clearance above sprite top)
            //   tail top Y    = tail bottom - tail height  (tail sprite is 8 px tall)
            //   bubble bottom = tail top + tail overlap with bubble border (2 px)
            //   bubble top    = bubble bottom - bubble height  (48 px)
            float tailBottomY   = p.Y;
            float tailTopY      = tailBottomY - TailSpriteH;
            float bubbleBottomY = tailTopY + GameConfig.SpeechBubbleTailOverlap;
            float bubbleTopY    = bubbleBottomY - GameConfig.SpeechBubbleHeight;
            float bubbleX       = p.X - GameConfig.SpeechBubbleWidth / 2f;
            float tailX         = p.X - TailSpriteW / 2f;

            // Nine-patch bubble
            _bubbleDrawable.Draw(batcher,
                bubbleX, bubbleTopY,
                GameConfig.SpeechBubbleWidth, GameConfig.SpeechBubbleHeight,
                BubbleColor);

            // Tail — top 2 rows overlap bubble's bottom border, merging the outlines
            _tailDestRect.X      = (int)tailX;
            _tailDestRect.Y      = (int)tailTopY;
            _tailDestRect.Width  = TailSpriteW;
            _tailDestRect.Height = TailSpriteH;
            batcher.Draw(_tailSprite, _tailDestRect, _tailSprite.SourceRect, BubbleColor);

            // Typewriter text
            if (_visibleText.Length > 0)
            {
                var textOrigin = new Vector2(
                    bubbleX + GameConfig.SpeechBubblePadding,
                    bubbleTopY + GameConfig.SpeechBubblePadding);
                _font.DrawInto(batcher, _visibleText, textOrigin, TextColor,
                    0f, Vector2.Zero, Vector2.One, SpriteEffects.None, 0f);
            }
        }

        private IEnumerator RevealRoutine()
        {
            float delay = 1f / GameConfig.SpeechBubbleCharsPerSecond;
            int length  = _wrappedText?.Length ?? 0;

            // Accumulate scaled delta time and reveal as many characters as it covers each
            // frame. Per-character WaitForSeconds quantized to whole frames (remainder
            // discarded), which capped the reveal near one char per frame and made fast
            // forward barely speed the text up.
            float accumulated = 0f;
            int revealed = 0;
            while (revealed < length)
            {
                while (_pauseService?.IsPaused == true)
                    yield return null;

                yield return null;
                accumulated += Time.DeltaTime;

                while (accumulated >= delay && revealed < length)
                {
                    accumulated -= delay;
                    _visibleText.Append(_wrappedText[revealed]);
                    revealed++;
                }
            }

            // Linger after full text is revealed; scaled so fast forward shortens it too
            float lingered = 0f;
            while (lingered < GameConfig.SpeechBubbleLingerSeconds)
            {
                while (_pauseService?.IsPaused == true)
                    yield return null;

                yield return null;
                lingered += Time.DeltaTime;
            }

            _active = false;
            _revealRoutine = null;
        }
    }
}
