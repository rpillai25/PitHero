using System;

namespace PitHero.Services.Replay.Frames
{
    /// <summary>One party member's HUD numbers (hero, mercenary 1, mercenary 2).</summary>
    public struct HudMember : IEquatable<HudMember>
    {
        public const int Size = 1 + 5 * 4;

        public bool Present;
        public int Hp, MaxHp, Mp, MaxMp, Level;

        public bool Equals(HudMember o)
            => Present == o.Present && Hp == o.Hp && MaxHp == o.MaxHp && Mp == o.Mp && MaxMp == o.MaxMp && Level == o.Level;
        public override bool Equals(object obj) => obj is HudMember m && Equals(m);
        public override int GetHashCode() => (Hp * 397) ^ MaxHp ^ (Mp << 8) ^ (Level << 16) ^ (Present ? 1 : 0);

        public void Write(ref FrameWriter w)
        {
            w.WriteU8(Present ? (byte)1 : (byte)0);
            w.WriteI32(Hp);
            w.WriteI32(MaxHp);
            w.WriteI32(Mp);
            w.WriteI32(MaxMp);
            w.WriteI32(Level);
        }

        public static HudMember Read(ref FrameReader r)
        {
            HudMember m;
            m.Present = r.ReadU8() != 0;
            m.Hp = r.ReadI32();
            m.MaxHp = r.ReadI32();
            m.Mp = r.ReadI32();
            m.MaxMp = r.ReadI32();
            m.Level = r.ReadI32();
            return m;
        }
    }

    /// <summary>
    /// One battle monster's slot in the HUD strip beside the gold display (format v6): its portrait
    /// sprite and tint, its HP, and where it stands so the viewer can hide the slot while the
    /// monster is off camera.
    /// </summary>
    public struct HudMonster : IEquatable<HudMonster>
    {
        public const int Size = 2 + 4 + 4 + 4 + 4 + 4;

        /// <summary>Stream id of the portrait sprite (MoveRight frame 0), 0 when it could not be resolved.</summary>
        public ushort Portrait;
        /// <summary>Packed tint of the portrait.</summary>
        public uint PortraitColor;
        public int Hp, MaxHp;
        /// <summary>World position of the monster.</summary>
        public float X, Y;

        public bool Equals(HudMonster o)
            => Portrait == o.Portrait && PortraitColor == o.PortraitColor && Hp == o.Hp && MaxHp == o.MaxHp && X == o.X && Y == o.Y;
        public override bool Equals(object obj) => obj is HudMonster m && Equals(m);
        public override int GetHashCode() => (Hp * 397) ^ MaxHp ^ Portrait;

        public void Write(ref FrameWriter w)
        {
            w.WriteU16(Portrait);
            w.WriteU32(PortraitColor);
            w.WriteI32(Hp);
            w.WriteI32(MaxHp);
            w.WriteF32(X);
            w.WriteF32(Y);
        }

        public static HudMonster Read(ref FrameReader r)
        {
            HudMonster m;
            m.Portrait = r.ReadU16();
            m.PortraitColor = r.ReadU32();
            m.Hp = r.ReadI32();
            m.MaxHp = r.ReadI32();
            m.X = r.ReadF32();
            m.Y = r.ReadF32();
            return m;
        }
    }

    /// <summary>One sprite of the hero's static HUD portrait (format v19, issue #462): its stream id and packed tint.</summary>
    public struct HudPortraitPart : IEquatable<HudPortraitPart>
    {
        public const int Size = 2 + 4;

        /// <summary>Stream id of the sprite, 0 when it could not be resolved.</summary>
        public ushort Sprite;
        /// <summary>Packed tint.</summary>
        public uint Color;

        public bool Equals(HudPortraitPart o) => Sprite == o.Sprite && Color == o.Color;
        public override bool Equals(object obj) => obj is HudPortraitPart p && Equals(p);
        public override int GetHashCode() => Sprite ^ (int)Color;

        public void Write(ref FrameWriter w)
        {
            w.WriteU16(Sprite);
            w.WriteU32(Color);
        }

