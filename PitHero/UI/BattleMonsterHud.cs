using Microsoft.Xna.Framework;
using Nez;
using Nez.Textures;
using Nez.UI;

namespace PitHero.UI
{
    /// <summary>
    /// The battle-monster strip in the top HUD: one slot per monster of the battle in progress, laid
    /// out left to right from a given point (just right of the gold display). A slot is the monster's
    /// portrait fitted into 32x32 over an HP bar the same width and height as the hero/mercenary HP
    /// bar. The scene feeds it every presentation frame, from the live battle or from a recorded HUD
    /// record, and only with the monsters currently on camera.
    /// </summary>
    public sealed class BattleMonsterHud
    {
        /// <summary>Slots available (matches what a HUD record can carry).</summary>
        public const int MaxSlots = Services.Replay.Frames.HudRecord.MaxMonsters;

        private const float PortraitSize = 32f;
        private const float BarWidth = 51f;        // GraphicalHUD's HP/MP bar width
        private const float PanelPad = 3f;         // panel edge to content
        private const float PortraitBarGap = 2f;
        private const float SlotGap = 4f;
        private static readonly Color BarBackColor = new Color(40, 0, 0, 200);

        private struct Slot
        {
            public Image Panel, Portrait, BarBack, BarFill;
            public Sprite PortraitSprite;
        }

        private readonly Slot[] _slots = new Slot[MaxSlots];
        private readonly float _barHeight;
        private int _count;

        /// <summary>Width of one slot's panel.</summary>
        public float SlotWidth => BarWidth + PanelPad * 2f;

        public BattleMonsterHud(Stage stage, Sprite panelSprite, Color panelTint, Sprite hpUnitSprite)
        {
            _barHeight = hpUnitSprite != null ? hpUnitSprite.SourceRect.Height : 4f;
            for (int i = 0; i < MaxSlots; i++)
            {
                ref var slot = ref _slots[i];
                // Each panel gets its own drawable: NinePatchDrawable caches its rects by size
                if (panelSprite != null)
                {
                    slot.Panel = stage.AddElement(new Image(
                        new NinePatchDrawable(new NinePatchSprite(panelSprite, 3, 3, 3, 3)) { TintColor = panelTint },
                        Scaling.Stretch));
                    Prepare(slot.Panel);
                }
                slot.BarBack = stage.AddElement(new Image(new PrimitiveDrawable(BarBackColor), Scaling.Stretch));
                Prepare(slot.BarBack);
                slot.BarFill = stage.AddElement(hpUnitSprite != null
                    ? new Image(new SpriteDrawable(hpUnitSprite), Scaling.Stretch)
                    : new Image(new PrimitiveDrawable(Color.Red), Scaling.Stretch));
                Prepare(slot.BarFill);
                slot.Portrait = stage.AddElement(new Image((Nez.UI.IDrawable)null, Scaling.Fit));
                Prepare(slot.Portrait);
            }
        }

        private static void Prepare(Image image)
        {
            image.SetTouchable(Touchable.Disabled);
            image.SetVisible(false);
        }

        /// <summary>Starts a new frame's slot list.</summary>
        public void Begin() => _count = 0;

        /// <summary>Adds a monster to this frame's slot list (ignored past <see cref="MaxSlots"/>).</summary>
        public void Add(Sprite portrait, Color portraitTint, int hp, int maxHp)
        {
            if (_count >= MaxSlots)
                return;
            ref var slot = ref _slots[_count++];

            if (!ReferenceEquals(slot.PortraitSprite, portrait))
            {
                slot.PortraitSprite = portrait;
                slot.Portrait.SetDrawable(portrait != null ? new SpriteDrawable(portrait) : (Nez.UI.IDrawable)null);
            }
            slot.Portrait.SetColor(portraitTint);

            // Same fill math as the hero/mercenary bar: whole pixels of the bar's width
            float ratio = maxHp > 0 ? Mathf.Clamp01((float)hp / maxHp) : 0f;
            slot.BarFill.SetSize((int)(ratio * BarWidth), _barHeight);
        }

        /// <summary>Lays this frame's slots out left to right from (x, y) and hides the unused ones.</summary>
        public void End(float x, float y)
        {
            float panelHeight = PanelPad + PortraitSize + PortraitBarGap + _barHeight + PanelPad;
            for (int i = 0; i < MaxSlots; i++)
            {
                ref var slot = ref _slots[i];
                bool shown = i < _count;
                slot.Panel?.SetVisible(shown);
                slot.Portrait.SetVisible(shown && slot.PortraitSprite != null);
                slot.BarBack.SetVisible(shown);
                slot.BarFill.SetVisible(shown && slot.BarFill.GetWidth() > 0f);
                if (!shown)
                    continue;

                float slotX = Mathf.Round(x + i * (SlotWidth + SlotGap));
                if (slot.Panel != null)
                {
                    slot.Panel.SetPosition(slotX, y);
                    slot.Panel.SetSize(SlotWidth, panelHeight);
                }
                slot.Portrait.SetSize(PortraitSize, PortraitSize);
                slot.Portrait.SetPosition(Mathf.Round(slotX + (SlotWidth - PortraitSize) * 0.5f), y + PanelPad);
                float barY = y + PanelPad + PortraitSize + PortraitBarGap;
                slot.BarBack.SetSize(BarWidth, _barHeight);
                slot.BarBack.SetPosition(slotX + PanelPad, barY);
                slot.BarFill.SetPosition(slotX + PanelPad, barY);
            }
        }

        /// <summary>Hides every slot (no battle, intro, nothing on camera).</summary>
        public void Clear()
        {
            Begin();
            End(0f, 0f);
        }
    }
}
