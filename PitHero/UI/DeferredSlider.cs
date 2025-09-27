using Microsoft.Xna.Framework;
using Nez;
using Nez.UI;
using System;

namespace PitHero.UI
{
    /// <summary>
    /// A slider that defers value application until mouse release, enabling smooth transitions
    /// </summary>
    public class DeferredSlider : ProgressBar, IInputListener
    {
        /// <summary>
        /// the maximum distance outside the slider the mouse can move when pressing it to cause it to be unfocused
        /// </summary>
        public float SliderBoundaryThreshold = 50f;

        SliderStyle style;
        bool _mouseOver, _mouseDown;
        bool _isDragging = false;
        float _committedValue;

        /// <summary>
        /// Event fired when the value is committed (mouse released)
        /// </summary>
        public event Action<float> OnValueCommitted;

        /// <summary>
        /// Creates a new deferred slider
        /// </summary>
        public DeferredSlider(float min, float max, float stepSize, bool vertical, SliderStyle style) : base(min, max, stepSize, vertical, style)
        {
            ShiftIgnoresSnap = true;
            this.style = style;
            _committedValue = Value;
        }

        public DeferredSlider(float min, float max, float stepSize, bool vertical, Skin skin, string styleName = null) : this(
            min, max, stepSize, vertical, skin.Get<SliderStyle>(styleName))
        {
        }

        public DeferredSlider(Skin skin, string styleName = null) : this(0, 1, 0.1f, false, skin.Get<SliderStyle>(styleName))
        {
        }

        /// <summary>
        /// Gets the committed value (the value that was last applied)
        /// </summary>
        public float GetCommittedValue()
        {
            return _committedValue;
        }

        /// <summary>
        /// Sets the value and immediately commits it (use for programmatic changes)
        /// </summary>
        public void SetValueAndCommit(float value)
        {
            SetValue(value);
            _committedValue = value;
            OnValueCommitted?.Invoke(_committedValue);
        }

        #region IInputListener

        void IInputListener.OnMouseEnter()
        {
            _mouseOver = true;
        }

        void IInputListener.OnMouseExit()
        {
            _mouseOver = false;
            // Don't commit here - only commit on actual mouse up
        }

        bool IInputListener.OnLeftMousePressed(Vector2 mousePos)
        {
            CalculatePositionAndValue(mousePos);
            _mouseDown = true;
            _isDragging = true;
            return true;
        }

        bool IInputListener.OnRightMousePressed(Vector2 mousePos)
        {
            return false;
        }

        void IInputListener.OnMouseMoved(Vector2 mousePos)
        {
            // As long as we're dragging (mouse button down), continue tracking mouse movement
            // regardless of whether mouse is within slider bounds
            if (_isDragging)
            {
                CalculatePositionAndValue(mousePos);
            }
        }

        void IInputListener.OnLeftMouseUp(Vector2 mousePos)
        {
            _mouseDown = false;
            
            // This is the ONLY place where we commit the value for mouse interaction
            if (_isDragging)
            {
                _isDragging = false;
                _committedValue = Value;
                OnValueCommitted?.Invoke(_committedValue);
            }
        }

        void IInputListener.OnRightMouseUp(Vector2 mousePos)
        {
            _mouseDown = false;
        }

        bool IInputListener.OnMouseScrolled(int mouseWheelDelta)
        {
            return false;
        }

        #endregion

        public DeferredSlider SetStyle(SliderStyle style)
        {
            if (!(style is SliderStyle))
                throw new ArgumentException("style must be a SliderStyle");

            base.SetStyle(style);
            this.style = style;
            return this;
        }

        /// <summary>
        /// Returns the slider's style. Modifying the returned style may not have an effect until SetStyle(SliderStyle) is called
        /// </summary>
        public new SliderStyle GetStyle()
        {
            return style;
        }

        public bool IsDragging()
        {
            return _mouseDown && _mouseOver;
        }

        protected override Nez.UI.IDrawable GetKnobDrawable()
        {
            if (Disabled && style.DisabledKnob != null)
                return style.DisabledKnob;

            if (IsDragging() && style.KnobDown != null)
                return style.KnobDown;

            if (_mouseOver && style.KnobOver != null)
                return style.KnobOver;

            return style.Knob;
        }

        void CalculatePositionAndValue(Vector2 mousePos)
        {
            var knob = GetKnobDrawable();

            float value;
            if (_vertical)
            {
                var height = this.height - style.Background.TopHeight - style.Background.BottomHeight;
                var knobHeight = knob == null ? 0 : knob.MinHeight;
                position = mousePos.Y - style.Background.BottomHeight - knobHeight * 0.5f;
                value = Min + (Max - Min) * (position / (height - knobHeight));
                position = Math.Max(0, position);
                position = Math.Min(height - knobHeight, position);
            }
            else
            {
                var width = this.width - style.Background.LeftWidth - style.Background.RightWidth;
                var knobWidth = knob == null ? 0 : knob.MinWidth;
                position = mousePos.X - style.Background.LeftWidth - knobWidth * 0.5f;
                value = Min + (Max - Min) * (position / (width - knobWidth));
                position = Math.Max(0, position);
                position = Math.Min(width - knobWidth, position);
            }

            SetValue(value);
        }
    }
}