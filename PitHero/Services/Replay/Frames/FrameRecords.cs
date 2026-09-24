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
    /// The fixed-layout HUD record captured with every frame (design §3.1): the numbers GraphicalHUD, the
    /// pit level label, the funds label and the clock show. Stored whole in the base frame and re-emitted
    /// in a delta only when it changed.
    /// </summary>
    public struct HudRecord : IEquatable<HudRecord>
    {
        public const int Size = 3 * HudMember.Size + 8 + 4 + 4 + 4 + 1;

        public HudMember Hero, Merc1, Merc2;
        public long Gold;
        public int PitLevel, PitTier;
        public float InGameSeconds;
        public bool Paused;

        public bool Equals(HudRecord o)
            => Hero.Equals(o.Hero) && Merc1.Equals(o.Merc1) && Merc2.Equals(o.Merc2) && Gold == o.Gold
               && PitLevel == o.PitLevel && PitTier == o.PitTier && InGameSeconds == o.InGameSeconds && Paused == o.Paused;
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
        }

        public static HudRecord Read(ref FrameReader r)
        {
            HudRecord h;
            h.Hero = HudMember.Read(ref r);
            h.Merc1 = HudMember.Read(ref r);
            h.Merc2 = HudMember.Read(ref r);
            h.Gold = r.ReadI64();
            h.PitLevel = r.ReadI32();
            h.PitTier = r.ReadI32();
            h.InGameSeconds = r.ReadF32();
            h.Paused = r.ReadU8() != 0;
            return h;
        }
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
