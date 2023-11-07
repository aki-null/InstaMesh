using UnityEditor;
using UnityEngine;

namespace InstaMesh.Editor
{
    [CustomEditor(typeof(InstaMeshAsset))]
    public class InstaMeshAssetEditor : UnityEditor.Editor
    {
        private InstaMeshAsset _instaMesh;

        private UnityEditor.Editor _editor;

        public void OnEnable()
        {
            _instaMesh = (InstaMeshAsset)target;
        }

        private void OnDisable()
        {
            if (_editor != null)
            {
                DestroyImmediate(_editor);
                _editor = null;
            }
        }

        private bool _showMeshInfo;

        public override void OnInspectorGUI()
        {
            var genType = serializedObject.FindProperty("genType");
            var disc = serializedObject.FindProperty("disc");

            EditorGUILayout.PropertyField(genType);

            switch (_instaMesh.genType)
            {
                case GeneratorType.Disc:
                    EditorGUILayout.PropertyField(disc);
                    break;
            }

            _showMeshInfo = EditorGUILayout.Foldout(_showMeshInfo, "Mesh Detail");
            if (_showMeshInfo)
            {
                var editor = Editor;
                if (editor != null)
                {
                    editor.OnInspectorGUI();
                }
            }

            if (serializedObject.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(_instaMesh);
                _instaMesh.Generate(_instaMesh.Mesh);
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
                if (_instaMesh == null) return null;
                var mesh = _instaMesh.Mesh;
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