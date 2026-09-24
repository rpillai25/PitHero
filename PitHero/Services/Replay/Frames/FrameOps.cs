using System;
using System.IO;
using System.Text;

namespace PitHero.Services.Replay.Frames
{
    /// <summary>
    /// Op codes of an entity's draw-op byte string (design doc features/feature_replay_frame_recording_424.md
    /// §3.1). Every op starts with its code byte; all positions are integer pixels (i16, measured in #425 to
    /// be the single biggest compression lever), depths are f32, colors are packed RGBA (Color.PackedValue).
    /// Layouts (bytes after the code byte):
    ///   Sprite     spriteId u16, x i16, y i16, layerDepth f32, renderLayer i16, color u32, flags u8          (17)
    ///   Composite  x i16, y i16, layerDepth f32, renderLayer i16, layerCount u8,
    ///              then per layer: spriteId u16, dx i16, dy i16, color u32, flags u8                           (11 + 11/layer)
    ///   Text       stringId u16, x i16, y i16, dx i16, dy i16, color u32, scale f32, fontId u8, flags u8,
    ///              charStart u16, charCount u16 (0xFFFF = to the end)                                          (24)
    ///   Rect       x i16, y i16, dx i16, dy i16, w i16, h i16, color u32, flags u8                             (17)
    ///   NinePatch  patchId u16, x i16, y i16, dx i16, dy i16, w i16, h i16, color u32, flags u8                (19)
    /// x/y are the anchor. With <see cref="FrameOpFlags.ConstantScreenSize"/> the anchor is a world point and
    /// dx/dy/w/h are screen pixels applied after the world-to-screen transform (constant on-screen size at
    /// any zoom: damage numbers, HP bars, speech bubbles); otherwise dx/dy/w/h share the anchor's space.
    /// </summary>
    public static class FrameOpCode
    {
        public const byte Sprite = 1;
        public const byte Composite = 2;
        public const byte Text = 3;
        public const byte Rect = 4;
        public const byte NinePatch = 5;

        /// <summary>Payload bytes after the code byte for the fixed-size ops; Composite is variable.</summary>
        public const int SpritePayload = 17;
        public const int CompositeHeaderPayload = 11;
        public const int CompositeLayerBytes = 11;
        public const int TextPayload = 24;
        public const int RectPayload = 17;
        public const int NinePatchPayload = 19;

        /// <summary>Text charCount meaning "every character from charStart".</summary>
        public const ushort AllChars = 0xFFFF;
    }

    /// <summary>Bit flags carried by the flags byte of Sprite, Composite layer, Text and Rect ops.</summary>
    public static class FrameOpFlags
    {
        public const byte None = 0;
        public const byte FlipX = 1;
        public const byte FlipY = 2;
        public const byte ScreenSpace = 4;
        public const byte Centered = 8;
        public const byte Outline = 16;
        /// <summary>Anchor is a world point; dx/dy/w/h and the text scale are screen pixels (constant size at any zoom).</summary>
        public const byte ConstantScreenSize = 32;
        /// <summary>Text is centered vertically on its anchor (Centered alone centers horizontally).</summary>
        public const byte CenteredY = 64;
    }

    /// <summary>Fonts a Text op can name; the viewer maps them to its own loaded fonts.</summary>
    public static class FrameFontId
    {
        /// <summary>The HUD font (GameConfig.FontPathHud, "Skullboy").</summary>
        public const byte Hud = 0;
        /// <summary>The half-window HUD font (GameConfig.FontPathHud2x).</summary>
        public const byte Hud2x = 1;
        /// <summary>The speech-bubble / main UI font (GameConfig.FontPathSpeechBubble, "Express").</summary>
        public const byte SpeechBubble = 2;
        /// <summary>The half-window speech-bubble font (GameConfig.FontPathSpeechBubble2x).</summary>
        public const byte SpeechBubble2x = 3;
    }

    public struct SpriteOp
    {
        public ushort SpriteId;
        public short X, Y;
        public float LayerDepth;
        public short RenderLayer;
        public uint Color;
        public byte Flags;
    }

    public struct CompositeOp
    {
        public short X, Y;
        public float LayerDepth;
        public short RenderLayer;
        public byte LayerCount;
    }

    public struct CompositeLayerOp
    {
        public ushort SpriteId;
        public short DX, DY;
        public uint Color;
        public byte Flags;
    }

