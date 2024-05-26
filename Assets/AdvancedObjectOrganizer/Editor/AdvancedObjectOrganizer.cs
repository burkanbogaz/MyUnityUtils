using UnityEditor;
using UnityEngine;
using System.Linq;

namespace Editor
{
    public class AdvancedObjectOrganizer : EditorWindow
    {
        string prefix = "NewName";
        string suffix = "";
        int startNumber = 1;
        int increment = 1;
        string statusMessage = ""; // For displaying status messages to the user

        private Texture2D _blueTexture;
        private Texture2D _greenTexture;

        [MenuItem("Tools/MyUnityUtils/Advanced Object Organizer")]
        public static void ShowWindow()
        {
            GetWindow<AdvancedObjectOrganizer>("Advanced Object Organizer").minSize = new Vector2(400, 300);
        }

        void OnEnable()
        {
            InitTextures();
        }

        private void InitTextures()
        {
            _blueTexture = MakeColorTexture(new Color(0.25f, 0.45f, 0.75f)); // Uniform blue color
            _greenTexture = MakeColorTexture(new Color(0.2f, 0.8f, 0.2f));   // Soft green color
        }

        void OnGUI()
        {
            GUILayout.Label("Rename and Sort Settings", EditorStyles.boldLabel);

            prefix = EditorGUILayout.TextField("Prefix", prefix);
            suffix = EditorGUILayout.TextField("Suffix", suffix);
            startNumber = EditorGUILayout.IntField("Start Number", startNumber);
            increment = EditorGUILayout.IntField("Increment", increment);

            GUILayout.Space(10);  // Add space for better layout

            if (!string.IsNullOrEmpty(statusMessage))
            {
                EditorGUILayout.HelpBox(statusMessage, MessageType.Info);
            }

            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button) {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                fixedHeight = 35  // Reduced button height
            };

            buttonStyle.normal.background = _greenTexture;
            if (GUILayout.Button("Apply Naming", buttonStyle))
            {
                ApplyNaming();
                statusMessage = "Naming applied successfully.";
            }

            GUILayout.Space(10);  // Add space between button sections

            buttonStyle.normal.background = _blueTexture;
            if (GUILayout.Button("Sort by Instance ID", buttonStyle))
            {
                SortSelectedObjectsByInstanceID();
                statusMessage = "Sorted by Instance ID.";
            }

            if (GUILayout.Button("Sort by Alphabet", buttonStyle))
            {
                SortSelectedObjectsByAlphabet();
                statusMessage = "Sorted by Alphabet.";
            }

            if (GUILayout.Button("Sort by Tag", buttonStyle))
            {
                SortByTag();
                statusMessage = "Sorted by Tag.";
            }

            if (GUILayout.Button("Sort by Layer", buttonStyle))
            {
                SortByLayer();
                statusMessage = "Sorted by Layer.";
            }

            if (GUILayout.Button("Sort by Visibility", buttonStyle))
            {
                SortByVisibility();
                statusMessage = "Sorted by Visibility.";
            }
        }

        private void ApplyNaming()
        {
            GameObject[] selectedObjects = Selection.gameObjects.OrderBy(obj => obj.transform.GetSiblingIndex()).ToArray();
            int count = startNumber;

            foreach (GameObject obj in selectedObjects)
            {
                Undo.RecordObject(obj, "Apply Naming");
                obj.name = prefix + count + suffix;
                count += increment;
            }
        }

        private void SortSelectedObjectsByInstanceID()
        {
            GameObject[] selectedObjects = Selection.gameObjects.OrderBy(obj => obj.GetInstanceID()).ToArray();
            ReorderHierarchy(selectedObjects);
        }

        private void SortSelectedObjectsByAlphabet()
        {
            GameObject[] selectedObjects = Selection.gameObjects.OrderBy(obj => obj.name).ToArray();
            ReorderHierarchy(selectedObjects);
        }

        private void SortByTag()
        {
            GameObject[] selectedObjects = Selection.gameObjects.OrderBy(obj => obj.tag).ToArray();
            ReorderHierarchy(selectedObjects);
        }

        private void SortByLayer()
        {
            GameObject[] selectedObjects = Selection.gameObjects.OrderBy(obj => obj.layer).ToArray();
            ReorderHierarchy(selectedObjects);
        }

        private void SortByVisibility()
        {
            GameObject[] selectedObjects = Selection.gameObjects.OrderBy(obj => !obj.activeInHierarchy).ThenBy(obj => obj.name).ToArray();
            ReorderHierarchy(selectedObjects);
        }

        private void ReorderHierarchy(GameObject[] objects)
        {
            for (int i = 0; i < objects.Length; i++)
            {
                objects[i].transform.SetSiblingIndex(i);
            }
        }

        private Texture2D MakeColorTexture(Color color)
        {
            Texture2D texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }
    }
}
