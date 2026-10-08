using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace KazakhNinja.EditorTools
{
    /// <summary>
    /// Generates the placeholder content (meshes, textures, materials, food definitions, effect prefabs)
    /// and builds the Game scene. Existing assets and scene references are never overwritten,
    /// so hand-tuned data survives a re-run, and re-running adds whatever is missing.
    /// </summary>
    public static class ContentBuilder
    {
        const string Root = "Assets/_Game";
        const string MeshFolder = Root + "/Meshes/Placeholders";
        const string TextureFolder = Root + "/Textures";
        const string MaterialFolder = Root + "/Materials";
        const string FoodMaterialFolder = MaterialFolder + "/Foods";
        const string FoodDataFolder = Root + "/Data/Foods";
        const string PrefabFolder = Root + "/Prefabs";
        const string EffectPrefabFolder = PrefabFolder + "/Effects";
        const string DatabasePath = Root + "/Data/FoodDatabase.asset";
        const string FoodPrefabPath = PrefabFolder + "/FoodItem.prefab";
        const string BladeMaterialPath = MaterialFolder + "/Blade.mat";
        const string BackgroundMaterialPath = MaterialFolder + "/Background.mat";
        const string ParticleMaterialPath = MaterialFolder + "/Particle.mat";
        const string SoftDotPath = TextureFolder + "/SoftDot.png";
        const string FuseSparksPath = EffectPrefabFolder + "/FuseSparks.prefab";
        const string GoldGlitterPath = EffectPrefabFolder + "/GoldGlitter.prefab";
        const string KymyzGlowPath = EffectPrefabFolder + "/KymyzGlow.prefab";
        const string ToyGlowPath = EffectPrefabFolder + "/ToyGlow.prefab";
        const string NauryzGlowPath = EffectPrefabFolder + "/NauryzGlow.prefab";
        const string ModeFolder = Root + "/Data/Modes";
        const string CollectionFolder = Root + "/Data/Collection";
        const string CollectionDatabasePath = CollectionFolder + "/CollectionDatabase.asset";
        const string ClassicModePath = ModeFolder + "/Mode_Classic.asset";
        const string ArcadeModePath = ModeFolder + "/Mode_Arcade.asset";
        const string GameScenePath = Root + "/Scenes/Game.unity";
        const string SfxFolder = Root + "/SFX";
        const string OpenerPath = SfxFolder + "/SFX_Opener.mp3";
        const string MenuMusicPath = SfxFolder + "/SFX_MainMenu_Ambient.mp3";
        const string GameMusicPath = SfxFolder + "/SFX_Ingame.mp3";
        /// <summary>Every clip in the SFX folder whose name starts with this is a slice sound.</summary>
        const string SliceClipPrefix = "SFX_Slice";

        sealed class FoodSpec
        {
            public string id, kk, ru, en, fact;
            public Func<Mesh> mesh;
            public Color skin, inside;
            public int points = 1;
            public Vector3 scale = Vector3.one;
        }

        static readonly FoodSpec[] Foods =
        {
            new() { id = "shuzhuk", kk = "Шұжық", ru = "Шужук", en = "Shuzhuk",
                fact = "Домашняя колбаса из конины с чесноком и специями; её заготавливают зимой, во время соғыма.",
                mesh = () => PlaceholderMeshes.Capsule(0.2f, 1.5f), skin = Rgb(0.42f, 0.13f, 0.10f), inside = Rgb(0.62f, 0.20f, 0.17f) },
            new() { id = "kazy", kk = "Қазы", ru = "Казы", en = "Kazy",
                fact = "Рёберное мясо конины вместе с жиром, уложенное в кишку, — самый почётный деликатес дастархана.",
                mesh = () => PlaceholderMeshes.Capsule(0.3f, 1.3f), skin = Rgb(0.50f, 0.28f, 0.20f), inside = Rgb(0.96f, 0.84f, 0.80f) },
            new() { id = "kurt", kk = "Құрт", ru = "Курт", en = "Kurt",
                fact = "Сушёные шарики из солёного кислого молока. Хранятся месяцами — их брали в дальнюю дорогу.",
                mesh = () => PlaceholderMeshes.Sphere(0.3f), skin = Rgb(0.93f, 0.91f, 0.83f), inside = Rgb(1f, 0.99f, 0.95f),
                points = 2, scale = new Vector3(1f, 0.9f, 1f) },
            new() { id = "baursak", kk = "Бауырсақ", ru = "Баурсак", en = "Baursak",
                fact = "Пышки из дрожжевого теста, жаренные в масле. Без горы бауырсаков не обходится ни один той.",
                mesh = () => PlaceholderMeshes.Sphere(0.45f), skin = Rgb(0.83f, 0.52f, 0.18f), inside = Rgb(1f, 0.90f, 0.66f),
                scale = new Vector3(1f, 0.85f, 0.95f) },
            new() { id = "aport", kk = "Апорт", ru = "Апорт", en = "Aport apple",
                fact = "Знаменитый алматинский сорт. Казахстан считают родиной яблок: дикая яблоня Сиверса до сих пор растёт в горах Алатау.",
                mesh = () => PlaceholderMeshes.Sphere(0.55f), skin = Rgb(0.78f, 0.10f, 0.12f), inside = Rgb(0.97f, 0.95f, 0.78f),
                scale = new Vector3(1f, 0.92f, 1f) },
            new() { id = "shelpek", kk = "Шелпек", ru = "Шелпек", en = "Shelpek",
                fact = "Тонкие лепёшки, жаренные в масле. По традиции их пекут по пятницам и в дни поминовения.",
                mesh = () => PlaceholderMeshes.Disc(0.62f, 0.1f), skin = Rgb(0.92f, 0.76f, 0.46f), inside = Rgb(1f, 0.94f, 0.80f) },
            new() { id = "samsa", kk = "Самса", ru = "Самса", en = "Samsa",
                fact = "Треугольные пирожки с рубленым мясом и луком; самые вкусные пекут в тандыре.",
                mesh = () => PlaceholderMeshes.Prism(0.6f, 0.3f), skin = Rgb(0.80f, 0.50f, 0.20f), inside = Rgb(0.50f, 0.28f, 0.18f) },
            new() { id = "irimshik", kk = "Ірімшік", ru = "Иримшик", en = "Irimshik",
                fact = "Сладковатый сушёный творожный сыр из молока, долго томлённого на огне.",
                mesh = () => PlaceholderMeshes.Box(new Vector3(0.65f, 0.55f, 0.55f)), skin = Rgb(0.92f, 0.78f, 0.52f), inside = Rgb(0.99f, 0.91f, 0.72f) },
            new() { id = "zhent", kk = "Жент", ru = "Жент", en = "Zhent",
                fact = "Сладость из толчёного жареного проса (тары) с маслом, сахаром и изюмом.",
                mesh = () => PlaceholderMeshes.Box(new Vector3(0.95f, 0.42f, 0.42f)), skin = Rgb(0.70f, 0.45f, 0.22f), inside = Rgb(0.86f, 0.66f, 0.40f) },
            new() { id = "karta", kk = "Қарта", ru = "Карта", en = "Karta",
                fact = "Часть конской кишки, вывернутая жиром наружу и сваренная; подаётся к бешбармаку вместе с қазы.",
                mesh = () => PlaceholderMeshes.Torus(0.42f, 0.17f), skin = Rgb(0.40f, 0.26f, 0.20f), inside = Rgb(0.95f, 0.88f, 0.82f),
                points = 2 },
        };

        [MenuItem("Kazakh Ninja/Setup Everything", priority = 0)]
        public static void SetupEverything()
        {
            FxBuilder.Build(false);
            BuildPlaceholderContent();
            ApplyMobilePlayerSettings();
            BuildGameScene();
            UiBuilder.BuildUi();
        }

        [MenuItem("Kazakh Ninja/Build Placeholder Content", priority = 20)]
        public static void BuildPlaceholderContent()
        {
            foreach (string folder in new[] { MeshFolder, TextureFolder, FoodMaterialFolder, FoodDataFolder, EffectPrefabFolder, ModeFolder })
                EnsureFolder(folder);

            var definitions = new List<FoodDefinition>();
            foreach (FoodSpec spec in Foods)
            {
                Mesh mesh = LoadOrCreateMesh(spec.id, spec.mesh);
                Material skin = LoadOrCreate($"{FoodMaterialFolder}/{spec.id}_Skin.mat", () => LitMaterial(spec.skin, 0.4f));
                Material inside = LoadOrCreate($"{FoodMaterialFolder}/{spec.id}_Inside.mat", () => LitMaterial(spec.inside, 0.15f));
                definitions.Add(LoadOrCreate($"{FoodDataFolder}/Food_{spec.id}.asset", () => NewFood(spec.id, spec.kk, spec.ru, spec.en,
                    spec.fact, mesh, new[] { skin }, inside, spec.scale, spec.inside, spec.points)));
            }

            BuildTextures();
            Material particleMaterial = LoadOrCreate(ParticleMaterialPath, () =>
                new Material(Shader.Find("Sprites/Default")) { mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(SoftDotPath) });
            BuildEffectPrefabs(particleMaterial);

            FoodDefinition bomb = BuildBomb();
            FoodDefinition golden = BuildGoldenAport();
            FoodDefinition[] powerUps = BuildPowerUps();
            BuildModes();

            FoodDatabase database = LoadOrCreate(DatabasePath, ScriptableObject.CreateInstance<FoodDatabase>);
            FoodDefinition[] merged = database.foods.Where(f => f != null).Union(definitions).ToArray();
            if (merged.Length != database.foods.Length) database.foods = merged;
            if (database.bomb == null) database.bomb = bomb;
            if (database.golden == null) database.golden = golden;
            if (database.powerUps == null || database.powerUps.Length == 0) database.powerUps = powerUps;
            EditorUtility.SetDirty(database);
            foreach (FoodDefinition food in database.foods.Append(database.bomb).Append(database.golden).Concat(database.powerUps ?? new FoodDefinition[0]))
                ApplySliceLook(food);

            BuildCollection(database);
            IconBaker.BakeMissing();

            LoadOrCreate(BladeMaterialPath, () => new Material(Shader.Find("Sprites/Default")) { name = "Blade" });
            LoadOrCreate(BackgroundMaterialPath, () => UnlitMaterial(Rgb(0.23f, 0.14f, 0.10f)));

            if (AssetDatabase.LoadAssetAtPath<GameObject>(FoodPrefabPath) == null)
            {
                var go = new GameObject("FoodItem", typeof(MeshFilter), typeof(MeshRenderer), typeof(Sliceable));
                var renderer = go.GetComponent<MeshRenderer>();
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                SavePrefab(go, FoodPrefabPath);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"Kazakh Ninja: placeholder content ready ({definitions.Count} foods, bomb, golden aport, {powerUps.Length} power-ups).");
        }

        static FoodDefinition BuildBomb()
        {
            Mesh mesh = LoadOrCreateMesh("bomb_pepper", PlaceholderMeshes.Pepper);
            Material body = LoadOrCreate($"{FoodMaterialFolder}/bomb_pepper_Skin.mat", () =>
            {
                Material m = LitMaterial(Rgb(0.75f, 0.05f, 0.04f), 0.85f);
                SetEmission(m, new Color(0.25f, 0.02f, 0f));
                return m;
            });
            Material stem = LoadOrCreate($"{FoodMaterialFolder}/bomb_pepper_Stem.mat", () => LitMaterial(Rgb(0.20f, 0.45f, 0.12f), 0.3f));
            return LoadOrCreate($"{FoodDataFolder}/Food_bomb_pepper.asset", () =>
            {
                FoodDefinition food = NewFood("bomb_pepper", "Ащы бұрыш", "Жгучий перец", "Hot pepper",
                    "Бомба! Не режь — обожжёшься.", mesh, new[] { body, stem }, null, Vector3.one, Rgb(1f, 0.45f, 0.1f), 0);
                food.kind = FoodKind.Bomb;
                food.spawnWeight = 0f;
                food.attachmentPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FuseSparksPath);
                return food;
            });
        }

        static FoodDefinition BuildGoldenAport()
        {
            Mesh mesh = LoadOrCreateMesh("golden_aport", () => PlaceholderMeshes.Sphere(0.55f));
            Material gold = LoadOrCreate($"{FoodMaterialFolder}/golden_aport_Skin.mat", () =>
            {
                Material m = LitMaterial(Rgb(1f, 0.78f, 0.22f), 0.8f);
                m.SetFloat("_Metallic", 0.9f);
                SetEmission(m, new Color(0.35f, 0.24f, 0.04f));
                return m;
            });
            Material inside = AssetDatabase.LoadAssetAtPath<Material>($"{FoodMaterialFolder}/aport_Inside.mat");
            return LoadOrCreate($"{FoodDataFolder}/Food_golden_aport.asset", () =>
            {
                FoodDefinition food = NewFood("golden_aport", "Алтын апорт", "Золотой апорт", "Golden aport",
                    "Бей, пока висит: каждый удар — очко, а последний расколет его пополам.", mesh, new[] { gold }, inside,
                    new Vector3(1f, 0.92f, 1f), Rgb(1f, 0.85f, 0.35f), 1);
                food.kind = FoodKind.Golden;
                food.spawnWeight = 0f;
                food.maxHits = 10;
                food.holdDuration = 2.2f;
                food.attachmentPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GoldGlitterPath);
                return food;
            });
        }

        static FoodDefinition[] BuildPowerUps()
        {
            return new[]
            {
                BuildPowerUp("kymyz", "Қымыз", "Кумыс", "Kymyz",
                    "Кисломолочный напиток из кобыльего молока; его заквашивают и взбивают в кожаном торсыке.",
                    PlaceholderMeshes.Torsyk, new[] { (Rgb(0.55f, 0.36f, 0.2f), 0.3f) }, Rgb(0.97f, 0.96f, 0.92f),
                    PowerUpType.SlowTime, 4f, KymyzGlowPath),
                BuildPowerUp("toy", "Той", "Той", "Toi feast",
                    "Той — праздник: гостей осыпают шашу — конфетами и монетами на счастье.",
                    () => PlaceholderMeshes.Disc(0.42f, 0.12f), new[] { (Rgb(1f, 0.8f, 0.25f), 0.75f) }, Rgb(1f, 0.9f, 0.5f),
                    PowerUpType.DoubleScore, 6f, ToyGlowPath),
                BuildPowerUp("nauryz", "Наурыз көже", "Наурыз-коже", "Nauryz kozhe",
                    "Праздничный суп из семи ингредиентов, который варят на Наурыз — весенний Новый год 22 марта.",
                    PlaceholderMeshes.Bowl, new[] { (Rgb(0.88f, 0.92f, 1f), 0.8f), (Rgb(0.93f, 0.87f, 0.7f), 0.2f) }, Rgb(0.95f, 0.9f, 0.75f),
                    PowerUpType.Frenzy, 5f, NauryzGlowPath),
            };
        }

        static FoodDefinition BuildPowerUp(string id, string kk, string ru, string en, string fact, Func<Mesh> createMesh,
            (Color color, float smoothness)[] skins, Color inside, PowerUpType type, float duration, string glowPath)
        {
            Mesh mesh = LoadOrCreateMesh(id, createMesh);
            var skinMaterials = new Material[skins.Length];
            for (int i = 0; i < skins.Length; i++)
            {
                (Color color, float smoothness) = skins[i];
                string suffix = i == 0 ? "Skin" : $"Skin{i + 1}";
                skinMaterials[i] = LoadOrCreate($"{FoodMaterialFolder}/{id}_{suffix}.mat", () => LitMaterial(color, smoothness));
            }
            Material insideMaterial = LoadOrCreate($"{FoodMaterialFolder}/{id}_Inside.mat", () => LitMaterial(inside, 0.15f));
            return LoadOrCreate($"{FoodDataFolder}/Food_{id}.asset", () =>
            {
                FoodDefinition food = NewFood(id, kk, ru, en, fact, mesh, skinMaterials, insideMaterial, Vector3.one, inside, 1);
                food.kind = FoodKind.PowerUp;
                food.spawnWeight = 0f;
                food.powerUp = type;
                food.powerUpDuration = duration;
                food.attachmentPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(glowPath);
                return food;
            });
        }

        static void BuildModes()
        {
            SetBackgroundTimes(LoadOrCreate(ClassicModePath, () =>
            {
                var mode = ScriptableObject.CreateInstance<GameModeDefinition>();
                mode.type = GameModeType.Classic;
                mode.rules = new GameRules { lives = 3, bomb = BombEffect.CostsLife };
                // Gets busier over three minutes: shorter gaps, bigger waves, more bombs.
                mode.waveInterval = new AnimationCurve(new Keyframe(0f, 2.2f), new Keyframe(60f, 1.5f), new Keyframe(180f, 1.0f));
                mode.foodPerWave = new AnimationCurve(new Keyframe(0f, 2f), new Keyframe(60f, 4f), new Keyframe(180f, 6f));
                mode.bombChance = new AnimationCurve(new Keyframe(0f, 0.1f), new Keyframe(60f, 0.3f), new Keyframe(180f, 0.5f));
                mode.goldenChance = 0.03f;
                mode.powerUpChance = 0f;
                return mode;
            }), 50f, 120f);
            SetBackgroundTimes(LoadOrCreate(ArcadeModePath, () =>
            {
                var mode = ScriptableObject.CreateInstance<GameModeDefinition>();
                mode.type = GameModeType.Arcade;
                mode.rules = new GameRules { duration = 60f, bomb = BombEffect.CostsPoints, bombPenalty = 10, powerUpsEnabled = true };
                mode.waveInterval = new AnimationCurve(new Keyframe(0f, 1.5f), new Keyframe(60f, 1.05f));
                mode.foodPerWave = new AnimationCurve(new Keyframe(0f, 3f), new Keyframe(60f, 5f));
                mode.bombChance = AnimationCurve.Constant(0f, 60f, 0.25f);
                mode.goldenChance = 0.06f;
                mode.powerUpChance = 0.22f;
                // Busy but readable on a phone, even when Наурыз throws food in from the sides.
                mode.maxInFlight = 10;
                return mode;
            }), 20f, 40f);
        }

        /// <summary>Evening falls as the round gets harder: the backdrop moves on at these times (only set once).</summary>
        static void SetBackgroundTimes(GameModeDefinition mode, params float[] times)
        {
            if (mode.backgroundTimes != null && mode.backgroundTimes.Length > 0) return;
            mode.backgroundTimes = times;
            EditorUtility.SetDirty(mode);
        }

        // ---------- How food comes apart ----------

        /// <summary>
        /// What flies out of each food when it is cut, and in which colours: juice from fruit, meat and drinks,
        /// crumbs from pastry and dried cheese, a puff of powder from құрт, sweets and coins from the той.
        /// </summary>
        static void ApplySliceLook(FoodDefinition food)
        {
            if (food == null) return;
            (SliceStyle style, int main, int second) = food.id switch
            {
                "aport" => (SliceStyle.Juicy, 0xFFD85C, 0xDC1E24),
                "golden_aport" => (SliceStyle.Juicy, 0xFFCC33, 0xFFF2A0),
                "shuzhuk" => (SliceStyle.Juicy, 0xC7211E, 0xFFE0B8),
                "kazy" => (SliceStyle.Juicy, 0xD9332A, 0xFFEDD6),
                "karta" => (SliceStyle.Juicy, 0xFFDBA8, 0x9A5238),
                "kymyz" => (SliceStyle.Juicy, 0xFFFCF0, 0xD8ECFF),
                "nauryz" => (SliceStyle.Juicy, 0xFAEAC4, 0x8CD158),
                "kurt" => (SliceStyle.Powdery, 0xFFFFF4, 0xEDE6CC),
                "irimshik" => (SliceStyle.Crumbly, 0xFFCC73, 0xD98033),
                "baursak" => (SliceStyle.Crumbly, 0xF29E38, 0xFFEBB2),
                "shelpek" => (SliceStyle.Crumbly, 0xFFCC6B, 0xFFF2D1),
                "samsa" => (SliceStyle.Crumbly, 0xF5A847, 0x9E4D2E),
                "zhent" => (SliceStyle.Crumbly, 0xD18F4D, 0x66291F),
                "toy" => (SliceStyle.Festive, 0xFFD140, 0xF24D8C),
                "bomb_pepper" => (SliceStyle.Juicy, 0xFF5A1A, 0x4D9926),
                _ => (food.sliceStyle, -1, -1),
            };
            if (main < 0) return;
            food.sliceStyle = style;
            food.juiceColor = Hex(main);
            food.juiceColor2 = Hex(second);
            EditorUtility.SetDirty(food);
        }

        static Color Hex(int rgb) => new(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);

        // ---------- Дастархан collection ----------

        static CollectionDatabase BuildCollection(FoodDatabase foods)
        {
            EnsureFolder(CollectionFolder);

            // In display order; each opens with an achievement (see ContentTexts).
            BladeDefinition[] blades =
            {
                BuildBlade("pyshak", "Пышақ", "Нож"),
                BuildBlade("kylysh", "Қылыш", "Сабля"),
                BuildBlade("tulpar", "Тұлпар", "Тулпар"),
                BuildBlade("shashu", "Шашу", "Шашу"),
                BuildBlade("oyu", "Ою-өрнек", "Орнамент"),
                BuildBlade("naizagai", "Найзағай", "Молния"),
                BuildBlade("altyn_semser", "Алтын семсер", "Золотой меч"),
            };

            CollectionDatabase database = LoadOrCreate(CollectionDatabasePath, () =>
            {
                var cards = new List<FoodDefinition>(foods.foods.Where(f => f != null));
                if (foods.golden != null) cards.Add(foods.golden);
                cards.AddRange(foods.powerUps.Where(f => f != null));
                var created = ScriptableObject.CreateInstance<CollectionDatabase>();
                created.cards = cards.ToArray();
                return created;
            });
            database.blades = blades;
            EditorUtility.SetDirty(database);
            return database;
        }

        /// <summary>A blade from the collection; its look (trail, particles, swatch) comes from <see cref="FxBuilder.BladeLooks"/>.</summary>
        static BladeDefinition BuildBlade(string id, string kk, string ru)
        {
            BladeDefinition blade = LoadOrCreate($"{CollectionFolder}/Blade_{id}.asset", () =>
            {
                var created = ScriptableObject.CreateInstance<BladeDefinition>();
                created.id = id;
                return created;
            });
            blade.nameKk = kk;
            blade.nameRu = ru;
            blade.unlock = ContentTexts.Unlock(id);
            FxBuilder.ApplyLook(blade, false);
            return blade;
        }

        static FoodDefinition NewFood(string id, string kk, string ru, string en, string fact, Mesh mesh,
            Material[] skins, Material inside, Vector3 scale, Color juice, int points)
        {
            var food = ScriptableObject.CreateInstance<FoodDefinition>();
            food.id = id;
            food.nameKk = kk;
            food.nameRu = ru;
            food.nameEn = en;
            food.factRu = fact;
            food.mesh = mesh;
            food.skinMaterials = skins;
            food.insideMaterial = inside;
            food.scale = scale;
            food.juiceColor = juice;
            food.points = points;
            return food;
        }

        // ---------- Textures ----------

        static void BuildTextures()
        {
            if (!File.Exists(SoftDotPath))
            {
                WritePng(SoftDotPath, 64, (x, y) =>
                {
                    float r = Mathf.Clamp01(new Vector2(x, y).magnitude);
                    return Mathf.SmoothStep(1f, 0f, r);
                });
            }

            ConfigureTexture(SoftDotPath, false);
        }

        /// <param name="alpha">Alpha for coordinates in [-1, 1].</param>
        static void WritePng(string path, int size, Func<float, float, float> alpha)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha(u, v)) * 255f));
                }
            }
            texture.SetPixels32(pixels);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
        }

        /// <summary>Applies the import settings; reimports only when something differs.</summary>
        static void ConfigureTexture(string path, bool sprite)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            var wanted = sprite ? TextureImporterType.Sprite : TextureImporterType.Default;
            if (importer.textureType == wanted && importer.alphaIsTransparency
                && (!sprite || importer.spriteImportMode == SpriteImportMode.Single)) return;

            importer.textureType = sprite ? TextureImporterType.Sprite : TextureImporterType.Default;
            if (sprite)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 128;
            }
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = !sprite;
            importer.SaveAndReimport();
        }

        // ---------- Effects ----------

        static void BuildEffectPrefabs(Material particleMaterial)
        {
            CreateParticlePrefabIfMissing(FuseSparksPath, particleMaterial, ps =>
            {
                ps.transform.localPosition = new Vector3(0f, 0.66f, 0f);
                ParticleSystem.MainModule main = ps.main;
                main.loop = true;
                main.playOnAwake = true;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.1f);
                main.gravityModifier = 0.4f;
                ParticleSystem.EmissionModule emission = ps.emission;
                emission.rateOverTime = 45f;
                ParticleSystem.ShapeModule shape = Shape(ps, ParticleSystemShapeType.Cone, 0.02f);
                shape.angle = 35f;
                shape.rotation = new Vector3(-90f, 0f, 0f); // cones emit along +Z; point up the stem
                ColorOverLifetime(ps, new Color(1f, 1f, 0.6f), new Color(1f, 0.6f, 0.1f), new Color(0.8f, 0.1f, 0f));
            });

            CreateGlitterPrefabIfMissing(GoldGlitterPath, particleMaterial, new Color(1f, 0.85f, 0.3f), Color.white, 0.5f);
            CreateGlitterPrefabIfMissing(KymyzGlowPath, particleMaterial, new Color(0.4f, 0.8f, 1f), Color.white, 0.5f);
            CreateGlitterPrefabIfMissing(ToyGlowPath, particleMaterial, new Color(1f, 0.35f, 0.75f), new Color(1f, 0.85f, 0.3f), 0.45f);
            CreateGlitterPrefabIfMissing(NauryzGlowPath, particleMaterial, new Color(0.45f, 1f, 0.45f), new Color(1f, 1f, 0.6f), 0.5f);

            // They glow through the bloom with the shared effects material.
            FxBuilder.UpgradeAttachment(FuseSparksPath, true);
            foreach (string path in new[] { GoldGlitterPath, KymyzGlowPath, ToyGlowPath, NauryzGlowPath })
                FxBuilder.UpgradeAttachment(path, false);
        }

        /// <summary>Slow sparkles around a special food so it reads as "not ordinary".</summary>
        static void CreateGlitterPrefabIfMissing(string path, Material material, Color a, Color b, float radius)
        {
            CreateParticlePrefabIfMissing(path, material, ps =>
            {
                ParticleSystem.MainModule main = ps.main;
                main.loop = true;
                main.playOnAwake = true;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.4f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
                main.startColor = new ParticleSystem.MinMaxGradient(a, b);
                ParticleSystem.EmissionModule emission = ps.emission;
                emission.rateOverTime = 14f;
                ParticleSystem.ShapeModule shape = Shape(ps, ParticleSystemShapeType.Sphere, radius);
                shape.radiusThickness = 0f;
                ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0f)));
            });
        }

        static void CreateParticlePrefabIfMissing(string path, Material material, Action<ParticleSystem> configure)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
            var go = new GameObject(Path.GetFileNameWithoutExtension(path));
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = ps.main;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = Color.white;
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 0f;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            configure(ps);
            SavePrefab(go, path);
        }

        static void Burst(ParticleSystem ps, short min, short max)
        {
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, min, max) });
        }

        static ParticleSystem.ShapeModule Shape(ParticleSystem ps, ParticleSystemShapeType type, float radius)
        {
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = type;
            shape.radius = radius;
            return shape;
        }

        static void FadeSize(ParticleSystem ps)
        {
            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
        }

        static void ColorOverLifetime(ParticleSystem ps, Color start, Color middle, Color end)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(start, 0f), new GradientColorKey(middle, 0.4f), new GradientColorKey(end, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            ParticleSystem.ColorOverLifetimeModule color = ps.colorOverLifetime;
            color.enabled = true;
            color.color = gradient;
        }

        // ---------- Player settings ----------

        [MenuItem("Kazakh Ninja/Apply Mobile Player Settings", priority = 21)]
        public static void ApplyMobilePlayerSettings()
        {
            PlayerSettings.productName = UiText.GameName;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            // Unity 6 lets every plan hide the engine splash: the game opens on its own intro instead,
            // from the same near-black it fades in from.
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SplashScreen.showUnityLogo = false;
            PlayerSettings.SplashScreen.backgroundColor = new Color(0.07f, 0.04f, 0.03f);
            Debug.Log("Kazakh Ninja: landscape orientation applied, engine splash off.");
        }

        // ---------- Scene ----------

        [MenuItem("Kazakh Ninja/Build Game Scene", priority = 22)]
        public static void BuildGameScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != GameScenePath)
            {
                if (File.Exists(GameScenePath)) scene = EditorSceneManager.OpenScene(GameScenePath);
                else
                {
                    EnsureFolder(Path.GetDirectoryName(GameScenePath));
                    scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    CreateStage();
                }
            }

            Camera camera = Camera.main;
            GameObject game = FindOrCreate("Game");
            var spawner = GetOrAdd<FoodSpawner>(game);
            var sliceSystem = GetOrAdd<SliceSystem>(game);
            var effects = GetOrAdd<SliceEffects>(game);
            var manager = GetOrAdd<GameManager>(game);
            // The old debug HUD script is gone; drop its dangling component.
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(game);
            SetMissingReferences(manager,
                ("spawner", spawner),
                ("classicMode", AssetDatabase.LoadAssetAtPath<GameModeDefinition>(ClassicModePath)),
                ("arcadeMode", AssetDatabase.LoadAssetAtPath<GameModeDefinition>(ArcadeModePath)));
            // Rounds are started by the GameManager now.
            var spawnerSettings = new SerializedObject(spawner);
            spawnerSettings.FindProperty("spawnOnStart").boolValue = false;
            spawnerSettings.ApplyModifiedPropertiesWithoutUndo();
            SetMissingReferences(spawner,
                ("database", AssetDatabase.LoadAssetAtPath<FoodDatabase>(DatabasePath)),
                ("foodPrefab", AssetDatabase.LoadAssetAtPath<Sliceable>(FoodPrefabPath)),
                ("gameCamera", camera));
            SetMissingReferences(sliceSystem, ("gameCamera", camera));

            BuildAudio(manager);

            GameObject bladeGo = GameObject.Find("Blade");
            if (bladeGo == null)
            {
                bladeGo = new GameObject("Blade");
                ConfigureTrail(bladeGo.AddComponent<TrailRenderer>(), AssetDatabase.LoadAssetAtPath<Material>(BladeMaterialPath));
            }
            var blade = GetOrAdd<BladeController>(bladeGo);
            SetMissingReferences(blade, ("gameCamera", camera), ("sliceSystem", sliceSystem), ("trail", bladeGo.GetComponent<TrailRenderer>()));

            var collection = GetOrAdd<CollectionManager>(game);
            SetMissingReferences(collection,
                ("game", manager),
                ("database", AssetDatabase.LoadAssetAtPath<CollectionDatabase>(CollectionDatabasePath)),
                ("blade", blade));
            FxBuilder.WireScene(manager, effects, blade);

            EditorSceneManager.SaveScene(scene, GameScenePath);
            if (EditorBuildSettings.scenes.All(s => s.path != GameScenePath))
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(GameScenePath, true) };
            Debug.Log($"Kazakh Ninja: {GameScenePath} is up to date.");
        }

        static void BuildAudio(GameManager manager)
        {
            var audio = GetOrAdd<AudioManager>(FindOrCreate("Audio"));
            SetMissingReferences(audio,
                ("game", manager),
                ("opener", AssetDatabase.LoadAssetAtPath<AudioClip>(OpenerPath)),
                ("menuMusic", AssetDatabase.LoadAssetAtPath<AudioClip>(MenuMusicPath)),
                ("gameMusic", AssetDatabase.LoadAssetAtPath<AudioClip>(GameMusicPath)));

            var serialized = new SerializedObject(audio);
            SerializedProperty slices = serialized.FindProperty("sliceClips");
            if (slices.arraySize > 0 || !AssetDatabase.IsValidFolder(SfxFolder)) return;
            AudioClip[] clips = AssetDatabase.FindAssets("t:AudioClip", new[] { SfxFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => Path.GetFileName(path).StartsWith(SliceClipPrefix))
                .OrderBy(path => path)
                .Select(AssetDatabase.LoadAssetAtPath<AudioClip>)
                .ToArray();
            slices.arraySize = clips.Length;
            for (int i = 0; i < clips.Length; i++) slices.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Camera, light and backdrop of a brand-new scene.</summary>
        static void CreateStage()
        {
            var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
            cameraGo.transform.position = new Vector3(0f, 0f, -15f);
            var camera = cameraGo.AddComponent<Camera>();
            camera.fieldOfView = 36f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 60f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Rgb(0.12f, 0.07f, 0.05f);
            cameraGo.AddComponent<AudioListener>();

            var lightGo = new GameObject("Directional Light");
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.color = new Color(1f, 0.96f, 0.9f);
            light.shadows = LightShadows.None;

            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.5f, 0.46f, 0.42f);

            var background = GameObject.CreatePrimitive(PrimitiveType.Quad);
            background.name = "Background";
            Object.DestroyImmediate(background.GetComponent<Collider>());
            background.transform.position = new Vector3(0f, 0f, 6f);
            background.transform.localScale = new Vector3(30f, 15f, 1f);
            var backgroundRenderer = background.GetComponent<MeshRenderer>();
            backgroundRenderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(BackgroundMaterialPath);
            backgroundRenderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        static void ConfigureTrail(TrailRenderer trail, Material material)
        {
            trail.sharedMaterial = material;
            trail.time = 0.14f;
            trail.minVertexDistance = 0.04f;
            trail.widthMultiplier = 0.32f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
            trail.numCapVertices = 4;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.emitting = false;
            // Gold head fading into the sky blue of the Kazakh flag.
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.95f, 0.75f), 0f),
                    new GradientColorKey(new Color(0.996f, 0.773f, 0.047f), 0.3f),
                    new GradientColorKey(new Color(0f, 0.686f, 0.792f), 1f),
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = gradient;
        }

        // ---------- Helpers ----------

        internal static GameObject FindOrCreate(string name) => GameObject.Find(name) ?? new GameObject(name);

        internal static T GetOrAdd<T>(GameObject go) where T : Component =>
            go.TryGetComponent(out T component) ? component : go.AddComponent<T>();

        /// <summary>Assigns only references that are still empty, so hand-made wiring is kept.</summary>
        internal static void SetMissingReferences(Object target, params (string field, Object value)[] references)
        {
            var serialized = new SerializedObject(target);
            foreach ((string field, Object value) in references)
            {
                SerializedProperty property = serialized.FindProperty(field);
                if (property == null)
                {
                    Debug.LogError($"Kazakh Ninja: {target.GetType().Name} has no serialized field '{field}'.");
                    continue;
                }
                if (property.objectReferenceValue == null) property.objectReferenceValue = value;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static Mesh LoadOrCreateMesh(string id, Func<Mesh> create) => LoadOrCreate($"{MeshFolder}/{id}.asset", () =>
        {
            Mesh mesh = create();
            mesh.name = id;
            return mesh;
        });

        internal static T LoadOrCreate<T>(string path, Func<T> create) where T : Object
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            T asset = create();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static void SavePrefab(GameObject go, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
        }

        static Material LitMaterial(Color color, float smoothness)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            return material;
        }

        static void SetEmission(Material material, Color color)
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }

        static Material UnlitMaterial(Color color)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            material.SetColor("_BaseColor", color);
            return material;
        }

        internal static Color Rgb(float r, float g, float b) => new(r, g, b, 1f);

        internal static void EnsureFolder(string path)
        {
            path = path.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
