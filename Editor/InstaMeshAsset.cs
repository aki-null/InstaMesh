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
                    disc.Generate(mesh);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            EditorUtility.SetDirty(mesh);
        }
    }
}