using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nez;
using Nez.BitmapFonts;
using Nez.Sprites;
using Nez.Textures;
using PitHero.ECS.Components;
using PitHero.Services.Replay.Frames;

namespace PitHero.Rendering
{
    /// <summary>
    /// Resolves the frame stream's ids back to drawable objects for the viewer (design §3.2): sprite
    /// ids → <see cref="Sprite"/> through the registry's (textureName, sourceRect) keys, nine-patch ids
    /// → <see cref="NinePatchSprite"/>, font ids → <see cref="BitmapFont"/>. Real atlas sprites are
    /// preferred (they carry the atlas origins); a key nobody loaded is rebuilt from its texture by
    /// name (a centre-origin sprite, which is what grid-sliced sheets use). A key with no texture
    /// draws nothing and warns once. Every lookup after the first is an array index.
    /// </summary>
    public sealed class FrameSpriteResolver
    {
        private static readonly string[] AtlasPaths =
        {
            "Content/Atlases/Actors.atlas",
            "Content/Atlases/CropsProps.atlas",
            "Content/Atlases/Items.atlas",
            "Content/Atlases/SkillsStencils.atlas",
            "Content/Atlases/UI.atlas",
        };

        private const string UiAtlasPath = "Content/Atlases/UI.atlas";
        private const string SpeechBubblePatchName = "NinePatchSpeechBubble";
        private const string SpeechBubbleTailName = "SpeechBubbleTail";
        private const int SpeechBubbleBorder = 4;

        private readonly SpriteKeyRegistry _registry;
        private readonly Dictionary<SpriteKey, Sprite> _known = new Dictionary<SpriteKey, Sprite>(2048);
        private readonly Dictionary<string, Texture2D> _textures = new Dictionary<string, Texture2D>(16, StringComparer.Ordinal);
        private readonly HashSet<string> _warned = new HashSet<string>(StringComparer.Ordinal);
        private Sprite[] _spritesById = new Sprite[1024];
        private bool[] _resolved = new bool[1024];
        private NinePatchSprite[] _patchesById = new NinePatchSprite[8];
        private readonly BitmapFont[] _fonts = new BitmapFont[4];
        private Sprite _speechBubbleTail;
        private bool _atlasesLearned;

        public FrameSpriteResolver(SpriteKeyRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        /// <summary>Learns every sprite of the loaded atlases (all animation frames, with their origins).</summary>
        public void LearnAtlases()
        {
            if (_atlasesLearned || Core.Instance == null)
                return;
            _atlasesLearned = true;
            for (int i = 0; i < AtlasPaths.Length; i++)
            {
                SpriteAtlas atlas;
                try
                {
                    atlas = Core.Content.LoadSpriteAtlas(AtlasPaths[i]);
                }
                catch (Exception ex)
                {
                    Debug.Warn("[FrameViewer] Atlas " + AtlasPaths[i] + " could not be loaded for the viewer: " + ex.Message);
                    continue;
                }
                if (atlas?.Sprites == null)
                    continue;
                for (int s = 0; s < atlas.Sprites.Length; s++)
                    Learn(atlas.Sprites[s]);
            }
        }

        /// <summary>
        /// Registers an atlas sprite. Sprites are deliberately not learned from the scene's renderables:
        /// a texture loaded through a scene's own content manager dies with that scene, and the viewer
        /// outlives a scene rebuild. Non-atlas keys go through <see cref="Core.Content"/> instead, which
        /// is global.
        /// </summary>
        private void Learn(Sprite sprite)
        {
            if (sprite?.Texture2D == null)
                return;
            string name = sprite.Texture2D.Name;
            if (string.IsNullOrEmpty(name))
                return;
            if (!_textures.ContainsKey(name))
                _textures[name] = sprite.Texture2D;
            var r = sprite.SourceRect;
            var key = new SpriteKey(name, r.X, r.Y, r.Width, r.Height);
            if (!_known.ContainsKey(key))
                _known[key] = sprite;
        }

        /// <summary>The sprite for a stream id, or null when it cannot be resolved (warned once per texture).</summary>
        public Sprite Get(ushort id)
        {
            if (id == SpriteKeyRegistry.None)
                return null;
            if (id >= _resolved.Length)
                Grow(id);
            if (_resolved[id])
            {
                var cached = _spritesById[id];
                if (cached == null || !cached.Texture2D.IsDisposed)
                    return cached;
                // The texture died with a scene: resolve the key again through the global content
                _spritesById[id] = null;
            }
            _resolved[id] = true;
            if (!_registry.TryGetSpriteKey(id, out var key))
                return null;
            if (_known.TryGetValue(key, out var stale) && stale.Texture2D.IsDisposed)
            {
                _known.Remove(key);
                _textures.Remove(key.TextureName);
            }
            if (_known.TryGetValue(key, out var sprite))
            {
                _spritesById[id] = sprite;
                return sprite;
            }
            var texture = TextureByName(key.TextureName);
            if (texture == null)
                return null;
            sprite = new Sprite(texture, new Rectangle(key.X, key.Y, key.Width, key.Height));
            _known[key] = sprite;
            _spritesById[id] = sprite;
            return sprite;
        }

        private void Grow(int id)
        {
            int size = _resolved.Length;
            while (size <= id)
                size *= 2;
            Array.Resize(ref _spritesById, size);
            Array.Resize(ref _resolved, size);
        }

        private Texture2D TextureByName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;
            if (_textures.TryGetValue(name, out var texture) && !texture.IsDisposed)
                return texture;
            texture = null;
            if (Core.Instance != null)
            {
                try
                {
                    texture = Core.Content.LoadTexture(name);
                }
                catch (Exception)
                {
                    texture = null;
                }
            }
            if (texture == null)
            {
                if (_warned.Add(name))
                    Debug.Warn("[FrameViewer] No texture named '" + name + "' is loaded; its recorded sprites are not drawn");
                return null;
            }
            _textures[name] = texture;
            return texture;
        }

