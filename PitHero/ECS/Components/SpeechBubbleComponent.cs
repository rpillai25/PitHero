using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nez;
using Nez.BitmapFonts;
using Nez.Textures;
using PitHero.Services;
using System.Collections;
using System.Text;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Renders a nine-patch speech bubble with a tail above the entity's head.
    /// Drawn in screen space (<see cref="GameConfig.RenderLayerSpeechBubble"/>) so the
    /// bubble holds a constant screen-pixel size at any camera zoom (128 wide, height
    /// sized to the mode's visible-line count); the tail tip is re-anchored to the
    /// entity's head each frame via the world camera's WorldToScreenPoint. Text is revealed via a typewriter effect; the bubble
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

        // Drawables and fonts (null until OnAddedToEntity succeeds)
        private NinePatchSprite _bubbleSprite;
        private Sprite _tailSprite;
        private BitmapFont _font;
        private BitmapFont _font2x;
        private PauseService _pauseService;

        // Nine-patch destination layouts in design units, generated once. Render scales each patch
        // by _activeScale so the 4 px borders double along with the bubble in half-window mode
        // (NinePatchDrawable.Draw can't do this — it always draws borders at their source size).
        // Each mode's bubble height derives from its visible-line count (normal 3, half-window 2);
        // text that wraps to more lines scrolls (see ScrollVisibleTextIfNeeded).
        private readonly Rectangle[] _bubbleDesignRects = new Rectangle[9];
        private readonly Rectangle[] _bubbleDesignRectsHalf = new Rectangle[9];
        private int _normalDesignHeight;
        private int _halfDesignHeight;

        // State
        private bool _active;
        private string _wrappedText;
        private readonly StringBuilder _visibleText = new StringBuilder(256);
        private ICoroutine _revealRoutine;

        // Per-bubble presentation, chosen at Say() time: 2x bubble + pre-scaled Express2x font in
        // half-size window mode so text reads at the same physical size as the normal window.
        // A window-mode toggle mid-bubble keeps the bubble's Say-time scale; the next Say re-picks.
        private int _activeScale = 1;
        private BitmapFont _activeFont;
        private Rectangle[] _activeDesignRects;
        private int _activeDesignHeight;
        private int _visibleLineCapacity = int.MaxValue;

        /// <summary>
        /// Optional body renderer whose sprite height anchors the bubble. When set, the tail
        /// tip offset is derived per-frame from the sprite height (workers vary 28–90 px).
        /// Null = hero/patron default (uses <see cref="GameConfig.SpeechBubbleTailTipOffsetY"/>).
        /// </summary>
        public Nez.Sprites.SpriteRenderer AnchorRenderer;

        /// <inheritdoc/>
        /// Screen pixels. Bounds is not used for culling — see the IsVisibleFromCamera override.
        public override float Width => GameConfig.SpeechBubbleWidth * _activeScale;

        /// <inheritdoc/>
        /// Full visual height in screen pixels: bubble body + tail sprite height - tail overlap with bubble border.
        public override float Height =>
            (_activeDesignHeight + TailSpriteH - GameConfig.SpeechBubbleTailOverlap) * _activeScale;

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

                _bubbleSprite = new NinePatchSprite(bubbleSprite.Texture2D, bubbleSprite.SourceRect, 4, 4, 4, 4);

                _font = Core.Content.LoadBitmapFont(GameConfig.FontPathSpeechBubble);
                if (_font == null)
                {
                    Debug.Warn("[SpeechBubble] Failed to load speech bubble font");
                    return;
                }

                _font2x = Core.Content.LoadBitmapFont(GameConfig.FontPathSpeechBubble2x);
                if (_font2x == null)
                    Debug.Warn("[SpeechBubble] Failed to load 2x speech bubble font — half-window bubbles stay 1x");

                // Per-mode bubble heights derive from the visible-line counts (design units, so the
                // 1x font's LineHeight — Express2x is exactly 2x and rides on _activeScale).
                _normalDesignHeight = GameConfig.SpeechBubblePadding * 2
                    + GameConfig.SpeechBubbleVisibleLinesNormal * _font.LineHeight;
                _halfDesignHeight = GameConfig.SpeechBubblePadding * 2
                    + GameConfig.SpeechBubbleVisibleLinesHalfWindow * _font.LineHeight;
                _bubbleSprite.GenerateNinePatchRects(
                    new Rectangle(0, 0, GameConfig.SpeechBubbleWidth, _normalDesignHeight),
                    _bubbleDesignRects, 4, 4, 4, 4);
                _bubbleSprite.GenerateNinePatchRects(
                    new Rectangle(0, 0, GameConfig.SpeechBubbleWidth, _halfDesignHeight),
                    _bubbleDesignRectsHalf, 4, 4, 4, 4);

                _activeFont = _font;
                _activeDesignRects = _bubbleDesignRects;
                _activeDesignHeight = _normalDesignHeight;

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
            if (_bubbleSprite == null || _font == null || string.IsNullOrEmpty(localizedText))
                return;

            _revealRoutine?.Stop();
            _revealRoutine = null;

            // Half-size window: double the bubble and use the pre-scaled 2x font so the text
            // displays at the same physical size as the normal window. Wrap width doubles with
            // the font, so line breaks land in the same places. The half bubble is also shorter
            // (2 text lines max); text beyond a bubble's capacity scrolls up a line at a time.
            bool halfWindow = WindowManager.IsHalfHeightMode() && _font2x != null;
            _activeScale = halfWindow ? 2 : 1;
            _activeFont = halfWindow ? _font2x : _font;
            _activeDesignRects = halfWindow ? _bubbleDesignRectsHalf : _bubbleDesignRects;
            _activeDesignHeight = halfWindow ? _halfDesignHeight : _normalDesignHeight;
            _visibleLineCapacity = halfWindow
                ? GameConfig.SpeechBubbleVisibleLinesHalfWindow
                : GameConfig.SpeechBubbleVisibleLinesNormal;

            _visibleText.Clear();
            _wrappedText = _activeFont.WrapText(localizedText, TextWrapWidth * _activeScale);
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
            if (!_active || _bubbleSprite == null || _activeFont == null)
                return;

            // camera is the screen-space renderer's static camera — positioning instead derives
            // from the world camera so the bubble tracks its speaker while staying a constant
            // screen-pixel size at any zoom.
            var anchor = Entity.Scene.Camera.WorldToScreenPoint(
                new Vector2(Entity.Position.X, Entity.Position.Y + GetTailTipOffsetY()));
            var p = anchor;
            float s = _activeScale;

            // Position math (screen pixels, all extents ×_activeScale — 2x in half-size window):
            //   tail bottom Y = anchor (tail tip, 4 world px clearance above sprite top)
            //   tail top Y    = tail bottom - tail height  (tail sprite is 8 px tall)
            //   bubble bottom = tail top + tail overlap with bubble border (2 px)
            //   bubble top    = bubble bottom - bubble height  (per-mode: 3-line layout in
            //                   normal mode, 2-line in half-window)
            float tailBottomY   = p.Y;
            float tailTopY      = tailBottomY - TailSpriteH * s;
            float bubbleBottomY = tailTopY + GameConfig.SpeechBubbleTailOverlap * s;
            float bubbleTopY    = bubbleBottomY - _activeDesignHeight * s;
            float bubbleX       = p.X - GameConfig.SpeechBubbleWidth * s / 2f;
            float tailX         = p.X - TailSpriteW * s / 2f;

            // Nine-patch bubble, patch-by-patch so the 4 px borders scale with the bubble
            for (var i = 0; i < 9; i++)
            {
                var dest = _activeDesignRects[i];
                var src = _bubbleSprite.NinePatchRects[i];
                if (dest.Width == 0 || dest.Height == 0 || src.Width == 0 || src.Height == 0)
                    continue;

                batcher.Draw(_bubbleSprite.Texture2D,
                    new Vector2(bubbleX + dest.X * s, bubbleTopY + dest.Y * s),
                    src, BubbleColor, 0f, Vector2.Zero,
                    new Vector2(dest.Width * s / src.Width, dest.Height * s / src.Height),
                    SpriteEffects.None, 0f);
            }

            // Tail — top 2 rows overlap bubble's bottom border, merging the outlines
            batcher.Draw(_tailSprite.Texture2D, new Vector2(tailX, tailTopY),
                _tailSprite.SourceRect, BubbleColor, 0f, Vector2.Zero, s,
                SpriteEffects.None, 0f);

            // Typewriter text
            if (_visibleText.Length > 0)
            {
                var textOrigin = new Vector2(
                    bubbleX + GameConfig.SpeechBubblePadding * s,
                    bubbleTopY + GameConfig.SpeechBubblePadding * s);
                _activeFont.DrawInto(batcher, _visibleText, textOrigin, TextColor,
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
                    var c = _wrappedText[revealed];
                    _visibleText.Append(c);
                    revealed++;

                    if (c == '\n')
                        ScrollVisibleTextIfNeeded();
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

        /// <summary>
        /// Scrolls the visible text block up one line at a time when the typewriter reveal
        /// crosses onto a line beyond the bubble's capacity: once a line finishes revealing
        /// (its newline is appended), the oldest visible line is dropped so the next line
        /// types into the freed bottom row. Only called when a '\n' was just appended.
        /// </summary>
        private void ScrollVisibleTextIfNeeded()
        {
            while (true)
            {
                int newlineCount = 0;
                int firstNewlineIndex = -1;
                for (int i = 0; i < _visibleText.Length; i++)
                {
                    if (_visibleText[i] == '\n')
                    {
                        if (firstNewlineIndex < 0)
                            firstNewlineIndex = i;
                        newlineCount++;
                    }
                }

                // N newlines = the (N+1)th line is about to type; scroll while that exceeds capacity
                if (newlineCount < _visibleLineCapacity)
                    return;

                _visibleText.Remove(0, firstNewlineIndex + 1);
            }
        }
    }
}
