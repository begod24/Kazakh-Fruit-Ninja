using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace KazakhNinja.EditorTools
{
    /// <summary>
    /// Builds the visual effects: shaders' materials, the FX library prefab (one particle system per kind of
    /// particle), the blades (trail materials, particles, swatches), the backdrop scenes and their atmosphere,
    /// the post-processing profiles, and wires them into the Game scene. Generated assets are rebuilt on every
    /// run, so tweaks belong in this file.
    /// </summary>
    public static class FxBuilder
    {
        const string Root = "Assets/_Game";
        const string MaterialFolder = Root + "/Materials/FX";
        const string PrefabFolder = Root + "/Prefabs/Effects";
        const string BladePrefabFolder = PrefabFolder + "/Blades";
        const string AmbientPrefabFolder = PrefabFolder + "/Ambient";
        const string BackgroundDataFolder = Root + "/Data/Backgrounds";
        const string VolumeFolder = Root + "/Settings/Volumes";
        const string ShaderFolder = Root + "/Shaders";

        public const string FxLibraryPath = PrefabFolder + "/FX.prefab";
        const string GlowMaterialPath = MaterialFolder + "/FX_Glow.mat";
        const string SoftGlowMaterialPath = MaterialFolder + "/FX_GlowSoft.mat";
        const string SolidMaterialPath = MaterialFolder + "/FX_Solid.mat";
        const string SplatMaterialPath = MaterialFolder + "/FX_Splat.mat";
        const string BackdropMaterialPath = MaterialFolder + "/Backdrop.mat";

        [MenuItem("Kazakh Ninja/Build Effects", priority = 24)]
        public static void BuildEffects() => Build(false);

        [MenuItem("Kazakh Ninja/Repaint Effects Art", priority = 25)]
        public static void RepaintArt() => Build(true);

        /// <summary>Everything in this file; <paramref name="repaint"/> also regenerates the procedural textures.</summary>
        public static void Build(bool repaint)
        {
            FxArt.BuildAll(repaint);
            BuildLibrary();
            BuildWeather();
            BuildVolumes();
            AssetDatabase.SaveAssets();
            Debug.Log("Kazakh Ninja: effects are up to date.");
        }

        // ---------- Materials ----------

        static Material ParticleMaterial(string path, float intensity, float additive) => Upsert(path, "Kazakh Ninja/FX Particle", m =>
        {
            m.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(FxArt.AtlasPath));
            m.SetFloat("_Intensity", intensity);
            m.SetFloat("_Additive", additive);
        });

        static Material Glow => ParticleMaterial(GlowMaterialPath, 2.6f, 1f);
        static Material SoftGlow => ParticleMaterial(SoftGlowMaterialPath, 1.1f, 1f);
        static Material Solid => ParticleMaterial(SolidMaterialPath, 1f, 0f);

        static Material Splat => Upsert(SplatMaterialPath, "Kazakh Ninja/Splat", m =>
        {
            m.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(FxArt.SplatAtlasPath));
            m.SetVector("_Grid", new Vector4(4f, 2f, 0f, 0f));
        });

        /// <summary>Creates or updates a material with <paramref name="shaderName"/>, keeping its GUID.</summary>
        static Material Upsert(string path, string shaderName, Action<Material> setup)
        {
            ContentBuilder.EnsureFolder(Path.GetDirectoryName(path));
            Shader shader = Shader.Find(shaderName);
            if (shader == null) throw new InvalidOperationException($"Shader '{shaderName}' not found; is {ShaderFolder} imported?");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            setup(material);
            EditorUtility.SetDirty(material);
            return material;
        }

        // ---------- The FX library ----------

        static void BuildLibrary()
        {
            var root = new GameObject("FX");
            var library = root.AddComponent<FxLibrary>();
            Material glow = Glow, solid = Solid;

            ParticleSystem slash = CreateSystem(root, "Slash", glow, 32, 11, ps =>
            {
                ParticleSystem.MainModule main = ps.main;
                main.startSize3D = true;
                SizeOverLifetime3D(ps, AnimationCurve.EaseInOut(0f, 0.75f, 1f, 1.15f), new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f)));
                FadeOut(ps, 0.4f);
            });
            ParticleSystem droplets = CreateSystem(root, "Droplets", solid, 500, 5, ps =>
            {
                Gravity(ps, 1.2f);
                Frames(ps, FxArt.Droplet, FxArt.Droplet);
                Stretch(ps, 0.035f, 1.1f);
                SizeOverLifetime(ps, AnimationCurve.Linear(0f, 1f, 1f, 0.35f));
                FadeOut(ps, 0.75f);
            });
            ParticleSystem mist = CreateSystem(root, "Mist", solid, 60, 2, ps =>
            {
                Frames(ps, FxArt.SoftDot, FxArt.SoftDot);
                SizeOverLifetime(ps, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 1.5f));
                FadeOut(ps, 0f);
            });
            ParticleSystem crumbs = CreateSystem(root, "Crumbs", solid, 400, 4, ps =>
            {
                Gravity(ps, 1.5f);
                Frames(ps, FxArt.CrumbFirst, FxArt.CrumbLast);
                Spin(ps, 540f);
                FadeOut(ps, 0.8f);
            });
            ParticleSystem powder = CreateSystem(root, "Powder", solid, 120, 3, ps =>
            {
                Gravity(ps, -0.03f);
                Frames(ps, FxArt.Puff, FxArt.Puff);
                Drag(ps, 2.5f);
                Spin(ps, 60f);
                SizeOverLifetime(ps, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 1.8f));
                FadeOut(ps, 0.1f);
            });
            ParticleSystem festive = CreateSystem(root, "Festive", solid, 300, 6, ps =>
            {
                Gravity(ps, 0.7f);
                Frames(ps, FxArt.Coin, FxArt.Candy);
                Drag(ps, 0.8f);
                Spin(ps, 420f);
                FadeOut(ps, 0.8f);
            });
            ParticleSystem glints = CreateSystem(root, "Glints", glow, 200, 9, ps =>
            {
                Frames(ps, FxArt.Star, FxArt.Star);
                SizeOverLifetime(ps, new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.25f, 1f), new Keyframe(1f, 0f)));
                Spin(ps, 90f);
            });
            ParticleSystem sparks = CreateSystem(root, "Sparks", glow, 300, 8, ps =>
            {
                Gravity(ps, 0.5f);
                Frames(ps, FxArt.HotDot, FxArt.HotDot);
                Stretch(ps, 0.05f, 1f);
                Drag(ps, 1.5f);
                ColorOverLifetime(ps, Color.white, new Color(1f, 0.9f, 0.7f), new Color(1f, 0.6f, 0.3f));
            });
            ParticleSystem embers = CreateSystem(root, "Embers", glow, 250, 7, ps =>
            {
                Gravity(ps, -0.15f);
                Frames(ps, FxArt.HotDot, FxArt.HotDot);
                Drag(ps, 1.2f);
                ColorOverLifetime(ps, new Color(1f, 1f, 0.8f), new Color(1f, 0.6f, 0.2f), new Color(0.8f, 0.15f, 0.05f));
                Flicker(ps);
            });
            ParticleSystem ornaments = CreateSystem(root, "Ornaments", glow, 100, 9, ps =>
            {
                Frames(ps, FxArt.Ornament, FxArt.Ornament);
                Drag(ps, 3f);
                SizeOverLifetime(ps, new AnimationCurve(new Keyframe(0f, 0.2f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0.6f)));
                FadeOut(ps, 0.5f);
            });
            ParticleSystem rings = CreateSystem(root, "Rings", glow, 30, 10, ps =>
            {
                Frames(ps, FxArt.Ring, FxArt.Ring);
                SizeOverLifetime(ps, AnimationCurve.EaseInOut(0f, 0.15f, 1f, 1f));
                FadeOut(ps, 0f);
            });
            ParticleSystem fire = CreateSystem(root, "Fire", glow, 60, 1, ps =>
            {
                Frames(ps, FxArt.Puff, FxArt.Puff);
                Drag(ps, 3f);
                Spin(ps, 90f);
                SizeOverLifetime(ps, AnimationCurve.EaseInOut(0f, 0.5f, 1f, 1.3f));
                ColorOverLifetime(ps, new Color(1f, 0.95f, 0.7f), new Color(1f, 0.55f, 0.15f), new Color(0.45f, 0.08f, 0.03f));
            });
            ParticleSystem smoke = CreateSystem(root, "Smoke", solid, 60, 0, ps =>
            {
                Gravity(ps, -0.05f);
                Frames(ps, FxArt.Puff, FxArt.Puff);
                Drag(ps, 1.5f);
                Spin(ps, 40f);
                SizeOverLifetime(ps, AnimationCurve.EaseInOut(0f, 0.5f, 1f, 1.6f));
                FadeOut(ps, 0.2f);
            });
            ParticleSystem stains = CreateSystem(root, "Stains", Splat, 64, -10, ps =>
            {
                ParticleSystem.MainModule main = ps.main;
                main.startSize3D = true;
                var renderer = ps.GetComponent<ParticleSystemRenderer>();
                renderer.alignment = ParticleSystemRenderSpace.World;
                renderer.sortMode = ParticleSystemSortMode.YoungestInFront;
                renderer.SetActiveVertexStreams(new List<ParticleSystemVertexStream>
                {
                    ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Color,
                    ParticleSystemVertexStream.UV, ParticleSystemVertexStream.AgePercent,
                });
            });

            Wire(library, ("slash", slash), ("droplets", droplets), ("mist", mist), ("crumbs", crumbs), ("powder", powder),
                ("festive", festive), ("glints", glints), ("sparks", sparks), ("embers", embers), ("ornaments", ornaments),
                ("rings", rings), ("fire", fire), ("smoke", smoke), ("stains", stains));
            SavePrefab(root, FxLibraryPath);
        }

        /// <summary>
        /// The sparkles that fly with special food (the fuse, the golden glitter, the power-up glows)
        /// are drawn from the FX atlas with the glowing material, so they bloom. Their emitter shape follows the
        /// food's scale (the spawner flies food bigger than its model), while the sparks keep their own size.
        /// </summary>
        public static void UpgradeAttachment(string path, bool sparks)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) return;
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            if (root.TryGetComponent(out ParticleSystem ps))
            {
                root.GetComponent<ParticleSystemRenderer>().sharedMaterial = Glow;
                ParticleSystem.MainModule main = ps.main;
                main.scalingMode = ParticleSystemScalingMode.Shape;
                if (sparks)
                {
                    Frames(ps, FxArt.HotDot, FxArt.HotDot);
                    Stretch(ps, 0.04f, 1f);
                }
                else
                {
                    Frames(ps, FxArt.Star, FxArt.Star);
                    Spin(ps, 120f);
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            PrefabUtility.UnloadPrefabContents(root);
        }

        // ---------- Blades ----------

        /// <summary>The look of one blade: trail style and colours, particles and cut burst.</summary>
        internal sealed class BladeLook
        {
            public string id;
            public BladeStyle style;
            public (Color color, float time)[] colors;
            public Color core = new(2.4f, 2.3f, 2.1f);
            public Color accent = Color.white;
            public float coreWidth = 0.22f, glow = 1.4f, additive = 0.7f, patternScale = 6f, scroll = 2f;
            public float width = 0.36f, time = 0.15f;
            public BladeCutEffect cut;
            public Color flash, effect;
            /// <summary>Optional particles that stream off the blade (built by <see cref="BladeEmitter"/>).</summary>
            public Func<GameObject, ParticleSystem> emitter;
        }

        internal static readonly BladeLook[] BladeLooks =
        {
            new()
            {
                id = "pyshak", style = BladeStyle.Plain,
                colors = new[] { (new Color(1f, 0.95f, 0.78f), 0f), (new Color(0.996f, 0.773f, 0.047f), 0.3f), (new Color(0f, 0.686f, 0.792f), 1f) },
                cut = BladeCutEffect.Glints, flash = new Color(1f, 0.85f, 0.45f), effect = new Color(1f, 0.9f, 0.6f),
            },
            new()
            {
                id = "kylysh", style = BladeStyle.Steel, core = new Color(3.2f, 3.3f, 3.6f), accent = new Color(1.6f, 1.8f, 2.2f), scroll = 3f,
                colors = new[] { (Color.white, 0f), (new Color(0.72f, 0.84f, 1f), 0.35f), (new Color(0.32f, 0.42f, 0.78f), 1f) },
                width = 0.34f, time = 0.15f, coreWidth = 0.18f,
                cut = BladeCutEffect.Sparks, flash = new Color(0.8f, 0.9f, 1f), effect = new Color(1f, 0.75f, 0.4f),
            },
            new()
            {
                id = "tulpar", style = BladeStyle.Wind, accent = new Color(0.8f, 1f, 1f), patternScale = 5f, scroll = 3.5f, additive = 0.85f,
                colors = new[] { (new Color(0.95f, 1f, 1f), 0f), (new Color(0.55f, 0.9f, 0.95f), 0.4f), (new Color(0.2f, 0.55f, 0.7f), 1f) },
                width = 0.48f, time = 0.24f, glow = 1.2f,
                cut = BladeCutEffect.Wind, flash = new Color(0.75f, 1f, 1f), effect = new Color(0.7f, 0.95f, 1f),
                emitter = parent => BladeEmitter(parent, "Wisps", SoftGlow, FxArt.SoftDot, FxArt.SoftDot, 7f, new Vector2(0.25f, 0.4f),
                    new Vector2(0.1f, 0.18f), new Color(0.75f, 0.95f, 1f, 0.55f), ps =>
                    {
                        Stretch(ps, 0.02f, 3f);
                        ParticleSystem.InheritVelocityModule inherit = ps.inheritVelocity;
                        inherit.enabled = true;
                        inherit.mode = ParticleSystemInheritVelocityMode.Initial;
                        inherit.curveMultiplier = 0.35f;
                        FadeOut(ps, 0.2f);
                    }),
            },
            new()
            {
                id = "shashu", style = BladeStyle.Plain, core = new Color(2.8f, 2.4f, 1.6f),
                colors = new[] { (new Color(1f, 0.93f, 0.6f), 0f), (new Color(1f, 0.72f, 0.2f), 0.35f), (new Color(0.86f, 0.25f, 0.5f), 1f) },
                width = 0.4f, time = 0.17f,
                cut = BladeCutEffect.Coins, flash = new Color(1f, 0.82f, 0.35f), effect = new Color(1f, 0.82f, 0.3f),
                emitter = parent => BladeEmitter(parent, "Coins", Solid, FxArt.Coin, FxArt.Candy, 2.8f, new Vector2(0.8f, 1.1f),
                    new Vector2(0.13f, 0.2f), Color.white, ps =>
                    {
                        ParticleSystem.MainModule main = ps.main;
                        main.gravityModifierMultiplier = 0.9f;
                        main.startColor = new ParticleSystem.MinMaxGradient(FestiveGradient()) { mode = ParticleSystemGradientMode.RandomColor };
                        Spin(ps, 360f);
                        FadeOut(ps, 0.8f);
                    }),
            },
            new()
            {
                id = "oyu", style = BladeStyle.Ornament, accent = new Color(1.6f, 1.2f, 0.4f), patternScale = 7f, scroll = 1.5f,
                core = new Color(2.6f, 2.2f, 1.5f), additive = 0.55f,
                colors = new[] { (new Color(1f, 0.55f, 0.45f), 0f), (new Color(0.85f, 0.16f, 0.14f), 0.3f), (new Color(0.35f, 0.04f, 0.06f), 1f) },
                width = 0.44f, time = 0.2f,
                cut = BladeCutEffect.Ornaments, flash = new Color(1f, 0.7f, 0.35f), effect = new Color(1f, 0.78f, 0.3f),
                emitter = parent => BladeEmitter(parent, "Ornaments", Glow, FxArt.Ornament, FxArt.Ornament, 1.8f, new Vector2(0.4f, 0.55f),
                    new Vector2(0.24f, 0.34f), new Color(1f, 0.78f, 0.3f), ps =>
                    {
                        SizeOverLifetime(ps, new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0f)));
                        ParticleSystem.MainModule main = ps.main;
                        main.startRotation = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);
                    }),
            },
            new()
            {
                id = "naizagai", style = BladeStyle.Lightning, core = new Color(3.5f, 3.8f, 5f), accent = new Color(0.7f, 0.6f, 1.6f),
                colors = new[] { (new Color(0.9f, 0.95f, 1f), 0f), (new Color(0.35f, 0.6f, 1f), 0.35f), (new Color(0.5f, 0.2f, 0.85f), 1f) },
                width = 0.5f, time = 0.16f, coreWidth = 0.14f, glow = 1.1f,
                cut = BladeCutEffect.Lightning, flash = new Color(0.7f, 0.8f, 1f), effect = new Color(0.65f, 0.8f, 1f),
                emitter = parent => BladeEmitter(parent, "Arcs", Glow, FxArt.HotDot, FxArt.HotDot, 5f, new Vector2(0.12f, 0.25f),
                    new Vector2(0.05f, 0.09f), new Color(0.7f, 0.85f, 1f), ps =>
                    {
                        ParticleSystem.MainModule main = ps.main;
                        main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f);
                        ParticleSystem.ShapeModule shape = ps.shape;
                        shape.enabled = true;
                        shape.shapeType = ParticleSystemShapeType.Sphere;
                        shape.radius = 0.05f;
                        Stretch(ps, 0.05f, 1f);
                    }),
            },
            new()
            {
                id = "altyn_semser", style = BladeStyle.Fire, core = new Color(3.2f, 2.6f, 1.4f), accent = new Color(1.4f, 0.3f, 0.05f),
                patternScale = 4f, scroll = 3f, additive = 0.8f, glow = 1.7f,
                colors = new[] { (new Color(1f, 1f, 0.85f), 0f), (new Color(1f, 0.8f, 0.2f), 0.3f), (new Color(1f, 0.35f, 0.05f), 0.75f), (new Color(0.7f, 0.05f, 0.05f), 1f) },
                width = 0.5f, time = 0.22f,
                cut = BladeCutEffect.Embers, flash = new Color(1f, 0.65f, 0.25f), effect = new Color(1f, 0.6f, 0.15f),
                emitter = parent => BladeEmitter(parent, "Embers", Glow, FxArt.HotDot, FxArt.HotDot, 6f, new Vector2(0.5f, 0.9f),
                    new Vector2(0.05f, 0.1f), new Color(1f, 0.7f, 0.25f), ps =>
                    {
                        ParticleSystem.MainModule main = ps.main;
                        main.gravityModifierMultiplier = -0.25f;
                        main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.2f);
                        ParticleSystem.ShapeModule shape = ps.shape;
                        shape.enabled = true;
                        shape.shapeType = ParticleSystemShapeType.Sphere;
                        shape.radius = 0.08f;
                        ColorOverLifetime(ps, new Color(1f, 1f, 0.8f), new Color(1f, 0.55f, 0.15f), new Color(0.7f, 0.1f, 0.03f));
                        Flicker(ps);
                    }),
            },
        };

        /// <summary>Updates the visual fields of a blade (material, colours, particles, swatch) from its look.</summary>
        public static void ApplyLook(BladeDefinition blade, bool repaint)
        {
            BladeLook look = Array.Find(BladeLooks, l => l.id == blade.id);
            if (look == null) return;

            var gradient = new Gradient();
            var colorKeys = new GradientColorKey[look.colors.Length];
            for (int i = 0; i < look.colors.Length; i++) colorKeys[i] = new GradientColorKey(look.colors[i].color, look.colors[i].time);
            gradient.SetKeys(colorKeys, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.9f, 0.35f), new GradientAlphaKey(0f, 1f) });

            blade.trailMaterial = Upsert($"{MaterialFolder}/Blade_{look.id}.mat", "Kazakh Ninja/Blade Trail", m =>
            {
                foreach (string keyword in new[] { "_STYLE_PLAIN", "_STYLE_STEEL", "_STYLE_WIND", "_STYLE_ORNAMENT", "_STYLE_LIGHTNING", "_STYLE_FIRE" })
                    m.DisableKeyword(keyword);
                m.EnableKeyword("_STYLE_" + look.style.ToString().ToUpperInvariant());
                m.SetFloat("_Style", (int)look.style);
                m.SetTexture("_NoiseTex", AssetDatabase.LoadAssetAtPath<Texture2D>(FxArt.NoisePath));
                m.SetColor("_CoreColor", look.core);
                m.SetColor("_AccentColor", look.accent);
                m.SetFloat("_CoreWidth", look.coreWidth);
                m.SetFloat("_GlowIntensity", look.glow);
                m.SetFloat("_Additive", look.additive);
                m.SetFloat("_PatternScale", look.patternScale);
                m.SetFloat("_ScrollSpeed", look.scroll);
                m.renderQueue = 3100; // over the food's particles
            });
            blade.trailColors = gradient;
            blade.width = look.width;
            blade.trailTime = look.time;
            blade.cutEffect = look.cut;
            blade.flashColor = look.flash;
            blade.effectColor = look.effect;
            blade.emitterPrefab = null;
            if (look.emitter != null)
            {
                var root = new GameObject($"Blade_{look.id}");
                ParticleSystem system = look.emitter(root);
                string path = $"{BladePrefabFolder}/Blade_{look.id}.prefab";
                ContentBuilder.EnsureFolder(BladePrefabFolder);
                PrefabUtility.SaveAsPrefabAsset(system.gameObject, path);
                Object.DestroyImmediate(root);
                blade.emitterPrefab = AssetDatabase.LoadAssetAtPath<ParticleSystem>(path);
            }
            BladeStyle swatchStyle = look.cut == BladeCutEffect.Coins ? BladeStyle.Coins : look.style;
            blade.preview = FxArt.BladeSwatch(look.id, gradient, swatchStyle, look.accent.maxColorComponent > 1f ? look.accent / look.accent.maxColorComponent : look.accent, repaint);
            EditorUtility.SetDirty(blade);
        }

        /// <summary>A world-space system that emits by distance while the blade moves (enabled only while cutting).</summary>
        static ParticleSystem BladeEmitter(GameObject parent, string name, Material material, int firstFrame, int lastFrame,
            float perUnit, Vector2 lifetime, Vector2 size, Color color, Action<ParticleSystem> configure)
        {
            ParticleSystem ps = CreateSystem(parent, name, material, 200, 12, _ => { });
            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime.x, lifetime.y);
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.startColor = color;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.enabled = false;
            emission.rateOverDistance = perUnit;
            Frames(ps, firstFrame, lastFrame);
            configure(ps);
            return ps;
        }

        static Gradient FestiveGradient()
        {
            var gradient = new Gradient();
            gradient.SetKeys(new[]
            {
                new GradientColorKey(new Color(1f, 0.8f, 0.25f), 0f), new GradientColorKey(new Color(0.92f, 0.25f, 0.33f), 0.25f),
                new GradientColorKey(new Color(1f, 0.85f, 0.3f), 0.5f), new GradientColorKey(new Color(0.25f, 0.72f, 0.45f), 0.75f),
                new GradientColorKey(new Color(0.3f, 0.6f, 0.95f), 1f),
            }, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return gradient;
        }

        // ---------- Backdrop scenes ----------

        /// <summary>Look and light of one scene of the day.</summary>
        sealed class SceneLook
        {
            public string id, kk, ru;
            public Color light, ambient, rim, reflectTop, reflectBottom;
            public float intensity;
            public Func<ParticleSystem> ambientParticles;
        }

        /// <summary>The day in a round: steppe at sunset, the mountains at dusk, Astana at night.</summary>
        static SceneLook[] SceneLooks => new[]
        {
            new SceneLook
            {
                id = "dala", kk = "Дала", ru = "Степь",
                light = new Color(1f, 0.82f, 0.6f), intensity = 1.25f, ambient = new Color(0.55f, 0.45f, 0.45f),
                rim = new Color(1f, 0.62f, 0.35f), reflectTop = new Color(1f, 0.72f, 0.5f), reflectBottom = new Color(0.35f, 0.25f, 0.15f),
                ambientParticles = () => Ambient("dala", "Fluff", SoftGlow, FxArt.SoftDot, 4f, new Vector2(7f, 10f), new Vector2(0.04f, 0.09f),
                    new Color(1f, 0.85f, 0.55f, 0.8f), new Vector3(-2f, -1f, 4.5f), new Vector3(16f, 7f, 0.1f), new Vector3(0.3f, 0.08f, 0f)),
            },
            new SceneLook
            {
                id = "alatau", kk = "Алатау", ru = "Горы Алатау",
                light = new Color(0.92f, 0.86f, 1f), intensity = 1.05f, ambient = new Color(0.45f, 0.42f, 0.55f),
                rim = new Color(0.95f, 0.65f, 0.85f), reflectTop = new Color(0.72f, 0.62f, 0.92f), reflectBottom = new Color(0.12f, 0.14f, 0.2f),
                ambientParticles = () => Ambient("alatau", "Snow", Solid, FxArt.Snowflake, 16f, new Vector2(7f, 10f), new Vector2(0.06f, 0.14f),
                    new Color(1f, 1f, 1f, 0.75f), new Vector3(0f, 7.5f, 4.5f), new Vector3(24f, 0.2f, 0.1f), new Vector3(0.25f, -1f, 0f)),
            },
            new SceneLook
            {
                id = "baiterek", kk = "Бәйтерек", ru = "Байтерек",
                light = new Color(0.85f, 0.9f, 1f), intensity = 0.95f, ambient = new Color(0.36f, 0.38f, 0.55f),
                rim = new Color(1f, 0.78f, 0.4f), reflectTop = new Color(0.38f, 0.42f, 0.72f), reflectBottom = new Color(0.28f, 0.2f, 0.12f),
                ambientParticles = () => Ambient("baiterek", "Twinkles", Glow, FxArt.Star, 5f, new Vector2(1.5f, 2.5f), new Vector2(0.08f, 0.16f),
                    new Color(1f, 0.9f, 0.7f, 0.9f), new Vector3(0f, 3f, 4.5f), new Vector3(24f, 8f, 0.1f), Vector3.zero),
            },
        };

        /// <summary>Returns the day's scenes, in order, with their paintings, light and particles.</summary>
        public static BackgroundDefinition[] BuildScenes()
        {
            ContentBuilder.EnsureFolder(BackgroundDataFolder);
            var scenes = new List<BackgroundDefinition>();
            foreach (SceneLook look in SceneLooks)
            {
                string path = $"{BackgroundDataFolder}/Background_{look.id}.asset";
                // Older versions kept these in the collection folder, as unlockable backgrounds.
                string old = $"{Root}/Data/Collection/Background_{look.id}.asset";
                if (AssetDatabase.LoadAssetAtPath<BackgroundDefinition>(path) == null && AssetDatabase.LoadAssetAtPath<BackgroundDefinition>(old) != null)
                    AssetDatabase.MoveAsset(old, path);
                BackgroundDefinition scene = ContentBuilder.LoadOrCreate(path, ScriptableObject.CreateInstance<BackgroundDefinition>);
                scene.id = look.id;
                scene.nameKk = look.kk;
                scene.nameRu = look.ru;
                scene.picture = BackgroundArt.Picture(look.id);
                scene.lightColor = look.light;
                scene.lightIntensity = look.intensity;
                scene.ambientColor = look.ambient;
                scene.rimColor = look.rim;
                scene.reflectTop = look.reflectTop;
                scene.reflectBottom = look.reflectBottom;
                scene.ambientPrefab = look.ambientParticles();
                EditorUtility.SetDirty(scene);
                scenes.Add(scene);
            }
            return scenes.ToArray();
        }

        /// <summary>The backdrop material, showing the first scene so the editor view looks right.</summary>
        public static Material BackdropMaterial(BackgroundDefinition first) => Upsert(BackdropMaterialPath, "Kazakh Ninja/Parallax Background", m =>
        {
            m.SetTexture("_PictureA", first.picture);
            m.SetTexture("_NoiseTex", AssetDatabase.LoadAssetAtPath<Texture2D>(FxArt.NoisePath));
            m.SetVector("_Offset", Vector4.zero);
        });

        /// <summary>Looping atmosphere in a box volume in front of the backdrop (dust, fluff, snow, twinkles).</summary>
        static ParticleSystem Ambient(string id, string name, Material material, int frame, float rate, Vector2 lifetime, Vector2 size,
            Color color, Vector3 center, Vector3 box, Vector3 velocity)
        {
            var root = new GameObject($"Ambient_{id}");
            ParticleSystem ps = CreateSystem(root, name, material, 400, -5, _ => { });
            ps.transform.localPosition = center;
            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.prewarm = true;
            main.duration = 5f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime.x, lifetime.y);
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.startColor = color;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = rate;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = box;
            ParticleSystem.VelocityOverLifetimeModule drift = ps.velocityOverLifetime;
            drift.enabled = true;
            drift.space = ParticleSystemSimulationSpace.World;
            drift.x = new ParticleSystem.MinMaxCurve(velocity.x - 0.1f, velocity.x + 0.1f);
            drift.y = new ParticleSystem.MinMaxCurve(velocity.y - 0.1f, velocity.y + 0.1f);
            drift.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            Frames(ps, frame, frame);
            Spin(ps, 30f);
            // Fade in and out, so nothing pops.
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
            ParticleSystem.ColorOverLifetimeModule fade = ps.colorOverLifetime;
            fade.enabled = true;
            fade.color = gradient;
            if (frame == FxArt.Star)
                SizeOverLifetime(ps, new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.5f, 1f), new Keyframe(1f, 0f)));

            string path = $"{AmbientPrefabFolder}/Ambient_{id}.prefab";
            ContentBuilder.EnsureFolder(AmbientPrefabFolder);
            PrefabUtility.SaveAsPrefabAsset(ps.gameObject, path);
            Object.DestroyImmediate(root);
            return AssetDatabase.LoadAssetAtPath<ParticleSystem>(path);
        }

        // ---------- Power-up weather ----------

        const string ShashuRainPath = PrefabFolder + "/ShashuRain.prefab";
        const string PetalWindPath = PrefabFolder + "/PetalWind.prefab";

        static void BuildWeather()
        {
            var rain = new GameObject("ShashuRain");
            ParticleSystem ps = CreateSystem(rain, "Rain", Solid, 300, 6, _ => { });
            Weather(ps, FxArt.Coin, FxArt.Candy, 22f, new Vector2(2.5f, 3.5f), new Vector2(0.18f, 0.28f), new Vector3(0f, -3f, 0f));
            ParticleSystem.MainModule main = ps.main;
            main.startColor = new ParticleSystem.MinMaxGradient(FestiveGradient()) { mode = ParticleSystemGradientMode.RandomColor };
            SavePrefab(ps.gameObject, ShashuRainPath);
            Object.DestroyImmediate(rain);

            var wind = new GameObject("PetalWind");
            ps = CreateSystem(wind, "Petals", Solid, 300, 6, _ => { });
            Weather(ps, FxArt.Petal, FxArt.Petal, 20f, new Vector2(3f, 4.5f), new Vector2(0.14f, 0.22f), new Vector3(6f, 0.4f, 0f));
            main = ps.main;
            var petals = new Gradient();
            petals.SetKeys(new[]
            {
                new GradientColorKey(new Color(1f, 0.85f, 0.9f), 0f), new GradientColorKey(new Color(1f, 0.96f, 0.96f), 0.5f),
                new GradientColorKey(new Color(0.55f, 0.85f, 0.4f), 1f),
            }, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            main.startColor = new ParticleSystem.MinMaxGradient(petals) { mode = ParticleSystemGradientMode.RandomColor };
            SavePrefab(ps.gameObject, PetalWindPath);
            Object.DestroyImmediate(wind);
        }

        static void Weather(ParticleSystem ps, int firstFrame, int lastFrame, float rate, Vector2 lifetime, Vector2 size, Vector3 velocity)
        {
            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime.x, lifetime.y);
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startSpeed = 0f;
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.enabled = false;
            emission.rateOverTime = rate;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(1f, 1f, 0.1f);
            ParticleSystem.VelocityOverLifetimeModule move = ps.velocityOverLifetime;
            move.enabled = true;
            move.space = ParticleSystemSimulationSpace.World;
            move.x = new ParticleSystem.MinMaxCurve(velocity.x * 0.8f, velocity.x * 1.2f + 0.3f);
            move.y = new ParticleSystem.MinMaxCurve(velocity.y * 1.2f - 0.3f, velocity.y * 0.8f + 0.3f);
            move.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            Frames(ps, firstFrame, lastFrame);
            Spin(ps, 300f);
            FadeOut(ps, 0.85f);
        }

        // ---------- Post-processing ----------

        const string BaseProfilePath = VolumeFolder + "/Base.asset";
        const string SlowProfilePath = VolumeFolder + "/SlowTime.asset";
        const string ImpactProfilePath = VolumeFolder + "/Impact.asset";
        const string DangerProfilePath = VolumeFolder + "/Danger.asset";

        static void BuildVolumes()
        {
            Profile(BaseProfilePath, profile =>
            {
                Bloom bloom = Override<Bloom>(profile);
                bloom.threshold.Override(1f);
                bloom.intensity.Override(0.9f);
                bloom.scatter.Override(0.62f);
                bloom.clamp.Override(24f);
                bloom.highQualityFiltering.Override(false);
                // The cheap path for phones: dual filtering from a quarter-size image.
                bloom.filter.Override(BloomFilterMode.Dual);
                bloom.downscale.Override(BloomDownscaleMode.Quarter);
                bloom.maxIterations.Override(5);
                Override<Tonemapping>(profile).mode.Override(TonemappingMode.Neutral);
                Vignette vignette = Override<Vignette>(profile);
                vignette.intensity.Override(0.28f);
                vignette.smoothness.Override(0.45f);
                vignette.color.Override(new Color(0.1f, 0.05f, 0.03f));
                ColorAdjustments color = Override<ColorAdjustments>(profile);
                color.contrast.Override(8f);
                color.saturation.Override(10f);
            });
            // The overlays only use effects that live in URP's post shader without keywords of their own (no chromatic
            // aberration): blending one in must not switch the shader to another variant, which stalls a phone the
            // first time — that was the hitch on the first pepper and the first Қымыз.
            Profile(SlowProfilePath, profile =>
            {
                ColorAdjustments color = Override<ColorAdjustments>(profile);
                color.saturation.Override(-25f);
                color.colorFilter.Override(new Color(0.84f, 0.95f, 1f));
                Vignette vignette = Override<Vignette>(profile);
                vignette.intensity.Override(0.42f);
                vignette.smoothness.Override(0.5f);
                vignette.color.Override(new Color(0.05f, 0.22f, 0.3f));
                Remove<ChromaticAberration>(profile);
            });
            Profile(ImpactProfilePath, profile =>
            {
                Vignette vignette = Override<Vignette>(profile);
                vignette.intensity.Override(0.5f);
                vignette.smoothness.Override(0.4f);
                vignette.color.Override(new Color(0.5f, 0.05f, 0.02f));
                Remove<ChromaticAberration>(profile);
                Override<ColorAdjustments>(profile).postExposure.Override(0.35f);
            });
            Profile(DangerProfilePath, profile =>
            {
                Vignette vignette = Override<Vignette>(profile);
                vignette.intensity.Override(0.45f);
                vignette.smoothness.Override(0.35f);
                vignette.color.Override(new Color(0.45f, 0.02f, 0.02f));
            });
        }

        static void Profile(string path, Action<VolumeProfile> setup)
        {
            ContentBuilder.EnsureFolder(VolumeFolder);
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }
            setup(profile);
            EditorUtility.SetDirty(profile);
        }

        /// <summary>The profile's component of type <typeparamref name="T"/>, added (as a sub-asset) if missing.</summary>
        static T Override<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet(out T component)) return component;
            component = profile.Add<T>(false);
            component.name = typeof(T).Name;
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }

        /// <summary>Takes the component of type <typeparamref name="T"/> (and its sub-asset) out of the profile, if it is there.</summary>
        static void Remove<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (!profile.TryGet(out T component)) return;
            profile.Remove<T>();
            Object.DestroyImmediate(component, true);
        }

        // ---------- Scene ----------

        /// <summary>
        /// Wires the effects into the open Game scene: the camera rig (for the intro's dolly and screen shake),
        /// post-processing, the backdrop director and scenes, the screen overlays and the FX library.
        /// </summary>
        public static void WireScene(GameManager manager, SliceEffects effects, BladeController blade)
        {
            Camera camera = Camera.main;
            if (camera == null) throw new InvalidOperationException("The Game scene has no main camera.");

            // Camera rig: the rig moves (intro), the camera only shakes relative to it.
            Transform rig = camera.transform.parent;
            if (rig == null || rig.name != "CameraRig")
            {
                rig = new GameObject("CameraRig").transform;
                rig.SetPositionAndRotation(camera.transform.position, camera.transform.rotation);
                camera.transform.SetParent(rig, true);
            }
            camera.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            ContentBuilder.GetOrAdd<CameraShake>(camera.gameObject);
            UniversalAdditionalCameraData cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.None;
            EditorUtility.SetDirty(cameraData);

            // Volumes: the base look, and the overlays at weight 0.
            GameObject post = ContentBuilder.FindOrCreate("PostProcessing");
            Volume Volume(string name, string profilePath, float priority)
            {
                Transform child = post.transform.Find(name);
                GameObject go = child ? child.gameObject : new GameObject(name);
                go.transform.SetParent(post.transform, false);
                Volume volume = ContentBuilder.GetOrAdd<Volume>(go);
                volume.isGlobal = true;
                volume.priority = priority;
                volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
                volume.weight = priority == 0f ? 1f : 0f;
                EditorUtility.SetDirty(volume);
                return volume;
            }
            Volume("Base", BaseProfilePath, 0f);
            Volume slow = Volume("SlowTime", SlowProfilePath, 1f);
            Volume impact = Volume("Impact", ImpactProfilePath, 3f);
            Volume danger = Volume("Danger", DangerProfilePath, 2f);

            // Screen overlays and power-up weather.
            ScreenFx screenFx = ContentBuilder.GetOrAdd<ScreenFx>(manager.gameObject);
            ParticleSystem rain = Instance(post.transform, ShashuRainPath);
            ParticleSystem petals = Instance(post.transform, PetalWindPath);
            Set(screenFx, ("game", manager), ("gameCamera", camera), ("slowTime", slow), ("impact", impact), ("danger", danger),
                ("shashuRain", rain), ("petalWind", petals));

            // The backdrop: the old quad gets the parallax material and a director.
            GameObject backdrop = GameObject.Find("Background");
            if (backdrop == null)
            {
                backdrop = GameObject.CreatePrimitive(PrimitiveType.Quad);
                backdrop.name = "Background";
                Object.DestroyImmediate(backdrop.GetComponent<Collider>());
                backdrop.transform.position = new Vector3(0f, 0f, 6f);
            }
            backdrop.transform.localScale = new Vector3(33f, 15f, 1f);
            var backdropRenderer = backdrop.GetComponent<MeshRenderer>();
            backdropRenderer.shadowCastingMode = ShadowCastingMode.Off;
            backdropRenderer.receiveShadows = false;
            BackgroundDefinition[] scenes = BuildScenes();
            backdropRenderer.sharedMaterial = BackdropMaterial(scenes[0]);
            BackgroundDirector director = ContentBuilder.GetOrAdd<BackgroundDirector>(backdrop);
            Light light = Object.FindFirstObjectByType<Light>();
            Set(director, ("game", manager), ("gameCamera", camera), ("backdrop", backdropRenderer), ("mainLight", light));
            var serialized = new SerializedObject(director);
            SerializedProperty list = serialized.FindProperty("scenes");
            list.arraySize = scenes.Length;
            for (int i = 0; i < scenes.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = scenes[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();

            // Slices: the shared FX library and the blade whose skin adds its own burst.
            Set(effects, ("gameCamera", camera), ("fxPrefab", AssetDatabase.LoadAssetAtPath<FxLibrary>(FxLibraryPath)), ("blade", blade));

            // The trail draws over every particle.
            var trail = blade.GetComponent<TrailRenderer>();
            trail.textureMode = LineTextureMode.Stretch;
            trail.alignment = LineAlignment.View;
            trail.sortingOrder = 20;
            EditorUtility.SetDirty(trail);
        }

        static ParticleSystem Instance(Transform parent, string prefabPath)
        {
            string name = Path.GetFileNameWithoutExtension(prefabPath);
            Transform existing = parent.Find(name);
            if (existing) Object.DestroyImmediate(existing.gameObject);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = name;
            return instance.GetComponent<ParticleSystem>();
        }

        // ---------- Particle helpers ----------

        /// <summary>A world-space system driven by Emit (no emission of its own), with the atlas material.</summary>
        static ParticleSystem CreateSystem(GameObject parent, string name, Material material, int maxParticles, int sortingOrder, Action<ParticleSystem> configure)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSpeed = 0f;
            main.startLifetime = 1f;
            main.startColor = Color.white;
            main.maxParticles = maxParticles;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 0f;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = false;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.sortingOrder = sortingOrder;
            renderer.maxParticleSize = 2f;

            configure(ps);
            return ps;
        }

        /// <summary>Picks a random atlas frame between <paramref name="first"/> and <paramref name="last"/> for each particle.</summary>
        static void Frames(ParticleSystem ps, int first, int last)
        {
            ParticleSystem.TextureSheetAnimationModule sheet = ps.textureSheetAnimation;
            sheet.enabled = true;
            sheet.mode = ParticleSystemAnimationMode.Grid;
            sheet.numTilesX = FxArt.AtlasColumns;
            sheet.numTilesY = FxArt.AtlasColumns;
            sheet.animation = ParticleSystemAnimationType.WholeSheet;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f);
            // Frames are normalised: 0..1 over the whole sheet.
            const float frames = FxArt.AtlasColumns * FxArt.AtlasColumns;
            sheet.startFrame = first == last
                ? new ParticleSystem.MinMaxCurve((first + 0.5f) / frames)
                : new ParticleSystem.MinMaxCurve(first / frames, (last + 0.999f) / frames);
            sheet.cycleCount = 1;
        }

        static void Gravity(ParticleSystem ps, float multiplier)
        {
            ParticleSystem.MainModule main = ps.main;
            main.gravityModifierMultiplier = multiplier;
        }

        static void Stretch(ParticleSystem ps, float velocityScale, float lengthScale)
        {
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = velocityScale;
            renderer.lengthScale = lengthScale;
        }

        static void SizeOverLifetime(ParticleSystem ps, AnimationCurve curve)
        {
            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, curve);
        }

        static void SizeOverLifetime3D(ParticleSystem ps, AnimationCurve x, AnimationCurve y)
        {
            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.separateAxes = true;
            size.x = new ParticleSystem.MinMaxCurve(1f, x);
            size.y = new ParticleSystem.MinMaxCurve(1f, y);
            size.z = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Constant(0f, 1f, 1f));
        }

        /// <summary>Full opacity until <paramref name="start"/> (fraction of the lifetime), then fading to nothing.</summary>
        static void FadeOut(ParticleSystem ps, float start)
        {
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, Mathf.Clamp01(start)), new GradientAlphaKey(0f, 1f) });
            ParticleSystem.ColorOverLifetimeModule color = ps.colorOverLifetime;
            color.enabled = true;
            color.color = gradient;
        }

        static void ColorOverLifetime(ParticleSystem ps, Color start, Color middle, Color end)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(start, 0f), new GradientColorKey(middle, 0.4f), new GradientColorKey(end, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.55f), new GradientAlphaKey(0f, 1f) });
            ParticleSystem.ColorOverLifetimeModule color = ps.colorOverLifetime;
            color.enabled = true;
            color.color = gradient;
        }

        static void Spin(ParticleSystem ps, float degreesPerSecond)
        {
            ParticleSystem.RotationOverLifetimeModule rotation = ps.rotationOverLifetime;
            rotation.enabled = true;
            float radians = degreesPerSecond * Mathf.Deg2Rad;
            rotation.z = new ParticleSystem.MinMaxCurve(-radians, radians);
        }

        static void Drag(ParticleSystem ps, float amount)
        {
            ParticleSystem.LimitVelocityOverLifetimeModule limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.limit = 100f;
            limit.drag = amount;
            limit.multiplyDragByParticleSize = false;
            limit.multiplyDragByParticleVelocity = false;
        }

        /// <summary>Embers flicker as they cool.</summary>
        static void Flicker(ParticleSystem ps)
        {
            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            var curve = new AnimationCurve();
            for (int i = 0; i <= 8; i++) curve.AddKey(i / 8f, (i % 2 == 0 ? 1f : 0.55f) * (1f - i / 10f));
            size.size = new ParticleSystem.MinMaxCurve(1f, curve);
        }

        // ---------- Wiring helpers ----------

        static void Wire(Object target, params (string field, Object value)[] references)
        {
            var serialized = new SerializedObject(target);
            foreach ((string field, Object value) in references)
            {
                SerializedProperty property = serialized.FindProperty(field);
                if (property == null) throw new InvalidOperationException($"{target.GetType().Name} has no serialized field '{field}'.");
                property.objectReferenceValue = value;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Assigns references (overwriting: this builder owns these fields).</summary>
        static void Set(Object target, params (string field, Object value)[] references)
        {
            Wire(target, references);
            EditorUtility.SetDirty(target);
        }

        static void SavePrefab(GameObject go, string path)
        {
            ContentBuilder.EnsureFolder(Path.GetDirectoryName(path));
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
        }
    }
}