        /// <summary>The nine-patch for a stream id (speech bubble body), or null.</summary>
        public NinePatchSprite GetNinePatch(ushort patchId)
        {
            if (patchId == SpriteKeyRegistry.None)
                return null;
            if (patchId >= _patchesById.Length)
                Array.Resize(ref _patchesById, Math.Max(patchId + 1, _patchesById.Length * 2));
            var patch = _patchesById[patchId];
            if (patch != null)
                return patch;
            string name = _registry.GetNinePatch(patchId);
            if (name != SpeechBubblePatchName)
            {
                if (name != null && _warned.Add("patch:" + name))
                    Debug.Warn("[FrameViewer] Unknown nine-patch '" + name + "' in the frame stream");
                return null;
            }
            var sprite = UiSprite(SpeechBubblePatchName);
            if (sprite == null)
                return null;
            patch = new NinePatchSprite(sprite.Texture2D, sprite.SourceRect, SpeechBubbleBorder, SpeechBubbleBorder, SpeechBubbleBorder, SpeechBubbleBorder);
            _patchesById[patchId] = patch;
            return patch;
        }

        /// <summary>The tail drawn under every speech-bubble nine-patch.</summary>
        public Sprite SpeechBubbleTail => _speechBubbleTail ??= UiSprite(SpeechBubbleTailName);

        private static Sprite UiSprite(string name)
        {
            if (Core.Instance == null)
                return null;
            try
            {
                return Core.Content.LoadSpriteAtlas(UiAtlasPath)?.GetSprite(name);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The font for a <see cref="FrameFontId"/> (loaded through the global content manager, so it survives scene rebuilds).</summary>
        public BitmapFont Font(byte fontId)
        {
            if (fontId >= _fonts.Length || Core.Instance == null)
                return null;
            var font = _fonts[fontId];
            if (font != null)
                return font;
            string path = fontId switch
            {
                FrameFontId.Hud => GameConfig.FontPathHud,
                FrameFontId.Hud2x => GameConfig.FontPathHud2x,
                FrameFontId.SpeechBubble => GameConfig.FontPathSpeechBubble,
                FrameFontId.SpeechBubble2x => GameConfig.FontPathSpeechBubble2x,
                _ => null,
            };
            if (path == null)
                return null;
            try
            {
                font = Core.Content.LoadBitmapFont(path);
            }
            catch (Exception ex)
            {
                Debug.Warn("[FrameViewer] Font " + path + " could not be loaded: " + ex.Message);
                return null;
            }
            _fonts[fontId] = font;
            return font;
        }
    }
}
