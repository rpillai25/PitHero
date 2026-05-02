using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using PitHero;

namespace PitHero.Services
{
    /// <summary>A text segment with an associated display color, used for colored console rows.</summary>
    public readonly struct ConsoleSegment
    {
        public readonly string Text;
        public readonly Color Color;

        public ConsoleSegment(string text, Color color)
        {
            Text = text;
            Color = color;
        }

        /// <summary>
        /// Splits a localized format string on {N} tokens and assigns each argument its own color.
        /// Literal text between tokens is white. Unrecognized tokens are left as-is.
        /// </summary>
        public static ConsoleSegment[] Build(string format, params (string text, Color color)[] args)
        {
            var result = new List<ConsoleSegment>(args.Length * 2 + 1);
            int pos = 0;
            while (pos < format.Length)
            {
                int open = format.IndexOf('{', pos);
                if (open < 0)
                {
                    if (pos < format.Length)
                        result.Add(new ConsoleSegment(format.Substring(pos), Color.White));
                    break;
                }
                if (open > pos)
                    result.Add(new ConsoleSegment(format.Substring(pos, open - pos), Color.White));
                int close = format.IndexOf('}', open + 1);
                if (close < 0) break;
                if (int.TryParse(format.Substring(open + 1, close - open - 1), out int idx) && (uint)idx < (uint)args.Length)
                    result.Add(new ConsoleSegment(args[idx].text, args[idx].color));
                pos = close + 1;
            }
            return result.ToArray();
        }
    }

    /// <summary>Global service for broadcasting gameplay events to UI listeners such as the EventConsolePanel.</summary>
    public class GameEventService
    {
        private readonly TextService _textService;

        public GameEventService(TextService textService)
        {
            _textService = textService;
        }

        /// <summary>Fired whenever a game event is emitted. Each element is a colored text segment.</summary>
        public event Action<ConsoleSegment[]> OnEvent;

        /// <summary>Broadcasts a plain white message.</summary>
        public void Emit(string message)
        {
            OnEvent?.Invoke(new[] { new ConsoleSegment(message, Color.White) });
        }

        /// <summary>Broadcasts a colored segment array built via <see cref="ConsoleSegment.Build"/>.</summary>
        public void Emit(ConsoleSegment[] segments)
        {
            OnEvent?.Invoke(segments);
        }

        /// <summary>Looks up the UI localized format for key and emits a colored segment row.</summary>
        public void EmitLocalized(string key, params (string text, Color color)[] args)
        {
            string format = _textService.DisplayText(TextType.UI, key);
            Emit(args.Length == 0
                ? new[] { new ConsoleSegment(format, Color.White) }
                : ConsoleSegment.Build(format, args));
        }

        /// <summary>Resolves a localized monster name for use as an EmitLocalized argument.</summary>
        public string MonsterName(string key) => _textService.DisplayText(TextType.Monster, key);
    }
}
