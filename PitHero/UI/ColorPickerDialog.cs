using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nez;
using Nez.UI;
using PitHero.ECS.Components;
using PitHero.Services;

namespace PitHero.UI
{
    /// <summary>One color a <see cref="ColorPickerDialog"/> edits, shown as a swatch with a label.</summary>
    public readonly struct ColorPickerTone
    {
        public readonly string Label;
        public readonly Color Color;

        public ColorPickerTone(string label, Color color)
        {
            Label = label;
            Color = color;
        }
    }

    /// <summary>
    /// Reusable color picker (issue #467): a hue strip and a saturation/value square choose any color for
    /// the selected tone; the swatches on the left pick which tone is being edited. Edits are reported live
    /// through onChanged so the caller can preview them, then committed by OK or rolled back by Cancel.
    /// An optional autoTone function backs the Auto Shade button, which remakes every other tone from the
    /// base tone (the Midtone). UI-only: it never touches simulation state.
    /// </summary>
    public class ColorPickerDialog : Window, IUIPrompt
    {
        private const float SquareSize = 96f;
        private const float HueStripWidth = 14f;
        private const float ButtonHeight = 16f;

        private readonly Color[] _colors;
        private readonly Color[] _original;
        private readonly int _baseIndex;
        private readonly Func<Color, int, Color> _autoTone;
        private readonly Action<Color[]> _onChanged;
        private readonly Action<Color[]> _onConfirm;
        private readonly Action _onCancel;

        private readonly ToneSwatch[] _swatches;
        private readonly SaturationValueSquare _square;
        private readonly HueStrip _hueStrip;
        private readonly Label _hexLabel;
        private int _selected;
        private float _hue;

        bool IUIPrompt.IsPromptVisible => GetParent() != null && IsVisible();

        void IUIPrompt.CancelPrompt() => Cancel();

        /// <param name="title">Window title.</param>
        /// <param name="skin">Skin for labels, buttons and the window.</param>
        /// <param name="tones">The colors edited, top to bottom (e.g. Highlight, Midtone, Shadow).</param>
        /// <param name="baseIndex">The tone that is selected first and that Auto Shade derives the others from.</param>
        /// <param name="autoTone">Makes tone i from the base color; null hides the Auto Shade button.</param>
        /// <param name="onChanged">Called after every edit with the current colors (same order as tones).</param>
        /// <param name="onConfirm">Called on OK with the final colors.</param>
        /// <param name="onCancel">Called on Cancel / Escape; onChanged has already been called with the original colors.</param>
        public ColorPickerDialog(string title, Skin skin, ColorPickerTone[] tones, int baseIndex,
            Func<Color, int, Color> autoTone, Action<Color[]> onChanged, Action<Color[]> onConfirm, Action onCancel)
            : base(title, skin)
        {
            _colors = new Color[tones.Length];
            _original = new Color[tones.Length];
            for (int i = 0; i < tones.Length; i++)
            {
                _colors[i] = tones[i].Color;
                _original[i] = tones[i].Color;
            }
            _baseIndex = baseIndex;
            _autoTone = autoTone;
            _onChanged = onChanged;
            _onConfirm = onConfirm;
            _onCancel = onCancel;

            var textService = Core.Services.GetService<TextService>();
            SetMovable(false);

            var table = new Table();
            table.Pad(12f);

            // ── Tone swatches | saturation/value square | hue strip ─────────────────
            var toneTable = new Table();
            toneTable.Defaults().SetPadBottom(4f);
            _swatches = new ToneSwatch[tones.Length];
            for (int i = 0; i < tones.Length; i++)
            {
                int index = i;
                _swatches[i] = new ToneSwatch(_colors[i], () => SelectTone(index));
                toneTable.Add(_swatches[i]).Size(22f, 16f).SetPadRight(6f);
                toneTable.Add(new Label(tones[i].Label, skin)).Left();
                toneTable.Row();
            }
            table.Add(toneTable).Top().Left().SetPadRight(10f);

            _square = new SaturationValueSquare(OnSquareChanged);
            table.Add(_square).Size(SquareSize, SquareSize).SetPadRight(6f);

            _hueStrip = new HueStrip(OnHueChanged);
            table.Add(_hueStrip).Size(HueStripWidth, SquareSize);
            table.Row();

            // ── Hex readout and Auto Shade ──────────────────────────────────────────
            var infoRow = new Table();
            _hexLabel = new Label(string.Empty, skin);
            infoRow.Add(_hexLabel).Left().Expand();
            if (_autoTone != null && tones.Length > 1)
            {
                var autoButton = new TextButton(textService.DisplayText(TextType.UI, UITextKey.ColorPickerAutoShade), skin, "ph-default");
                autoButton.OnClicked += (b) => AutoShade();
                infoRow.Add(autoButton).SetMinHeight(ButtonHeight);
            }
            table.Add(infoRow).SetColspan(3).Fill().SetPadTop(8f);
            table.Row();

            // ── OK / Cancel ─────────────────────────────────────────────────────────
            var buttonRow = new Table();
            var okButton = new TextButton(textService.DisplayText(TextType.UI, UITextKey.ButtonOK), skin, "ph-default");
            okButton.OnClicked += (b) => Confirm();
            buttonRow.Add(okButton).Width(80f).SetMinHeight(ButtonHeight).SetPadRight(10f);
            var cancelButton = new TextButton(textService.DisplayText(TextType.UI, UITextKey.ButtonCancel), skin, "ph-default");
            cancelButton.ClickSoundCategory = ButtonClickCategory.Cancel;
            cancelButton.OnClicked += (b) => Cancel();
            buttonRow.Add(cancelButton).Width(80f).SetMinHeight(ButtonHeight);
            table.Add(buttonRow).SetColspan(3).SetPadTop(8f);

            Add(table).Expand().Fill();
            Pack();

            SelectTone(baseIndex >= 0 && baseIndex < tones.Length ? baseIndex : 0);
        }

