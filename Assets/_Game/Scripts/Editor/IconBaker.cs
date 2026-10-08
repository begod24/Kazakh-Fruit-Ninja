using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace KazakhNinja.EditorTools
{
    /// <summary>
    /// Renders every food's mesh with its own materials into a transparent sprite for the collection UI.
    /// Each icon is rendered on black and on white; the difference gives exact alpha, whatever the
    /// render pipeline does with the alpha channel. Re-bake after swapping placeholder art.
    /// </summary>
    public static class IconBaker
    {
        const string IconFolder = "Assets/_Game/UI/Icons";
        const string FoodFolder = "Assets/_Game/Data";
        const int Size = 256;

        [MenuItem("Kazakh Ninja/Bake Food Icons", priority = 40)]
        public static void BakeMissing() => Bake(false);

        [MenuItem("Kazakh Ninja/Rebake All Food Icons", priority = 41)]
        public static void BakeAll() => Bake(true);

        public static void Bake(bool overwrite)
        {
            ContentBuilder.EnsureFolder(IconFolder);
            int baked = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:FoodDefinition", new[] { FoodFolder }))
            {
                var food = AssetDatabase.LoadAssetAtPath<FoodDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (food == null || food.mesh == null || food.skinMaterials == null || food.skinMaterials.Length == 0) continue;
                string path = $"{IconFolder}/{food.id}.png";
                if (!overwrite && food.icon != null && File.Exists(path)) continue;

                Texture2D icon = RenderIcon(food);
                File.WriteAllBytes(path, icon.EncodeToPNG());
                Object.DestroyImmediate(icon);
                AssetDatabase.ImportAsset(path);
                ConfigureSprite(path);

                food.icon = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                EditorUtility.SetDirty(food);
                baked++;
            }
            AssetDatabase.SaveAssets();
            if (baked > 0) Debug.Log($"Kazakh Ninja: baked {baked} food icons.");
        }

        static Texture2D RenderIcon(FoodDefinition food)
        {
            Texture2D onBlack = Shoot(food, Color.black);
            Texture2D onWhite = Shoot(food, Color.white);
            Color[] black = onBlack.GetPixels(), white = onWhite.GetPixels();
            var pixels = new Color[black.Length];
            for (int i = 0; i < black.Length; i++)
            {
                // Over black a pixel is colour * alpha; over white it is that plus (1 - alpha).
                float alpha = 1f - ((white[i].r - black[i].r) + (white[i].g - black[i].g) + (white[i].b - black[i].b)) / 3f;
                alpha = Mathf.Clamp01(alpha);
                pixels[i] = alpha > 0.004f
                    ? new Color(Mathf.Clamp01(black[i].r / alpha), Mathf.Clamp01(black[i].g / alpha), Mathf.Clamp01(black[i].b / alpha), alpha)
                    : Color.clear;
            }
            var result = new Texture2D(onBlack.width, onBlack.height, TextureFormat.RGBA32, false);
            result.SetPixels(pixels);
            result.Apply();
            Object.DestroyImmediate(onBlack);
            Object.DestroyImmediate(onWhite);
            return result;
        }

        static Texture2D Shoot(FoodDefinition food, Color background)
        {
            var preview = new PreviewRenderUtility();
            try
            {
                Camera camera = preview.camera;
                camera.fieldOfView = 24f;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 30f;
                camera.transform.SetPositionAndRotation(new Vector3(0f, 0f, -8f), Quaternion.identity);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = background;
                preview.lights[0].intensity = 1.3f;
                preview.lights[0].transform.rotation = Quaternion.Euler(35f, -30f, 0f);
                preview.lights[1].intensity = 0.5f;
                preview.lights[1].transform.rotation = Quaternion.Euler(-20f, 150f, 0f);
                preview.ambientColor = new Color(0.35f, 0.33f, 0.3f);

                // Fit the food into ~80% of the frame, tilted so its shape and thickness read.
                Bounds bounds = food.mesh.bounds;
                float radius = Vector3.Scale(bounds.extents, food.scale).magnitude;
                float fit = 1.35f / Mathf.Max(radius, 0.01f);
                Quaternion rotation = Quaternion.Euler(-18f, 28f, 30f);
                Vector3 scale = food.scale * fit;
                Vector3 offset = -(rotation * Vector3.Scale(bounds.center, scale));
                Matrix4x4 matrix = Matrix4x4.TRS(offset, rotation, scale);

                preview.BeginStaticPreview(new Rect(0f, 0f, Size, Size));
                for (int submesh = 0; submesh < food.mesh.subMeshCount; submesh++)
                {
                    Material material = food.skinMaterials[Mathf.Min(submesh, food.skinMaterials.Length - 1)];
                    preview.DrawMesh(food.mesh, matrix, material, submesh);
                }
                preview.Render(true);
                return preview.EndStaticPreview();
            }
            finally
            {
                preview.Cleanup();
            }
        }

        static void ConfigureSprite(string path)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }
    }
}
