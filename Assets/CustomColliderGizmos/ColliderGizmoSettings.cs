using UnityEngine;
using UnityEditor;
using System.IO;

public class ColliderGizmoSettings : ScriptableObject
{
    public bool showOnlyWhenSelected = true;
    public bool showTriggers = true;
    public bool showNormalColliders = true;
    public bool showWireframe = true;
    public bool showFilled = true;
    public bool showInPrefabMode = true;
    
    public Color normalColliderColor = new Color(0.0f, 0.5f, 0.0f, 0.4f);
    public Color triggerColliderColor = new Color(0.0f, 0.8f, 0.0f, 0.3f);
    
    private static ColliderGizmoSettings _instance;
    public static ColliderGizmoSettings Instance
    {
        get
        {
            if (_instance == null)
            {
                // Find the settings asset in the CustomColliderGizmos folder
                string[] guids = AssetDatabase.FindAssets("t:ColliderGizmoSettings", new[] { "Assets/Scripts/DivisionPack/Editor/CustomColliderGizmos" });
                
                if (guids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    _instance = AssetDatabase.LoadAssetAtPath<ColliderGizmoSettings>(path);
                }
                
                // If not found, create a new one in the same folder as this script
                if (_instance == null)
                {
                    _instance = CreateInstance<ColliderGizmoSettings>();
                    
                    // Get the path to the script folder
                    MonoScript script = MonoScript.FromScriptableObject(_instance);
                    string scriptPath = AssetDatabase.GetAssetPath(script);
                    string folderPath = Path.GetDirectoryName(scriptPath);
                    
                    // If we can't find the folder path, default to CustomColliderGizmos folder
                    if (string.IsNullOrEmpty(folderPath))
                    {
                        folderPath = "Assets/Scripts/DivisionPack/Editor/CustomColliderGizmos";
                    }
                    
                    // Make sure directory exists
                    if (!Directory.Exists(folderPath))
                    {
                        Directory.CreateDirectory(folderPath);
                    }
                    
                    string assetPath = Path.Combine(folderPath, "ColliderGizmoSettings.asset");
                    AssetDatabase.CreateAsset(_instance, assetPath);
                    AssetDatabase.SaveAssets();
                    Debug.Log("ColliderGizmoSettings created at " + assetPath);
                }
            }
            
            return _instance;
        }
    }
}

[CustomEditor(typeof(ColliderGizmoSettings))]
public class ColliderGizmoSettingsEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUI.BeginChangeCheck();
        
        ColliderGizmoSettings settings = (ColliderGizmoSettings)target;
        
        EditorGUILayout.LabelField("Display Options", EditorStyles.boldLabel);
        settings.showOnlyWhenSelected = EditorGUILayout.Toggle("Show Only When Selected", settings.showOnlyWhenSelected);
        settings.showTriggers = EditorGUILayout.Toggle("Show Triggers", settings.showTriggers);
        settings.showNormalColliders = EditorGUILayout.Toggle("Show Normal Colliders", settings.showNormalColliders);
        settings.showWireframe = EditorGUILayout.Toggle("Show Wireframe", settings.showWireframe);
        settings.showFilled = EditorGUILayout.Toggle("Show Filled", settings.showFilled);
        settings.showInPrefabMode = EditorGUILayout.Toggle("Show In Prefab Mode", settings.showInPrefabMode);
        
        EditorGUILayout.Space();
        
        EditorGUILayout.LabelField("Color Options", EditorStyles.boldLabel);
        settings.normalColliderColor = EditorGUILayout.ColorField("Normal Collider Color", settings.normalColliderColor);
        settings.triggerColliderColor = EditorGUILayout.ColorField("Trigger Collider Color", settings.triggerColliderColor);
        
        if (EditorGUI.EndChangeCheck())
        {
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }
    }
}

// Menü öğesi ekle
public class ColliderGizmoSettingsMenu
{
    [MenuItem("DivisionPack/Utility/CustomColliderGizmosSetting")]
    private static void ShowSettings()
    {
        Selection.activeObject = ColliderGizmoSettings.Instance;
    }
} 