using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace KazakhNinja.EditorTools
{
    /// <summary>
    /// TextMesh Pro fonts for the menu look: Cormorant Garamond (serif, the name and titles) and Montserrat
    /// (the small second-language lines and running text), both OFL, in UI/Fonts.
    /// The atlases are baked once with Latin, Cyrillic with the Kazakh letters, and the punctuation the game uses,
    /// so no glyph is drawn during play; anything else falls back to LiberationSans.
    /// Delete a font asset to bake it again.
    /// </summary>
    static class UiFonts
    {
        const string Folder = "Assets/_Game/UI/Fonts";
        const string FallbackPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

        public const string TitlePath = Folder + "/CormorantGaramond-Medium SDF.asset";
        public const string SerifBoldPath = Folder + "/CormorantGaramond-SemiBold SDF.asset";
        public const string BodyPath = Folder + "/Montserrat-Medium SDF.asset";

        public static TMP_FontAsset Title { get; private set; }
        public static TMP_FontAsset SerifBold { get; private set; }
        public static TMP_FontAsset Body { get; private set; }

        /// <summary>Latin, Russian and Kazakh letters, digits and punctuation.</summary>
        static string Charset
        {
            get
            {
                var set = new StringBuilder();
                for (char c = ' '; c <= '~'; c++) set.Append(c);
                for (char c = 'А'; c <= 'я'; c++) set.Append(c);
                set.Append("ЁёӘәҒғҚқҢңӨөҰұҮүҺһІі");
                set.Append("«»—–…№×·•’‘“”„°₸←→");
                return set.ToString();
            }
        }

        public static bool Build()
        {
            ContentBuilder.EnsureFolder(Folder);
            var fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FallbackPath);
            Title = Bake(TitlePath, Folder + "/CormorantGaramond-Medium.ttf", 90, 9, 2048, 1024, fallback);
            SerifBold = Bake(SerifBoldPath, Folder + "/CormorantGaramond-SemiBold.ttf", 72, 8, 1024, 1024, fallback);
            Body = Bake(BodyPath, Folder + "/Montserrat-Medium.ttf", 64, 7, 1024, 1024, fallback);
            return Title != null && SerifBold != null && Body != null;
        }

        static TMP_FontAsset Bake(string path, string ttfPath, int pointSize, int padding, int width, int height, TMP_FontAsset fallback)
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (existing != null) return existing;

            var ttf = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
            if (ttf == null)
            {
                Debug.LogError($"Kazakh Ninja: font file {ttfPath} is missing.");
                return null;
            }

            TMP_FontAsset font = TMP_FontAsset.CreateFontAsset(ttf, pointSize, padding, GlyphRenderMode.SDFAA, width, height);
            if (font == null) return null;
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            font.name = name;
            AssetDatabase.CreateAsset(font, path);
            font.material.name = name + " Material";
            AssetDatabase.AddObjectToAsset(font.material, font);

            font.TryAddCharacters(Charset, out string missing);
            if (!string.IsNullOrEmpty(missing)) Debug.Log($"Kazakh Ninja: {name} has no glyphs for \"{missing}\" (they come from the fallback).");

            // The atlas grows as glyphs are added (and may spill into more pages); store every page in the asset.
            Texture2D[] atlases = font.atlasTextures;
            for (int i = 0; i < atlases.Length; i++)
            {
                if (atlases[i] == null || AssetDatabase.Contains(atlases[i])) continue;
                atlases[i].name = i == 0 ? name + " Atlas" : $"{name} Atlas {i}";
                AssetDatabase.AddObjectToAsset(atlases[i], font);
            }

            font.atlasPopulationMode = AtlasPopulationMode.Static;
            if (fallback != null) font.fallbackFontAssetTable = new List<TMP_FontAsset> { fallback };
            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssets();
            Debug.Log($"Kazakh Ninja: baked {name} ({atlases.Length} atlas page(s)).");
            return font;
        }

        // ---------- Materials ----------

        /// <summary>A copy of the font's material with a soft shadow under the letters, for text over the paintings.</summary>
        public static Material Shadowed(TMP_FontAsset font, string name, float softness, float dilate, Vector2 offset, float alpha)
        {
            string path = $"{Folder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(font.material) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = font.material.shader;
            material.SetTexture(ShaderUtilities.ID_MainTex, font.material.GetTexture(ShaderUtilities.ID_MainTex));
            material.EnableKeyword(ShaderUtilities.Keyword_Underlay);
            material.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, alpha));
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, offset.x);
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, offset.y);
            material.SetFloat(ShaderUtilities.ID_UnderlayDilate, dilate);
            material.SetFloat(ShaderUtilities.ID_UnderlaySoftness, softness);
            ShaderUtilities.UpdateShaderRatios(material);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>A copy with a dark outline and a shadow: popups and the score, readable over anything.</summary>
        public static Material Outlined(TMP_FontAsset font, string name, Color outline, float width)
        {
            Material material = Shadowed(font, name, 0.35f, 0.3f, new Vector2(0.5f, -0.9f), 0.5f);
            material.EnableKeyword(ShaderUtilities.Keyword_Outline);
            material.SetFloat(ShaderUtilities.ID_OutlineWidth, width);
            material.SetColor(ShaderUtilities.ID_OutlineColor, outline);
            ShaderUtilities.UpdateShaderRatios(material);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