        public static HudPortraitPart Read(ref FrameReader r)
        {
            HudPortraitPart p;
            p.Sprite = r.ReadU16();
            p.Color = r.ReadU32();
            return p;
        }
    }

    /// <summary>
    /// The fixed-layout HUD record captured with every frame (design §3.1): the numbers GraphicalHUD, the
    /// pit level label, the funds label and the clock show, plus (format v3, issue #431) the hero's
    /// static portrait: the walk-down first frame of the head, eyes and hair layers with their tints (every
    /// tone layer of them since format v19, issue #462),
    /// exactly what the live HUD draws, and (format v6) the monsters of the battle in progress for the
    /// HUD strip beside the gold display. Stored whole in the base frame and re-emitted in a delta only
    /// when it changed.
    /// </summary>
    public struct HudRecord : IEquatable<HudRecord>
    {
        /// <summary>Battle monsters the record carries; a hero can be attacked from at most its eight neighbouring tiles.</summary>
        public const int MaxMonsters = 8;

        /// <summary>
        /// Portrait sprites the record carries: every tone layer of the head, eye base, eyes and hair (issue #462:
        /// head 2 + eye base 1 + eyes 1 + hair up to 4).
        /// </summary>
        public const int MaxPortraitParts = 8;

        public const int Size = 3 * HudMember.Size + 8 + 4 + 4 + 4 + 1 + 1 + MaxPortraitParts * HudPortraitPart.Size + 1 + MaxMonsters * HudMonster.Size
            + 3 * 4 + 4 * 2; // pit warp (format v21): twist, pull, spin, region x/y/w/h

        /// <summary>How many of the monster slots are in use (0 outside a battle).</summary>
        public byte MonsterCount;
        private HudMonster _m0, _m1, _m2, _m3, _m4, _m5, _m6, _m7;

        /// <summary>The battle monster in a slot (slots at or past <see cref="MonsterCount"/> are default).</summary>
        public readonly HudMonster GetMonster(int index)
        {
            switch (index)
            {
                case 0: return _m0;
                case 1: return _m1;
                case 2: return _m2;
                case 3: return _m3;
                case 4: return _m4;
                case 5: return _m5;
                case 6: return _m6;
                case 7: return _m7;
                default: return default;
            }
        }

        public void SetMonster(int index, in HudMonster monster)
        {
            switch (index)
            {
                case 0: _m0 = monster; break;
                case 1: _m1 = monster; break;
                case 2: _m2 = monster; break;
                case 3: _m3 = monster; break;
                case 4: _m4 = monster; break;
                case 5: _m5 = monster; break;
                case 6: _m6 = monster; break;
                case 7: _m7 = monster; break;
            }
        }

        private readonly bool MonstersEqual(in HudRecord o)
            => MonsterCount == o.MonsterCount && _m0.Equals(o._m0) && _m1.Equals(o._m1) && _m2.Equals(o._m2) && _m3.Equals(o._m3)
               && _m4.Equals(o._m4) && _m5.Equals(o._m5) && _m6.Equals(o._m6) && _m7.Equals(o._m7);


        public HudMember Hero, Merc1, Merc2;
        public long Gold;
        public int PitLevel, PitTier;
        public float InGameSeconds;
        public bool Paused;

        /// <summary>The wizard-orb warp swirl on screen (issue #469): all zero outside a warp. Region is world pixels.</summary>
        public float PitWarpTwist, PitWarpPull, PitWarpSpin;
        public short PitWarpX, PitWarpY, PitWarpWidth, PitWarpHeight;
        /// <summary>How many portrait parts are in use (0 when the hero has no portrait on screen).</summary>
        public byte PortraitCount;
        private HudPortraitPart _p0, _p1, _p2, _p3, _p4, _p5, _p6, _p7;

        /// <summary>A portrait sprite, back to front (slots at or past <see cref="PortraitCount"/> are default).</summary>
        public readonly HudPortraitPart GetPortraitPart(int index)
        {
            switch (index)
            {
                case 0: return _p0;
                case 1: return _p1;
                case 2: return _p2;
                case 3: return _p3;
                case 4: return _p4;
                case 5: return _p5;
                case 6: return _p6;
                case 7: return _p7;
                default: return default;
            }
        }