        /// <summary>Shows the dialog centered on the stage, above everything else.</summary>
        public void Show(Stage stage)
        {
            SetPosition((stage.GetWidth() - GetWidth()) / 2f, UILayout.CenterY(GetHeight(), stage.GetHeight(), 0f));
            stage.AddElement(this);
            SetVisible(true);
            ToFront();
            UIPromptRegistry.Register(this);
        }

        private void SelectTone(int index)
        {
            _selected = index;
            for (int i = 0; i < _swatches.Length; i++)
                _swatches[i].Selected = i == index;
            HeroToneProfile.RgbToHsv(_colors[index], out float h, out float s, out float v);
            if (s > 0f)
                _hue = h; // a gray has no hue: keep the strip where it was
            _hueStrip.Hue = _hue;
            _square.SetColor(_hue, s, v);
            UpdateHex();
        }

        private void OnHueChanged(float hue)
        {
            _hue = hue;
            _square.Hue = hue;
            ApplySelected();
        }

        private void OnSquareChanged() => ApplySelected();

        // Writes the square's color into the selected tone and reports it
        private void ApplySelected()
        {
            _colors[_selected] = HeroToneProfile.HsvToRgb(_hue, _square.Saturation, _square.Value, 255);
            _swatches[_selected].Color = _colors[_selected];
            UpdateHex();
            _onChanged?.Invoke(_colors);
        }

        private void AutoShade()
        {
            for (int i = 0; i < _colors.Length; i++)
            {
                if (i == _baseIndex)
                    continue;
                _colors[i] = _autoTone(_colors[_baseIndex], i);
                _swatches[i].Color = _colors[i];
            }
            SelectTone(_selected);
            _onChanged?.Invoke(_colors);
        }