    public struct TextOp
    {
        public ushort StringId;
        public short X, Y, DX, DY;
        public uint Color;
        public float Scale;
        public byte FontId;
        public byte Flags;
        public ushort CharStart, CharCount;
    }

    public struct RectOp
    {
        public short X, Y, DX, DY, Width, Height;
        public uint Color;
        public byte Flags;
    }

    public struct NinePatchOp
    {
        public ushort PatchId;
        public short X, Y, DX, DY, Width, Height;
        public uint Color;
        public byte Flags;
    }

    /// <summary>
    /// Little-endian writer over a caller-owned byte[]; grows by doubling (warm-up only), never allocates
    /// per op. Because it is a ref struct, pass it by <c>ref</c> so a grown buffer reaches the caller, and
    /// read <see cref="Buffer"/> back after writing.
    /// </summary>
    public ref struct FrameWriter
    {
        private byte[] _buffer;
        private int _length;

        public FrameWriter(byte[] buffer)
        {
            _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
            _length = 0;
        }

        /// <summary>A writer that appends after <paramref name="startLength"/> bytes already in the buffer.</summary>
        public FrameWriter(byte[] buffer, int startLength)
        {
            _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
            if (startLength < 0 || startLength > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(startLength));
            _length = startLength;
        }

        /// <summary>The backing array (may differ from the one passed in after growth).</summary>
        public byte[] Buffer => _buffer;
        /// <summary>Bytes written so far.</summary>
        public int Length => _length;
        /// <summary>The written bytes.</summary>
        public ReadOnlySpan<byte> Written => new ReadOnlySpan<byte>(_buffer, 0, _length);

        /// <summary>Rewinds to the start without touching the buffer.</summary>
        public void Reset() => _length = 0;

        private void Ensure(int extra)
        {
            int need = _length + extra;
            if (need <= _buffer.Length)
                return;
            int size = Math.Max(_buffer.Length * 2, 64);
            while (size < need)
                size *= 2;
            var grown = new byte[size];
            System.Buffer.BlockCopy(_buffer, 0, grown, 0, _length);
            _buffer = grown;
        }

        /// <summary>Appends one byte.</summary>
        public void WriteU8(byte v)
        {
            Ensure(1);
            _buffer[_length++] = v;
        }

        /// <summary>Appends a little-endian u16.</summary>
        public void WriteU16(ushort v)
        {
            Ensure(2);
            _buffer[_length++] = (byte)v;
            _buffer[_length++] = (byte)(v >> 8);
        }

        /// <summary>Appends a little-endian i16.</summary>
        public void WriteI16(short v) => WriteU16((ushort)v);

        /// <summary>Appends a little-endian u32.</summary>
        public void WriteU32(uint v)
        {
            Ensure(4);
            _buffer[_length++] = (byte)v;
            _buffer[_length++] = (byte)(v >> 8);
            _buffer[_length++] = (byte)(v >> 16);
            _buffer[_length++] = (byte)(v >> 24);
        }

        /// <summary>Appends a little-endian i32.</summary>
        public void WriteI32(int v) => WriteU32((uint)v);

        /// <summary>Appends a little-endian i64.</summary>
        public void WriteI64(long v)
        {
            WriteU32((uint)v);
            WriteU32((uint)(v >> 32));
        }

        /// <summary>Appends an IEEE-754 single as its little-endian bit pattern.</summary>
        public void WriteF32(float v) => WriteU32((uint)BitConverter.SingleToInt32Bits(v));

        /// <summary>Appends a byte range.</summary>
        public void WriteBytes(byte[] src, int offset, int count)
        {
            Ensure(count);
            System.Buffer.BlockCopy(src, offset, _buffer, _length, count);
            _length += count;
        }

        /// <summary>Appends a span of bytes.</summary>
        public void WriteBytes(ReadOnlySpan<byte> src)
        {
            Ensure(src.Length);
            src.CopyTo(new Span<byte>(_buffer, _length, src.Length));
            _length += src.Length;
        }

        /// <summary>u16 byte length + UTF-8 bytes, no intermediate allocation. Table deltas only.</summary>
        public void WriteString(string s)
        {
            s ??= string.Empty;
            int bytes = Encoding.UTF8.GetByteCount(s);
            if (bytes > ushort.MaxValue)
                throw new ArgumentException("String too long for a frame table entry");
            WriteU16((ushort)bytes);
            Ensure(bytes);
            Encoding.UTF8.GetBytes(s, 0, s.Length, _buffer, _length);
            _length += bytes;
        }

        /// <summary>Writes a placeholder u32 and returns its offset for <see cref="PatchU32"/>.</summary>
        public int ReserveU32()
        {
            int at = _length;
            WriteU32(0);
            return at;
        }

        /// <summary>Overwrites a u32 written earlier (section lengths, counts).</summary>
        public void PatchU32(int at, uint v)
        {
            _buffer[at] = (byte)v;
            _buffer[at + 1] = (byte)(v >> 8);
            _buffer[at + 2] = (byte)(v >> 16);
            _buffer[at + 3] = (byte)(v >> 24);
        }

        /// <summary>
        /// Rounds a world/screen coordinate to the nearest pixel (half to even, the same rounding
        /// SpriteCompositorBase applies to entity positions) and clamps to the i16 range.
        /// </summary>
        public static short ToPixel(float v)
        {
            float r = MathF.Round(v, MidpointRounding.ToEven);
            if (r > short.MaxValue) return short.MaxValue;
            if (r < short.MinValue) return short.MinValue;
            return (short)r;
        }

        /// <summary>Sprite op from live values (positions snapped to pixels).</summary>
        public void WriteSprite(ushort spriteId, float x, float y, float layerDepth, int renderLayer, uint color, byte flags)
        {
            WriteU8(FrameOpCode.Sprite);
            WriteU16(spriteId);
            WriteI16(ToPixel(x));
            WriteI16(ToPixel(y));
            WriteF32(layerDepth);
            WriteI16((short)renderLayer);
            WriteU32(color);
            WriteU8(flags);
        }

        /// <summary>Sprite op from a decoded record.</summary>
        public void WriteSprite(in SpriteOp op)
        {
            WriteU8(FrameOpCode.Sprite);
            WriteU16(op.SpriteId);
            WriteI16(op.X);
            WriteI16(op.Y);
            WriteF32(op.LayerDepth);
            WriteI16(op.RenderLayer);
            WriteU32(op.Color);
            WriteU8(op.Flags);
        }

        /// <summary>Composite header; follow it with exactly <paramref name="layerCount"/> <see cref="WriteCompositeLayer"/> calls.</summary>
        public void WriteCompositeHeader(float x, float y, float layerDepth, int renderLayer, byte layerCount)
        {
            WriteU8(FrameOpCode.Composite);
            WriteI16(ToPixel(x));
            WriteI16(ToPixel(y));
            WriteF32(layerDepth);
            WriteI16((short)renderLayer);
            WriteU8(layerCount);
        }

        /// <summary>One composite layer (offsets snapped to pixels).</summary>
        public void WriteCompositeLayer(ushort spriteId, float dx, float dy, uint color, byte flags)
        {
            WriteU16(spriteId);
            WriteI16(ToPixel(dx));
            WriteI16(ToPixel(dy));
            WriteU32(color);
            WriteU8(flags);
        }

        /// <summary>Text op drawn whole at its anchor.</summary>
        public void WriteText(ushort stringId, float x, float y, uint color, float scale, byte fontId, byte flags)
            => WriteText(stringId, x, y, 0f, 0f, color, scale, fontId, flags, 0, FrameOpCode.AllChars);

        /// <summary>Text op with an offset from the anchor and a character range of the interned string.</summary>
        public void WriteText(ushort stringId, float x, float y, float dx, float dy, uint color, float scale, byte fontId, byte flags, ushort charStart, ushort charCount)
        {
            WriteU8(FrameOpCode.Text);
            WriteU16(stringId);
            WriteI16(ToPixel(x));
            WriteI16(ToPixel(y));
            WriteI16(ToPixel(dx));
            WriteI16(ToPixel(dy));
            WriteU32(color);
            WriteF32(scale);
            WriteU8(fontId);
            WriteU8(flags);
            WriteU16(charStart);
            WriteU16(charCount);
        }

        /// <summary>Rect op (filled or outlined per flags) at its anchor.</summary>
        public void WriteRect(float x, float y, float width, float height, uint color, byte flags)
            => WriteRect(x, y, 0f, 0f, width, height, color, flags);

        /// <summary>Rect op with an offset from the anchor.</summary>
        public void WriteRect(float x, float y, float dx, float dy, float width, float height, uint color, byte flags)
        {
            WriteU8(FrameOpCode.Rect);
            WriteI16(ToPixel(x));
            WriteI16(ToPixel(y));
            WriteI16(ToPixel(dx));
            WriteI16(ToPixel(dy));
            WriteI16(ToPixel(width));
            WriteI16(ToPixel(height));
            WriteU32(color);
            WriteU8(flags);
        }

        /// <summary>Nine-patch op at its anchor.</summary>
        public void WriteNinePatch(ushort patchId, float x, float y, float width, float height, uint color)
            => WriteNinePatch(patchId, x, y, 0f, 0f, width, height, color, FrameOpFlags.None);

        /// <summary>Nine-patch op with an offset from the anchor.</summary>
        public void WriteNinePatch(ushort patchId, float x, float y, float dx, float dy, float width, float height, uint color, byte flags)
        {
            WriteU8(FrameOpCode.NinePatch);
            WriteU16(patchId);
            WriteI16(ToPixel(x));
            WriteI16(ToPixel(y));
            WriteI16(ToPixel(dx));
            WriteI16(ToPixel(dy));
            WriteI16(ToPixel(width));
            WriteI16(ToPixel(height));
            WriteU32(color);
            WriteU8(flags);
        }
    }