        public void SetPortraitPart(int index, in HudPortraitPart part)
        {
            switch (index)
            {
                case 0: _p0 = part; break;
                case 1: _p1 = part; break;
                case 2: _p2 = part; break;
                case 3: _p3 = part; break;
                case 4: _p4 = part; break;
                case 5: _p5 = part; break;
                case 6: _p6 = part; break;
                case 7: _p7 = part; break;
            }
        }

        private readonly bool PortraitEqual(in HudRecord o)
            => PortraitCount == o.PortraitCount && _p0.Equals(o._p0) && _p1.Equals(o._p1) && _p2.Equals(o._p2) && _p3.Equals(o._p3)
               && _p4.Equals(o._p4) && _p5.Equals(o._p5) && _p6.Equals(o._p6) && _p7.Equals(o._p7);

        public bool Equals(HudRecord o)
            => Hero.Equals(o.Hero) && Merc1.Equals(o.Merc1) && Merc2.Equals(o.Merc2) && Gold == o.Gold
               && PitLevel == o.PitLevel && PitTier == o.PitTier && InGameSeconds == o.InGameSeconds && Paused == o.Paused
               && PitWarpTwist == o.PitWarpTwist && PitWarpPull == o.PitWarpPull && PitWarpSpin == o.PitWarpSpin
               && PitWarpX == o.PitWarpX && PitWarpY == o.PitWarpY && PitWarpWidth == o.PitWarpWidth && PitWarpHeight == o.PitWarpHeight
               && PortraitEqual(in o)
               && MonstersEqual(in o);
        public override bool Equals(object obj) => obj is HudRecord r && Equals(r);
        public override int GetHashCode() => Hero.GetHashCode() ^ (int)Gold ^ (PitLevel << 4) ^ InGameSeconds.GetHashCode();

        public void Write(ref FrameWriter w)
        {
            Hero.Write(ref w);
            Merc1.Write(ref w);
            Merc2.Write(ref w);
            w.WriteI64(Gold);
            w.WriteI32(PitLevel);
            w.WriteI32(PitTier);
            w.WriteF32(InGameSeconds);
            w.WriteU8(Paused ? (byte)1 : (byte)0);
            w.WriteU8(PortraitCount);
            for (int i = 0; i < MaxPortraitParts; i++)
                GetPortraitPart(i).Write(ref w);
            w.WriteU8(MonsterCount);
            for (int i = 0; i < MaxMonsters; i++)
                GetMonster(i).Write(ref w);
            w.WriteF32(PitWarpTwist);
            w.WriteF32(PitWarpPull);
            w.WriteF32(PitWarpSpin);
            w.WriteI16(PitWarpX);
            w.WriteI16(PitWarpY);
            w.WriteI16(PitWarpWidth);
            w.WriteI16(PitWarpHeight);
        }

        public static HudRecord Read(ref FrameReader r)
        {
            var h = new HudRecord();
            h.Hero = HudMember.Read(ref r);
            h.Merc1 = HudMember.Read(ref r);
            h.Merc2 = HudMember.Read(ref r);
            h.Gold = r.ReadI64();
            h.PitLevel = r.ReadI32();
            h.PitTier = r.ReadI32();
            h.InGameSeconds = r.ReadF32();
            h.Paused = r.ReadU8() != 0;
            h.PortraitCount = r.ReadU8();
            for (int i = 0; i < MaxPortraitParts; i++)
                h.SetPortraitPart(i, HudPortraitPart.Read(ref r));
            h.MonsterCount = r.ReadU8();
            for (int i = 0; i < MaxMonsters; i++)
                h.SetMonster(i, HudMonster.Read(ref r));
            h.PitWarpTwist = r.ReadF32();
            h.PitWarpPull = r.ReadF32();
            h.PitWarpSpin = r.ReadF32();
            h.PitWarpX = r.ReadI16();
            h.PitWarpY = r.ReadI16();
            h.PitWarpWidth = r.ReadI16();
            h.PitWarpHeight = r.ReadI16();
            return h;
        }
    }

