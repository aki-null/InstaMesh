using System;
using UnityEditor;
using UnityEngine;

namespace InstaMesh.Editor
{
    [CustomEditor(typeof(InstaMeshAsset))]
    public class InstaMeshAssetEditor : UnityEditor.Editor
    {
        private InstaMeshAsset _asset;

        private UnityEditor.Editor _editor;

        public void OnEnable()
        {
            _asset = (InstaMeshAsset)target;
        }

        private bool _showMeshInfo;

        private static void DrawDisc(SerializedProperty disc, SerializedProperty vertexColorSpace)
        {
            EditorGUILayout.PropertyField(disc.FindPropertyRelative(nameof(Disc.innerRadius)));
            EditorGUILayout.PropertyField(disc.FindPropertyRelative(nameof(Disc.outerRadius)));
            EditorGUILayout.PropertyField(disc.FindPropertyRelative(nameof(Disc.extrusion)));
            EditorGUILayout.PropertyField(disc.FindPropertyRelative(nameof(Disc.angle)));
            EditorGUILayout.PropertyField(disc.FindPropertyRelative(nameof(Disc.uSegments)));
            EditorGUILayout.PropertyField(disc.FindPropertyRelative(nameof(Disc.vSegments)));
            EditorGUILayout.PropertyField(disc.FindPropertyRelative(nameof(Disc.axis)));
            EditorGUILayout.PropertyField(disc.FindPropertyRelative(nameof(Disc.flipped)));
            EditorGUILayout.PropertyField(disc.FindPropertyRelative(nameof(Disc.doubleSided)));
            EditorGUILayout.PropertyField(disc.FindPropertyRelative(nameof(Disc.vertexColorUVType)));
            EditorGUILayout.PropertyField(disc.FindPropertyRelative(nameof(Disc.vertexColorMapType)));
            EditorGUILayout.PropertyField(disc.FindPropertyRelative(nameof(Disc.vertexColor)));
            EditorGUILayout.PropertyField(vertexColorSpace);

            var uvCountProp = disc.FindPropertyRelative(nameof(Disc.uvCount));
            EditorGUILayout.PropertyField(uvCountProp);
            var uvCount = uvCountProp.intValue;

            var currentUVProp = disc.FindPropertyRelative(nameof(Disc.uv0));
            for (var i = 0; i < 8 && i < uvCount; ++i)
            {
                EditorGUILayout.PropertyField(currentUVProp);
                currentUVProp.Next(false);
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var genType = serializedObject.FindProperty(nameof(InstaMeshAsset.genType));
            var disc = serializedObject.FindProperty(nameof(InstaMeshAsset.disc));
            var vertexColorSpace = serializedObject.FindProperty(nameof(InstaMeshAsset.vertexColorSpace));

            EditorGUILayout.PropertyField(genType);

            SerializedProperty genProp;
            switch (_asset.genType)
            {
                case GeneratorType.Disc:
                    DrawDisc(disc, vertexColorSpace);
                    break;
                default:
                    return;
            }

            var editor = Editor;
            if (editor != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.BeginVertical(GUI.skin.box);
                EditorGUILayout.LabelField("Mesh Info");
                editor.OnInspectorGUI();
                EditorGUILayout.EndVertical();
            }

            // The mesh regeneration is done in the asset's OnValidate to make undo work correctly.
            // However, this means the regeneration happens when the editor enters the play mode, which is not
            // desirable.
            // The NeedsRegeneration boolean is used as a "I want you to regenerate while I apply properties" flag.
            _asset.NeedsRegeneration = true;

            try
            {
                if (serializedObject.ApplyModifiedProperties())
                {
                    EditorUtility.SetDirty(_asset);
                }
            }
            finally
            {
                _asset.NeedsRegeneration = false;
            }
        }

        public override bool HasPreviewGUI()
        {
            return true;
        }

        private UnityEditor.Editor Editor
        {
            get
            {
                if (_asset == null) return null;
                var mesh = _asset.Mesh;
                if (mesh == null) return null;
                CreateCachedEditor(mesh, null, ref _editor);
                return _editor;
            }
        }

        public override void OnInteractivePreviewGUI(Rect r, GUIStyle background)
        {
            var editor = Editor;
            if (editor != null)
            {
                editor.OnInteractivePreviewGUI(r, background);
            }
        }

        public override void OnPreviewSettings()
        {
            var editor = Editor;
            if (editor != null)
            {
                editor.OnPreviewSettings();
            }
        }
    }
}