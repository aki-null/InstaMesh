using UnityEditor;
using UnityEngine;

namespace InstaMesh.Editor
{
    public class InstaMeshPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets,
            string[] movedAssets, string[] movedFromAssetPaths)
        {
            foreach (string str in importedAssets)
            {
                var instaMesh = AssetDatabase.LoadAssetAtPath<InstaMeshAsset>(str);
                if (instaMesh == null)
                {
                    continue;
                }

                var mesh = instaMesh.Mesh;
                if (mesh != null) continue;

                // Initial mesh generation
                mesh = new Mesh
                {
                    name = "InstaMesh"
                };
                // Treat it as a sub-asset
                AssetDatabase.AddObjectToAsset(mesh, instaMesh);
                // Configure default
                instaMesh.vertexColorSpace = InstaMeshSettings.GetOrCreateSettings().defaultColorSpace;
                instaMesh.disc.vertexColor = new Gradient();
                // Make something up using the default config
                instaMesh.Generate(mesh);
                EditorUtility.SetDirty(mesh);
                EditorUtility.SetDirty(instaMesh);
            }
        }
    }
}