        private void UpdateHex()
        {
            var c = _colors[_selected];
            _hexLabel.SetText("#" + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2"));
        }

        private void Confirm()
        {
            Close();
            _onConfirm?.Invoke(_colors);
        }

        private void Cancel()
        {
            Close();
            for (int i = 0; i < _colors.Length; i++)
                _colors[i] = _original[i];
            _onChanged?.Invoke(_colors);
            _onCancel?.Invoke();
        }

        private void Close()
        {
            _square.DisposeTexture();
            _hueStrip.DisposeTexture();
            Remove();
        }

        /// <summary>A clickable color swatch; the selected one wears a thick white frame.</summary>
        private sealed class ToneSwatch : Element, IInputListener
        {
            private static readonly Color FrameColor = new Color(71, 36, 7);
            private readonly Action _onClick;

            public Color Color;
            public bool Selected;

            public ToneSwatch(Color color, Action onClick)
            {
                Color = color;
                _onClick = onClick;
                SetTouchable(Touchable.Enabled);
            }

            public override void Draw(Batcher batcher, float parentAlpha)
            {
                float x = GetX(), y = GetY(), w = GetWidth(), h = GetHeight();
                batcher.DrawRect(x, y, w, h, Selected ? Color.White : FrameColor);
                float inset = Selected ? 2f : 1f;
                batcher.DrawRect(x + inset, y + inset, w - inset * 2f, h - inset * 2f, Color);
            }

            void IInputListener.OnMouseEnter() { }
            void IInputListener.OnMouseExit() { }
            void IInputListener.OnMouseMoved(Vector2 mousePos) { }
            bool IInputListener.OnLeftMousePressed(Vector2 mousePos)
            {
                _onClick?.Invoke();
                return false;
            }
            bool IInputListener.OnRightMousePressed(Vector2 mousePos) => false;
            void IInputListener.OnLeftMouseUp(Vector2 mousePos) { }
            void IInputListener.OnRightMouseUp(Vector2 mousePos) { }
            bool IInputListener.OnMouseScrolled(int mouseWheelDelta) => false;
        }

        /// <summary>
        /// Saturation left to right, value bottom to top, for the current hue. The gradient is a texture the
        /// size of the element, remade only when the hue changes.
        /// </summary>
        private sealed class SaturationValueSquare : Element, IInputListener
        {
            private readonly Action _onChanged;
            private Texture2D _texture;
            private Color[] _pixels;
            private float _textureHue = -1f;
            private bool _dragging;

            public float Hue;
            public float Saturation { get; private set; }
            public float Value { get; private set; }

            public SaturationValueSquare(Action onChanged)
            {
                _onChanged = onChanged;
                SetTouchable(Touchable.Enabled);
            }

            public void SetColor(float hue, float saturation, float value)
            {
                Hue = hue;
                Saturation = saturation;
                Value = value;
            }

            public void DisposeTexture()
            {
                _texture?.Dispose();
                _texture = null;
            }

            public override void Draw(Batcher batcher, float parentAlpha)
            {
                int w = (int)GetWidth(), h = (int)GetHeight();
                if (w <= 0 || h <= 0 || Core.GraphicsDevice == null)
                    return;
                if (_texture == null || _texture.Width != w || _texture.Height != h)
                {
                    _texture?.Dispose();
                    _texture = new Texture2D(Core.GraphicsDevice, w, h);
                    _pixels = new Color[w * h];
                    _textureHue = -1f;
                }
                if (_textureHue != Hue)
                {
                    for (int py = 0; py < h; py++)
                    {
                        float v = 1f - py / (float)(h - 1);
                        for (int px = 0; px < w; px++)
                            _pixels[py * w + px] = HeroToneProfile.HsvToRgb(Hue, px / (float)(w - 1), v, 255);
                    }
                    _texture.SetData(_pixels);
                    _textureHue = Hue;
                }

                float x = GetX(), y = GetY();
                batcher.Draw(_texture, new Rectangle((int)x, (int)y, w, h), Color.White);

                // Ring on the picked color: dark on light colors, light on dark ones
                float cx = x + Saturation * (w - 1);
                float cy = y + (1f - Value) * (h - 1);
                var ring = Value > 0.6f && Saturation < 0.5f ? Color.Black : Color.White;
                batcher.DrawHollowRect(cx - 3f, cy - 3f, 7f, 7f, ring, 0f, Vector2.Zero, 1f);
            }

            private void Pick(Vector2 local)
            {
                float w = Math.Max(1f, GetWidth() - 1f), h = Math.Max(1f, GetHeight() - 1f);
                Saturation = Math.Clamp(local.X / w, 0f, 1f);
                Value = Math.Clamp(1f - local.Y / h, 0f, 1f);
                _onChanged?.Invoke();
            }

            void IInputListener.OnMouseEnter() { }
            void IInputListener.OnMouseExit() { }
            void IInputListener.OnMouseMoved(Vector2 mousePos)
            {
                if (_dragging)
                    Pick(mousePos);
            }
            bool IInputListener.OnLeftMousePressed(Vector2 mousePos)
            {
                _dragging = true;
                Pick(mousePos);
                return true;
            }
            bool IInputListener.OnRightMousePressed(Vector2 mousePos) => false;
            void IInputListener.OnLeftMouseUp(Vector2 mousePos) => _dragging = false;
            void IInputListener.OnRightMouseUp(Vector2 mousePos) { }
            bool IInputListener.OnMouseScrolled(int mouseWheelDelta) => false;
        }

        /// <summary>A vertical hue strip, red at the top through the spectrum back to red.</summary>
        private sealed class HueStrip : Element, IInputListener
        {
            private readonly Action<float> _onChanged;
            private Texture2D _texture;
            private bool _dragging;

            public float Hue;

            public HueStrip(Action<float> onChanged)
            {
                _onChanged = onChanged;
                SetTouchable(Touchable.Enabled);
            }

            public void DisposeTexture()
            {
                _texture?.Dispose();
                _texture = null;
            }

            public override void Draw(Batcher batcher, float parentAlpha)
            {
                int h = (int)GetHeight();
                if (h <= 1 || Core.GraphicsDevice == null)
                    return;
                if (_texture == null || _texture.Height != h)
                {
                    _texture?.Dispose();
                    _texture = new Texture2D(Core.GraphicsDevice, 1, h);
                    var pixels = new Color[h];
                    for (int py = 0; py < h; py++)
                        pixels[py] = HeroToneProfile.HsvToRgb(360f * py / h, 1f, 1f, 255);
                    _texture.SetData(pixels);
                }

                float x = GetX(), y = GetY(), w = GetWidth();
                batcher.Draw(_texture, new Rectangle((int)x, (int)y, (int)w, h), Color.White);

                float my = y + Hue / 360f * h;
                batcher.DrawRect(x - 2f, my - 1f, w + 4f, 3f, Color.Black);
                batcher.DrawRect(x - 1f, my, w + 2f, 1f, Color.White);
            }

            private void Pick(Vector2 local)
            {
                float h = Math.Max(1f, GetHeight());
                Hue = Math.Clamp(local.Y / h, 0f, 0.9999f) * 360f;
                _onChanged?.Invoke(Hue);
            }

            void IInputListener.OnMouseEnter() { }
            void IInputListener.OnMouseExit() { }
            void IInputListener.OnMouseMoved(Vector2 mousePos)
            {
                if (_dragging)
                    Pick(mousePos);
            }
            bool IInputListener.OnLeftMousePressed(Vector2 mousePos)
            {
                _dragging = true;
                Pick(mousePos);
                return true;
            }
            bool IInputListener.OnRightMousePressed(Vector2 mousePos) => false;
            void IInputListener.OnLeftMouseUp(Vector2 mousePos) => _dragging = false;
            void IInputListener.OnRightMouseUp(Vector2 mousePos) { }
            bool IInputListener.OnMouseScrolled(int mouseWheelDelta) => false;
        }
    }
}