    /// <summary>
    /// Little-endian reader over a byte range. Every read is bounds-checked and throws
    /// <see cref="InvalidDataException"/> on overrun, so corrupt chunk bytes fail loudly instead of
    /// producing garbage. Read an op with <see cref="ReadOpCode"/> followed by the matching typed reader.
    /// </summary>
    public ref struct FrameReader
    {
        private const string OverrunMessage = "Frame data ended unexpectedly";

        private readonly byte[] _buffer;
        private readonly int _end;
        private int _pos;

        public FrameReader(byte[] buffer, int offset, int length)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || length < 0 || offset + length > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(length));
            _buffer = buffer;
            _pos = offset;
            _end = offset + length;
        }

        public FrameReader(byte[] buffer) : this(buffer, 0, buffer?.Length ?? 0) { }

        /// <summary>Absolute offset of the next byte in the backing array.</summary>
        public int Position => _pos;
        /// <summary>Bytes left in the range.</summary>
        public int Remaining => _end - _pos;
        public bool AtEnd => _pos >= _end;

        private void Need(int n)
        {
            if (_pos + n > _end)
                throw new InvalidDataException(OverrunMessage);
        }

        /// <summary>Reads one byte.</summary>
        public byte ReadU8()
        {
            Need(1);
            return _buffer[_pos++];
        }

        /// <summary>Reads a little-endian u16.</summary>
        public ushort ReadU16()
        {
            Need(2);
            ushort v = (ushort)(_buffer[_pos] | (_buffer[_pos + 1] << 8));
            _pos += 2;
            return v;
        }

        /// <summary>Reads a little-endian i16.</summary>
        public short ReadI16() => (short)ReadU16();

        /// <summary>Reads a little-endian u32.</summary>
        public uint ReadU32()
        {
            Need(4);
            uint v = (uint)(_buffer[_pos] | (_buffer[_pos + 1] << 8) | (_buffer[_pos + 2] << 16) | (_buffer[_pos + 3] << 24));
            _pos += 4;
            return v;
        }

        /// <summary>Reads a little-endian i32.</summary>
        public int ReadI32() => (int)ReadU32();

        /// <summary>Reads a little-endian i64.</summary>
        public long ReadI64()
        {
            uint lo = ReadU32();
            uint hi = ReadU32();
            return (long)(((ulong)hi << 32) | lo);
        }

        /// <summary>Reads an IEEE-754 single.</summary>
        public float ReadF32() => BitConverter.Int32BitsToSingle((int)ReadU32());

        /// <summary>Reads a u16-length-prefixed UTF-8 string (allocates; table deltas only).</summary>
        public string ReadString()
        {
            int len = ReadU16();
            Need(len);
            string s = len == 0 ? string.Empty : Encoding.UTF8.GetString(_buffer, _pos, len);
            _pos += len;
            return s;
        }

        /// <summary>Copies the next bytes into a span.</summary>
        public void ReadBytes(Span<byte> dst)
        {
            Need(dst.Length);
            new ReadOnlySpan<byte>(_buffer, _pos, dst.Length).CopyTo(dst);
            _pos += dst.Length;
        }

        /// <summary>The next <paramref name="count"/> bytes as a span, without copying; advances the cursor.</summary>
        public ReadOnlySpan<byte> Slice(int count)
        {
            Need(count);
            var span = new ReadOnlySpan<byte>(_buffer, _pos, count);
            _pos += count;
            return span;
        }

        /// <summary>Advances past bytes without reading them.</summary>
        public void Skip(int count)
        {
            Need(count);
            _pos += count;
        }

        /// <summary>The op code at the cursor without consuming it.</summary>
        public byte PeekOp()
        {
            Need(1);
            return _buffer[_pos];
        }

        /// <summary>Consumes and returns the next op code; follow with the matching typed reader.</summary>
        public byte ReadOpCode() => ReadU8();

        /// <summary>Sprite payload (after its op code).</summary>
        public void ReadSprite(out SpriteOp op)
        {
            op.SpriteId = ReadU16();
            op.X = ReadI16();
            op.Y = ReadI16();
            op.LayerDepth = ReadF32();
            op.RenderLayer = ReadI16();
            op.Color = ReadU32();
            op.Flags = ReadU8();
        }

        /// <summary>Composite header payload (after its op code); follow with LayerCount layer reads.</summary>
        public void ReadCompositeHeader(out CompositeOp op)
        {
            op.X = ReadI16();
            op.Y = ReadI16();
            op.LayerDepth = ReadF32();
            op.RenderLayer = ReadI16();
            op.LayerCount = ReadU8();
        }

        /// <summary>One composite layer record.</summary>
        public void ReadCompositeLayer(out CompositeLayerOp layer)
        {
            layer.SpriteId = ReadU16();
            layer.DX = ReadI16();
            layer.DY = ReadI16();
            layer.Color = ReadU32();
            layer.Flags = ReadU8();
        }

        /// <summary>Text payload (after its op code).</summary>
        public void ReadText(out TextOp op)
        {
            op.StringId = ReadU16();
            op.X = ReadI16();
            op.Y = ReadI16();
            op.DX = ReadI16();
            op.DY = ReadI16();
            op.Color = ReadU32();
            op.Scale = ReadF32();
            op.FontId = ReadU8();
            op.Flags = ReadU8();
            op.CharStart = ReadU16();
            op.CharCount = ReadU16();
        }

        /// <summary>Rect payload (after its op code).</summary>
        public void ReadRect(out RectOp op)
        {
            op.X = ReadI16();
            op.Y = ReadI16();
            op.DX = ReadI16();
            op.DY = ReadI16();
            op.Width = ReadI16();
            op.Height = ReadI16();
            op.Color = ReadU32();
            op.Flags = ReadU8();
        }

        /// <summary>Nine-patch payload (after its op code).</summary>
        public void ReadNinePatch(out NinePatchOp op)
        {
            op.PatchId = ReadU16();
            op.X = ReadI16();
            op.Y = ReadI16();
            op.DX = ReadI16();
            op.DY = ReadI16();
            op.Width = ReadI16();
            op.Height = ReadI16();
            op.Color = ReadU32();
            op.Flags = ReadU8();
        }

        /// <summary>Skips one whole op (code + payload) at the cursor; throws on an unknown code.</summary>
        public void SkipOp()
        {
            byte code = ReadOpCode();
            switch (code)
            {
                case FrameOpCode.Sprite: Skip(FrameOpCode.SpritePayload); break;
                case FrameOpCode.Composite:
                    Skip(FrameOpCode.CompositeHeaderPayload - 1);
                    int layers = ReadU8();
                    Skip(layers * FrameOpCode.CompositeLayerBytes);
                    break;
                case FrameOpCode.Text: Skip(FrameOpCode.TextPayload); break;
                case FrameOpCode.Rect: Skip(FrameOpCode.RectPayload); break;
                case FrameOpCode.NinePatch: Skip(FrameOpCode.NinePatchPayload); break;
                default: throw new InvalidDataException("Unknown frame op code " + code);
            }
        }
    }
}