    /// <summary>
    /// One simulation sound at a tick (issue #431): the resolved effect (the variant a group sound
    /// rolled), where it came from and whether it was positional. UI clicks are never recorded.
    /// </summary>
    public struct SoundEvent
    {
        /// <summary>Flag: the sound was played through PlaySoundAt (falloff and pan from the position).</summary>
        public const byte Positional = 1;

        public long Tick;
        /// <summary>A <c>SoundEffectType</c> ordinal.</summary>
        public byte Type;
        /// <summary>The variant a group sound picked (0 for single sounds).</summary>
        public byte Variant;
        public short X, Y;
        public byte Flags;

        public SoundEvent(long tick, byte type, byte variant, short x, short y, byte flags)
        {
            Tick = tick; Type = type; Variant = variant; X = x; Y = y; Flags = flags;
        }

        public bool IsPositional => (Flags & Positional) != 0;
    }

    /// <summary>One runtime tile mutation (TiledMapService.SetTile/RemoveTile) at a tick; gid 0 = removal.</summary>
    public struct TileEvent
    {
        public long Tick;
        public byte Layer;
        public ushort X, Y;
        public int Gid;

        public TileEvent(long tick, byte layer, ushort x, ushort y, int gid)
        {
            Tick = tick; Layer = layer; X = x; Y = y; Gid = gid;
        }
    }

    /// <summary>One segment of a recorded console line: interned text, color, optional interned item name.</summary>
    public struct ConsoleSegmentRecord
    {
        public ushort StringId;
        public uint Color;
        public ushort ItemStringId;

        public ConsoleSegmentRecord(ushort stringId, uint color, ushort itemStringId)
        {
            StringId = stringId; Color = color; ItemStringId = itemStringId;
        }
    }

    /// <summary>One console line at a tick; its segments live in the owning chunk's segment list.</summary>
    public struct ConsoleEvent
    {
        public long Tick;
        public int SegmentStart;
        public byte SegmentCount;

        public ConsoleEvent(long tick, int segmentStart, byte segmentCount)
        {
            Tick = tick; SegmentStart = segmentStart; SegmentCount = segmentCount;
        }
    }

    /// <summary>A full gid snapshot of one mutable tile layer, taken at a chunk's first tick.</summary>
    public struct TileLayerSnapshot
    {
        public byte LayerIndex;
        public ushort Width, Height;
        /// <summary>Row-major gids, length at least Width * Height (pooled; may be longer).</summary>
        public uint[] Gids;

        public int GidCount => Width * Height;
    }

    /// <summary>One entity of a decoded frame: its recorder id and its op bytes inside the frame's ops buffer.</summary>
    public struct FrameEntity
    {
        public ushort Id;
        public int Offset;
        public int Length;

        public FrameEntity(ushort id, int offset, int length)
        {
            Id = id; Offset = offset; Length = length;
        }
    }

    /// <summary>
    /// Identity of a frame sidecar: ties it to the .bin recording it caches. A sidecar whose identity
    /// does not match, or whose frame format version differs, is ignored and rebuilt (design §3.1).
    /// </summary>
    public struct FrameSidecarIdentity
    {
        public int MasterSeed;
        public long RecordedAtUtcTicks;
        public int SimulationVersion;
        public int FrameFormatVersion;
        /// <summary>Ticks recorded (frames exist for 0..TotalTicks-1); -1 while a session is still being written.</summary>
        public long TotalTicks;

        public FrameSidecarIdentity(int masterSeed, long recordedAtUtcTicks, int simulationVersion, int frameFormatVersion, long totalTicks)
        {
            MasterSeed = masterSeed;
            RecordedAtUtcTicks = recordedAtUtcTicks;
            SimulationVersion = simulationVersion;
            FrameFormatVersion = frameFormatVersion;
            TotalTicks = totalTicks;
        }

        /// <summary>True when this sidecar belongs to the recording with these header values.</summary>
        public bool MatchesRecording(int masterSeed, long recordedAtUtcTicks, int simulationVersion)
            => MasterSeed == masterSeed && RecordedAtUtcTicks == recordedAtUtcTicks && SimulationVersion == simulationVersion;
    }
}
