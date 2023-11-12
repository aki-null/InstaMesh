using System;
using UnityEditor;
using UnityEngine;

namespace InstaMesh.Editor
{
    [CreateAssetMenu(menuName = "InstaMesh Asset", fileName = "New InstaMesh")]
    public class InstaMeshAsset : ScriptableObject
    {
        public GeneratorType genType;
        public Disc disc = new();
        public ColorSpace vertexColorSpace;

        // This flag is used by InstaMeshAssetEditor to activate OnValidate mesh regeneration mode
        [NonSerialized] public bool NeedsRegeneration;

        public Mesh Mesh
        {
            get
            {
                // Mesh reference is not kept as serialized field due to the reference being lost in some situations
                var path = AssetDatabase.GetAssetPath(this);
                if (string.IsNullOrEmpty(path)) return null;
                var assets = AssetDatabase.LoadAllAssetsAtPath(path);
                foreach (var asset in assets)
                {
                    if (asset is Mesh mesh)
                    {
                        return mesh;
                    }
                }

                return null;
            }
        }

        public void Generate(Mesh mesh)
        {
            if (mesh == null)
            {
                return;
            }

            switch (genType)
            {
                case GeneratorType.Disc:
                    disc.Generate(mesh, vertexColorSpace);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            EditorUtility.SetDirty(mesh);
        }

        private void OnValidate()
        {
            if (NeedsRegeneration || Undo.isProcessing)
            {
                Generate(Mesh);
            }
        }
    }
}