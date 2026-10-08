using UnityEditor;
using UnityEngine;

namespace KazakhNinja.EditorTools
{
    /// <summary>
    /// The backdrop paintings, one per scene of the day: <c>Textures/Backgrounds/{id}.jpg</c> (or .png), any
    /// landscape shape. They keep their own size (no power-of-two scaling), so the director can crop them by
    /// their real shape.
    /// </summary>
    static class BackgroundArt
    {
        public const string Folder = "Assets/_Game/Textures/Backgrounds";
        static readonly string[] Extensions = { ".jpg", ".jpeg", ".png" };

        /// <summary>The painting of scene <paramref name="id"/>, with its import settings applied.</summary>
        public static Texture2D Picture(string id)
        {
            foreach (string extension in Extensions)
            {
                string path = $"{Folder}/{id}{extension}";
                if (AssetImporter.GetAtPath(path) == null) continue;
                ProcTex.Configure(path, importer =>
                {
                    importer.textureType = TextureImporterType.Default;
                    importer.sRGBTexture = true;
                    importer.alphaSource = TextureImporterAlphaSource.None;
                    importer.npotScale = TextureImporterNPOTScale.None;
                    importer.mipmapEnabled = false;
                    importer.wrapMode = TextureWrapMode.Clamp;
                    importer.filterMode = FilterMode.Bilinear;
                    importer.maxTextureSize = 2048;
                    importer.textureCompression = TextureImporterCompression.CompressedHQ;
                });
                return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
            Debug.LogError($"Kazakh Ninja: the backdrop painting {Folder}/{id}.jpg is missing.");
            return null;
        }
    }
}
