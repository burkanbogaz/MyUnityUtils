using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    public sealed class MissingScriptCleaner : EditorWindow
    {
        private readonly List<GameObject> _missingScriptObjects = new List<GameObject>();
        private readonly HashSet<string> _uniquePrefabPaths = new HashSet<string>();
        private bool _foundMissingScripts = false;
        private string _statusMessage = "";  // For displaying status messages to the user

        private Texture2D _normalFindTexture;
        private Texture2D _hoverFindTexture;
        private Texture2D _activeFindTexture;
        private Texture2D _normalClearTexture;
        private Texture2D _hoverClearTexture;
        private Texture2D _activeClearTexture;

        [MenuItem("Tools/MyUnityUtils/Missing Script Cleaner")]
        public static void ShowWindow()
        {
            var window = GetWindow<MissingScriptCleaner>(false, "Missing Script Cleaner");
            window.minSize = new Vector2(300, 250);
        }

        private void OnEnable()
        {
            InitTextures();  // Initialize textures when window is enabled
        }

        private void InitTextures()
        {
            _normalFindTexture = MakeColorTexture(new Color(0.78F, 0.404F, 0.373F));
            _hoverFindTexture = MakeColorTexture(new Color(0.878F, 0.455F, 0.416F));
            _activeFindTexture = MakeColorTexture(new Color(0.98F, 0.596F, 0.561F));

            _normalClearTexture = MakeColorTexture(new Color(0.204F, 0.51F, 0.337F));
            _hoverClearTexture = MakeColorTexture(new Color(0.282F, 0.722F, 0.471F));
            _activeClearTexture = MakeColorTexture(new Color(0.4f, 1f, 0.4f));
        }

        private void OnGUI()
        {
            if (!string.IsNullOrEmpty(_statusMessage))
            {
                EditorGUILayout.HelpBox(_statusMessage, MessageType.Info);
            }

            if (_missingScriptObjects.Count > 0)
            {
                EditorGUILayout.LabelField("Objects with missing scripts:", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;

                foreach (GameObject obj in _missingScriptObjects)
                {
                    EditorGUILayout.ObjectField(obj.name, obj, typeof(GameObject), true);
                }

                EditorGUI.indentLevel--;
            }
            else if (_foundMissingScripts)
            {
                EditorGUILayout.LabelField("No missing scripts found in the current scene.");
            }

            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            GUIStyle btnStyle = new(GUI.skin.button)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 12,
                fixedHeight = 40,
                normal = { background = _normalFindTexture, textColor = Color.white },
                hover = { background = _hoverFindTexture, textColor = Color.white },
                active = { background = _activeFindTexture, textColor = Color.white }
            };

            float buttonWidth = (position.width - 20) / 2;

            if (GUILayout.Button("Find Missing Scripts", btnStyle, GUILayout.Width(buttonWidth)))
            {
                FindAllMissingScripts();
                _statusMessage = _foundMissingScripts ? "Missing scripts found." : "No missing scripts detected.";
            }

            GUILayout.Space(10);

            btnStyle.normal.background = _normalClearTexture;
            btnStyle.hover.background = _hoverClearTexture;
            btnStyle.active.background = _activeClearTexture;

            EditorGUI.BeginDisabledGroup(!_foundMissingScripts);
            if (GUILayout.Button("Clear All", btnStyle, GUILayout.Width(buttonWidth)))
            {
                ClearAllMissingScripts();
                _statusMessage = "Cleared all missing scripts.";
                _foundMissingScripts = false;
            }
            EditorGUI.EndDisabledGroup();

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private Texture2D MakeColorTexture(Color color)
        {
            Texture2D texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        private void FindAllMissingScripts()
        {
            _missingScriptObjects.Clear();
            _uniquePrefabPaths.Clear();
            GameObject[] allObjects = GetAllObjectsInScene();

            foreach (GameObject obj in allObjects)
            {
                if (obj.GetComponents<Component>().Any(c => c == null))
                {
                    string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(obj);
                    if (!string.IsNullOrEmpty(prefabPath))
                    {
                        if (_uniquePrefabPaths.Add(prefabPath))
                        {
                            _missingScriptObjects.Add(obj);
                        }
                    }
                    else
                    {
                        _missingScriptObjects.Add(obj);
                    }
                }
            }

            _foundMissingScripts = _missingScriptObjects.Count > 0;
        }

        private void ClearAllMissingScripts()
        {
            foreach (GameObject obj in _missingScriptObjects)
            {
                Undo.RecordObject(obj, "Remove Missing Scripts");
                int removedCount = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(obj);
                Debug.Log(obj.name + ": Removed " + removedCount + " missing scripts.");
                EditorUtility.SetDirty(obj);

                // Check if this GameObject is part of a prefab instance
                if (PrefabUtility.IsPartOfPrefabInstance(obj))
                {
                    // Apply changes to the prefab asset
                    GameObject prefabRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(obj); // Find the root GameObject of the prefab instance
                    if (prefabRoot != null)
                    {
                        GameObject prefabSource = PrefabUtility.GetCorrespondingObjectFromSource(prefabRoot) as GameObject;
                        if (prefabSource != null)
                        {
                            // Push the changes from the scene instance to the asset
                            PrefabUtility.ApplyPrefabInstance(prefabRoot, InteractionMode.UserAction);
                        }
                    }
                }
            }
            _missingScriptObjects.Clear();
        }

        private static GameObject[] GetAllObjectsInScene()
        {
            return Resources.FindObjectsOfTypeAll<GameObject>();
        }
    }
}
