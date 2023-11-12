using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace InstaMesh.Editor
{
    internal class InstaMeshSettings : ScriptableObject
    {
        private const string SettingsPath = "Assets/Editor/InstaMeshSettings.asset";

        public ColorSpace defaultColorSpace;

        public static InstaMeshSettings GetOrCreateSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<InstaMeshSettings>(SettingsPath);
            if (settings != null) return settings;

            if (!AssetDatabase.IsValidFolder("Assets/Editor"))
            {
                AssetDatabase.CreateFolder("Assets", "Editor");
            }

            settings = CreateInstance<InstaMeshSettings>();
            settings.defaultColorSpace = ColorSpace.Linear;
            AssetDatabase.CreateAsset(settings, SettingsPath);
            AssetDatabase.SaveAssets();

            return settings;
        }

        internal static SerializedObject GetSerializedSettings()
        {
            return new SerializedObject(GetOrCreateSettings());
        }
    }

    internal static class InstaMeshSettingsIMGUIRegister
    {
        [SettingsProvider]
        public static SettingsProvider CreateMyCustomSettingsProvider()
        {
            var provider = new SettingsProvider("Project/InstaMesh", SettingsScope.Project)
            {
                label = "InstaMesh",
                guiHandler = _ =>
                {
                    var settings = InstaMeshSettings.GetSerializedSettings();
                    EditorGUILayout.PropertyField(settings.FindProperty(nameof(InstaMeshSettings.defaultColorSpace)),
                        new GUIContent("Default Color Space"));
                    settings.ApplyModifiedProperties();
                },

                keywords = new HashSet<string>(new[] { "Default Color Space", "Default ColorSpace" })
            };

            return provider;
        }
    }
}