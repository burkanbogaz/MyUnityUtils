using System;
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
        private bool _includePrefabs = true; // Prefab kontrolü için yeni değişken
        private bool _searchingInProgress = false;
        private float _searchProgress = 0f;

        [MenuItem("MyUnityUtils/Missing Script Cleaner")]
        public static void ShowWindow()
        {
            var window = GetWindow<MissingScriptCleaner>(false, "Missing Script Cleaner");
            window.minSize = new Vector2(300, 250);
        }

        private void OnEnable()
        {
            // Texture'lara gerek kalmadı
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            
            if (!string.IsNullOrEmpty(_statusMessage))
            {
                var style = new GUIStyle(EditorStyles.helpBox)
                {
                    fontSize = 12,
                    padding = new RectOffset(10, 10, 10, 10),
                    margin = new RectOffset(5, 5, 5, 5)
                };
                EditorGUILayout.LabelField(_statusMessage, style);
                EditorGUILayout.Space(5);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(5);
                _includePrefabs = EditorGUILayout.ToggleLeft("Include Project Prefabs", _includePrefabs, GUILayout.Width(200));
            }
            
            EditorGUILayout.Space(5);

            if (_searchingInProgress)
            {
                Rect progressRect = EditorGUILayout.GetControlRect(false, 18);
                progressRect.x += 5;
                progressRect.width -= 10;
                EditorGUI.ProgressBar(progressRect, _searchProgress, $"Searching... {(_searchProgress * 100):F0}%");
                EditorGUILayout.Space(5);
            }

            if (_missingScriptObjects.Count > 0)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField("Objects with Missing Scripts:", EditorStyles.boldLabel);
                    EditorGUILayout.Space(5);
                    
                    foreach (GameObject obj in _missingScriptObjects)
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            GUILayout.Space(10);
                            EditorGUILayout.ObjectField(obj.name, obj, typeof(GameObject), true);
                        }
                    }
                }
            }
            else if (_foundMissingScripts)
            {
                EditorGUILayout.HelpBox("No missing scripts found in the current scene.", MessageType.Info);
            }

            EditorGUILayout.Space(10);

            // Modern buton stilleri
            var buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                padding = new RectOffset(15, 15, 8, 8),
                margin = new RectOffset(5, 5, 5, 5),
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white },
                hover = { textColor = Color.white },
                active = { textColor = Color.white }
            };

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();

                GUI.backgroundColor = new Color(0.35f, 0.35f, 0.35f);
                if (GUILayout.Button("Find Missing Scripts", buttonStyle, GUILayout.Height(30)))
                {
                    FindAllMissingScripts();
                    _statusMessage = _foundMissingScripts ? "Missing scripts found." : "No missing scripts detected.";
                }

                GUI.backgroundColor = _foundMissingScripts ? new Color(0.204F, 0.51F, 0.337F) : new Color(0.5f, 0.5f, 0.5f);
                EditorGUI.BeginDisabledGroup(!_foundMissingScripts);
                if (GUILayout.Button("Clear All", buttonStyle, GUILayout.Height(30)))
                {
                    ClearAllMissingScripts();
                    _statusMessage = "Cleared all missing scripts.";
                    _foundMissingScripts = false;
                }
                EditorGUI.EndDisabledGroup();
                
                GUI.backgroundColor = Color.white;
                GUILayout.FlexibleSpace();
            }
        }

        private void FindAllMissingScripts()
        {
            _missingScriptObjects.Clear();
            _uniquePrefabPaths.Clear();
            _searchingInProgress = true;
            _searchProgress = 0f;

            // Sahne objelerini kontrol et
            GameObject[] sceneObjects = GetAllObjectsInScene();
            CheckObjectsForMissingScripts(sceneObjects);

            // Prefabları kontrol et
            if (_includePrefabs)
            {
                CheckAllPrefabsInProject();
            }

            _foundMissingScripts = _missingScriptObjects.Count > 0;
            _searchingInProgress = false;
            _searchProgress = 1f;
        }

        private void CheckObjectsForMissingScripts(GameObject[] objects)
        {
            foreach (GameObject obj in objects)
            {
                if (obj == null) continue;

                // Null component kontrolü
                Component[] components = obj.GetComponents<Component>();
                bool hasMissingScript = components.Any(c => c == null);

                if (hasMissingScript)
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

                // Alt objeleri recursive olarak kontrol et
                Transform objTransform = obj.transform;
                for (int i = 0; i < objTransform.childCount; i++)
                {
                    CheckObjectsForMissingScripts(new[] { objTransform.GetChild(i).gameObject });
                }
            }
        }

        private void CheckAllPrefabsInProject()
        {
            string[] allPrefabPaths = AssetDatabase.GetAllAssetPaths()
                .Where(path => path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            float progressStep = 1f / allPrefabPaths.Length;
            
            for (int i = 0; i < allPrefabPaths.Length; i++)
            {
                string prefabPath = allPrefabPaths[i];
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                
                if (prefab != null)
                {
                    // Prefab'ın kendisini ve tüm alt objelerini kontrol et
                    CheckPrefabForMissingScripts(prefab, prefabPath);
                }

                _searchProgress = i * progressStep;
                // Her 10 prefabda bir UI'ı güncelle
                if (i % 10 == 0)
                {
                    EditorUtility.DisplayProgressBar("Checking Prefabs", 
                        $"Checking prefab {i + 1} of {allPrefabPaths.Length}", _searchProgress);
                }
            }

            EditorUtility.ClearProgressBar();
        }

        private void CheckPrefabForMissingScripts(GameObject prefab, string prefabPath)
        {
            // Prefab'ın kendisini kontrol et
            Component[] components = prefab.GetComponents<Component>();
            if (components.Any(c => c == null))
            {
                if (_uniquePrefabPaths.Add(prefabPath))
                {
                    _missingScriptObjects.Add(prefab);
                }
            }

            // Prefab'ın tüm alt objelerini recursive olarak kontrol et
            Transform[] allChildren = prefab.GetComponentsInChildren<Transform>(true);
            foreach (Transform child in allChildren)
            {
                if (child.gameObject == prefab) continue;

                components = child.GetComponents<Component>();
                if (components.Any(c => c == null))
                {
                    if (_uniquePrefabPaths.Add(prefabPath))
                    {
                        _missingScriptObjects.Add(prefab);
                        break; // Aynı prefab'ı tekrar eklemeye gerek yok
                    }
                }
            }
        }

        private void ClearAllMissingScripts()
        {
            foreach (GameObject obj in _missingScriptObjects)
            {
                if (obj == null) continue;

                string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(obj);
                bool isPrefabAsset = !string.IsNullOrEmpty(prefabPath);

                if (isPrefabAsset)
                {
                    // Prefab asset'i düzenle
                    GameObject prefabAsset = PrefabUtility.LoadPrefabContents(prefabPath);
                    if (prefabAsset != null)
                    {
                        bool modified = false;
                        // Prefab'ın kendisi ve tüm alt objelerindeki missing scriptleri temizle
                        Transform[] allTransforms = prefabAsset.GetComponentsInChildren<Transform>(true);
                        foreach (Transform t in allTransforms)
                        {
                            int removedCount = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
                            if (removedCount > 0)
                            {
                                modified = true;
                                Debug.Log($"Removed {removedCount} missing scripts from prefab: {prefabPath} in object: {t.name}");
                            }
                        }

                        if (modified)
                        {
                            PrefabUtility.SaveAsPrefabAsset(prefabAsset, prefabPath);
                            EditorUtility.SetDirty(prefabAsset);
                        }
                        PrefabUtility.UnloadPrefabContents(prefabAsset);
                    }
                }
                else
                {
                    // Sahne objesini düzenle
                    Undo.RecordObject(obj, "Remove Missing Scripts");
                    int removedCount = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(obj);
                    if (removedCount > 0)
                    {
                        Debug.Log($"Removed {removedCount} missing scripts from scene object: {obj.name}");
                        EditorUtility.SetDirty(obj);
                    }
                }
            }

            _missingScriptObjects.Clear();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static GameObject[] GetAllObjectsInScene()
        {
            return Resources.FindObjectsOfTypeAll<GameObject>();
        }
    }
}
