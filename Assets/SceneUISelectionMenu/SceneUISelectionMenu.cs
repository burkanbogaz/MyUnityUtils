using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_2021_2_OR_NEWER
using PrefabStage = UnityEditor.SceneManagement.PrefabStage;
using PrefabStageUtility = UnityEditor.SceneManagement.PrefabStageUtility;
#elif UNITY_2018_3_OR_NEWER
using PrefabStage = UnityEditor.Experimental.SceneManagement.PrefabStage;
using PrefabStageUtility = UnityEditor.Experimental.SceneManagement.PrefabStageUtility;
#endif

public class SceneUISelectionMenu : EditorWindow
{
	private struct Entry
	{
		public readonly RectTransform RectTransform;
		public readonly List<Entry> Children;

		public Entry( RectTransform rectTransform )
		{
			RectTransform = rectTransform;
			Children = new List<Entry>( 2 );
		}
	}

	private class UIObjectInfo
	{
		public RectTransform RectTransform;
		public string Label;
		public GUIContent Content;
		public int IndentLevel;
		
		public UIObjectInfo(RectTransform rectTransform, string label, int indentLevel)
		{
			RectTransform = rectTransform;
			Label = label;
			IndentLevel = indentLevel;
			Content = new GUIContent(label, GetIconForUIObject(rectTransform));
		}
	}

	private readonly List<UIObjectInfo> uiObjectInfos = new List<UIObjectInfo>(16);
	private readonly List<UIObjectInfo> filteredUiObjectInfos = new List<UIObjectInfo>(16);

	private string searchText = "";
	private bool isSearching = false;
	private Vector2 scrollPosition = Vector2.zero;
	
	// Cache for component icons
	private static Dictionary<Type, Texture> componentIconCache = new Dictionary<Type, Texture>();

	private static RectTransform hoveredUIObject;
	private static readonly Vector3[] hoveredUIObjectCorners = new Vector3[4];
	private static readonly List<ICanvasRaycastFilter> raycastFilters = new List<ICanvasRaycastFilter>( 4 );

	private static double lastRightClickTime;
	private static Vector2 lastRightPos;
	private static bool blockSceneViewInput;

	private readonly MethodInfo screenFittedRectGetter = typeof( EditorWindow ).Assembly.GetType( "UnityEditor.ContainerWindow" ).GetMethod( "FitRectToScreen", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static );

	private const float Padding = 1f;
	private const float SearchBarHeight = 20f;
	private const float IconSize = 16f;
	private const float IconPadding = 2f;
	private const float IndentWidth = 15f;
	private float RowHeight { get { return EditorGUIUtility.singleLineHeight; } }
	private GUIStyle RowGUIStyle { get { return "MenuItem"; } }
	
	// Define highlight colors
	private static readonly Color HighlightFillColor = new Color(1f, 1f, 1f, 0.2f);
	private static readonly Color HighlightOutlineColor = new Color(1f, 1f, 1f, 0.8f);

	private void ShowContextWindow( List<Entry> results )
	{
		InitializeUIObjectsRecursive( results, 0 );

		// Cache filtered UI objects
		FilterUIObjects();

		GUIStyle rowGUIStyle = RowGUIStyle;
		float preferredWidth = 0f;
		foreach(UIObjectInfo info in uiObjectInfos)
		{
			// Calculate width needed considering indent level and icon
			float labelWidth = rowGUIStyle.CalcSize(new GUIContent(info.Label)).x;
			float totalWidth = labelWidth + (info.IndentLevel * IndentWidth) + IconSize + IconPadding * 2;
			preferredWidth = Mathf.Max(preferredWidth, totalWidth);
		}

		ShowAsDropDown( new Rect(), new Vector2( preferredWidth + Padding * 2f, GetWindowHeight() ) );

		// Show dropdown above the cursor instead of below the cursor
		position = (Rect) screenFittedRectGetter.Invoke( null, new object[3] { new Rect( GUIUtility.GUIToScreenPoint( Event.current.mousePosition ) - new Vector2( 0f, position.height ), position.size ), true, true } );
	}

	private float GetWindowHeight()
	{
		int itemCount = isSearching ? filteredUiObjectInfos.Count : uiObjectInfos.Count;
		float contentHeight = itemCount * RowHeight;
		float totalHeight = contentHeight + SearchBarHeight + Padding * 3f;
		
		// Limit max height to 70% of screen height
		float maxHeight = Screen.currentResolution.height * 0.7f;
		return Mathf.Min(totalHeight, maxHeight);
	}

	private void InitializeUIObjectsRecursive( List<Entry> results, int depth )
	{
		foreach( Entry entry in results )
		{
			// Clean object name without indent spaces
			string label = entry.RectTransform.name;
			
			// Store UI object info with indent level
			uiObjectInfos.Add(new UIObjectInfo(entry.RectTransform, label, depth));

			if( entry.Children.Count > 0 )
				InitializeUIObjectsRecursive( entry.Children, depth + 1 );
		}
	}

	private static Texture GetIconForUIObject(RectTransform rectTransform)
	{
		if (rectTransform == null)
			return null;
			
		// Default icon for RectTransform
		Texture icon = GetIconForComponentType(typeof(RectTransform));
			
		// Check for common UI components in priority order
		Component[] components = rectTransform.GetComponents<Component>();
		
		// Define priority for icons
		Type[] priorityComponents = new Type[]
		{
			typeof(Button),
			typeof(Image),
			typeof(RawImage),
			typeof(Text),
			typeof(TMPro.TextMeshProUGUI),
			typeof(InputField),
			typeof(TMPro.TMP_InputField),
			typeof(Dropdown),
			typeof(TMPro.TMP_Dropdown),
			typeof(Slider),
			typeof(Toggle),
			typeof(Scrollbar),
			typeof(ScrollRect),
			typeof(Mask),
			typeof(RectMask2D),
			typeof(Canvas),
			typeof(CanvasGroup),
			typeof(LayoutGroup),
			typeof(ContentSizeFitter),
			typeof(LayoutElement)
		};
		
		// Find the first component from priority list
		foreach (Type priorityType in priorityComponents)
		{
			foreach (Component component in components)
			{
				if (component != null && priorityType.IsInstanceOfType(component))
				{
					return GetIconForComponentType(component.GetType());
				}
			}
		}
		
		return icon;
	}
	
	private static Texture GetIconForComponentType(Type componentType)
	{
		// Check cache first
		if (componentIconCache.TryGetValue(componentType, out Texture cachedIcon))
		{
			return cachedIcon;
		}
		
		// Try to get the editor icon for this component type
		Texture icon = null;
		
		// Special case handling for common UI types
		if (componentType == typeof(Button))
			icon = EditorGUIUtility.IconContent("UnityEditor.AnimationWindow").image;
		else if (componentType == typeof(Image) || componentType == typeof(RawImage))
			icon = EditorGUIUtility.IconContent("Image Icon").image;
		else if (componentType == typeof(Text) || componentType.Name.Contains("Text"))
			icon = EditorGUIUtility.IconContent("TextAsset Icon").image;
		else if (componentType == typeof(InputField) || componentType.Name.Contains("InputField"))
			icon = EditorGUIUtility.IconContent("TextField Icon").image;
		else if (componentType.Name.Contains("Dropdown"))
			icon = EditorGUIUtility.IconContent("PreMatCube").image;
		else if (componentType == typeof(Slider))
			icon = EditorGUIUtility.IconContent("HorizontalSlider").image;
		else if (componentType == typeof(Toggle))
			icon = EditorGUIUtility.IconContent("Toggle").image;
		else if (componentType == typeof(Scrollbar))
			icon = EditorGUIUtility.IconContent("VerticalScrollbar").image;
		else if (componentType == typeof(ScrollRect))
			icon = EditorGUIUtility.IconContent("ScrollRect Icon").image;
		else if (componentType == typeof(Mask) || componentType == typeof(RectMask2D))
			icon = EditorGUIUtility.IconContent("PreMatSphere").image;
		else if (componentType == typeof(Canvas))
			icon = EditorGUIUtility.IconContent("Canvas Icon").image;
		else if (componentType == typeof(RectTransform))
			icon = EditorGUIUtility.IconContent("RectTransform Icon").image;
		else
			icon = EditorGUIUtility.IconContent("UnityEngine/Component Icon").image;
	
		// Cache it for future use
		componentIconCache[componentType] = icon;
		return icon;
	}

	private void FilterUIObjects()
	{
		filteredUiObjectInfos.Clear();
		
		if (string.IsNullOrEmpty(searchText))
		{
			isSearching = false;
			return;
		}
		
		isSearching = true;
		string searchTextLower = searchText.ToLower();
		
		foreach(UIObjectInfo info in uiObjectInfos)
		{
			if (info.RectTransform != null && info.Label.ToLower().Contains(searchTextLower))
			{
				filteredUiObjectInfos.Add(info);
			}
		}
	}

	protected void OnEnable()
	{
		wantsMouseMove = wantsMouseEnterLeaveWindow = true;
#if UNITY_2020_1_OR_NEWER
		wantsLessLayoutEvents = false;
#endif
		blockSceneViewInput = true;
	}

	protected void OnDisable()
	{
		hoveredUIObject = null;
		SceneView.RepaintAll();
	}

	protected void OnGUI()
	{
		Event ev = Event.current;

		// Draw search bar
		Rect searchRect = new Rect(Padding, Padding, position.width - Padding * 2f, SearchBarHeight);
		GUI.Box(searchRect, "", EditorStyles.toolbar);
		
		string newSearchText = EditorGUI.TextField(
			new Rect(searchRect.x + 2, searchRect.y + 2, searchRect.width - 4, searchRect.height - 4), 
			searchText, 
			EditorStyles.toolbarSearchField
		);
		
		if (newSearchText != searchText)
		{
			searchText = newSearchText;
			FilterUIObjects();
			Repaint();
		}

		// Draw result list
		float rowWidth = position.width - Padding * 2f;
		float rowHeight = RowHeight;
		GUIStyle rowGUIStyle = RowGUIStyle;
		int hoveredRowIndex = -1;
		
		// Start the scroll view below the search bar
		Rect scrollViewRect = new Rect(0, SearchBarHeight + Padding * 2f, position.width, position.height - SearchBarHeight - Padding * 2f);
		float contentHeight = (isSearching ? filteredUiObjectInfos.Count : uiObjectInfos.Count) * rowHeight;
		Rect contentRect = new Rect(0, 0, rowWidth, contentHeight);
		
		scrollPosition = GUI.BeginScrollView(scrollViewRect, scrollPosition, contentRect);
		
		List<UIObjectInfo> currentUIObjectInfos = isSearching ? filteredUiObjectInfos : uiObjectInfos;
		
		for (int i = 0; i < currentUIObjectInfos.Count; i++)
		{
			UIObjectInfo info = currentUIObjectInfos[i];
			Rect rect = new Rect(Padding, i * rowHeight, rowWidth - Padding, rowHeight);
			
			// Draw the button background
			bool isSelected = false;
			if (info.RectTransform != null && Selection.activeTransform == info.RectTransform)
			{
				isSelected = true;
			}
			
			if (GUI.Button(rect, "", rowGUIStyle))
			{
				if (info.RectTransform != null)
					Selection.activeTransform = info.RectTransform;

				blockSceneViewInput = false;
				ev.Use();
				Close();
				GUIUtility.ExitGUI();
			}
			
			// Calculate indent position
			float indentPosition = rect.x + info.IndentLevel * IndentWidth;
			
			// Draw the icon at the indented position
			if (info.Content.image != null)
			{
				Rect iconRect = new Rect(
					indentPosition + IconPadding, 
					rect.y + (rect.height - IconSize) * 0.5f, 
					IconSize, 
					IconSize
				);
				GUI.DrawTexture(iconRect, info.Content.image);
			}
			
			// Draw the label next to the icon
			Rect labelRect = new Rect(
				indentPosition + IconSize + IconPadding * 2,
				rect.y,
				rect.width - (indentPosition - rect.x) - IconSize - IconPadding * 2,
				rect.height
			);
			
			Color originalColor = GUI.color;
			if (isSelected)
			{
				GUI.color = new Color(0.6f, 0.8f, 1.0f);
			}
			
			GUI.Label(labelRect, info.Label);
			GUI.color = originalColor;

			if (hoveredRowIndex < 0 && ev.type == EventType.MouseMove && rect.Contains(ev.mousePosition + scrollPosition))
				hoveredRowIndex = i;
		}
		
		GUI.EndScrollView();

		if (ev.type == EventType.MouseMove || ev.type == EventType.MouseLeaveWindow)
		{
			RectTransform hoveredUIObject = (hoveredRowIndex >= 0 && hoveredRowIndex < currentUIObjectInfos.Count) ? 
				currentUIObjectInfos[hoveredRowIndex].RectTransform : null;
				
			if (hoveredUIObject != SceneUISelectionMenu.hoveredUIObject)
			{
				SceneUISelectionMenu.hoveredUIObject = hoveredUIObject;
				Repaint();
				SceneView.RepaintAll();
			}
		}
		
		// Handle keyboard events
		if (ev.type == EventType.KeyDown)
		{
			if (ev.keyCode == KeyCode.Escape)
			{
				blockSceneViewInput = false;
				Close();
				GUIUtility.ExitGUI();
			}
			else if (ev.keyCode == KeyCode.Return && currentUIObjectInfos.Count > 0)
			{
				// Find first item if there's a search
				if (isSearching && currentUIObjectInfos.Count > 0)
				{
					Selection.activeTransform = currentUIObjectInfos[0].RectTransform;
					blockSceneViewInput = false;
					Close();
					GUIUtility.ExitGUI();
				}
			}
		}
	}

	[InitializeOnLoadMethod]
	private static void OnSceneViewGUI()
	{
#if UNITY_2019_1_OR_NEWER
		SceneView.duringSceneGui += ( SceneView sceneView ) =>
#else
		SceneView.onSceneGUIDelegate += ( SceneView sceneView ) =>
#endif
		{
			/// Couldn't get <see cref="EventType.ContextClick"/> to work here in Unity 5.6 so implemented context click detection manually
			Event ev = Event.current;
			switch( ev.type )
			{
				case EventType.MouseDown:
				{
					if( ev.button == 1 )
					{
						lastRightClickTime = EditorApplication.timeSinceStartup;
						lastRightPos = ev.mousePosition;
					}
					else if( blockSceneViewInput )
					{
						// User has clicked outside the context window to close it. Ignore this click in Scene view if it's left click
						blockSceneViewInput = false;

						if( ev.button == 0 )
						{
							GUIUtility.hotControl = 0;
							ev.Use();
						}
					}

					break;
				}
				case EventType.MouseUp:
				{
					if( ev.button == 1 && EditorApplication.timeSinceStartup - lastRightClickTime < 0.2 && ( ev.mousePosition - lastRightPos ).magnitude < 2f )
						OnSceneViewRightClicked( sceneView );

					break;
				}
			}

			if( hoveredUIObject != null )
			{
				hoveredUIObject.GetWorldCorners( hoveredUIObjectCorners );
				Handles.DrawSolidRectangleWithOutline( hoveredUIObjectCorners, HighlightFillColor, HighlightOutlineColor );
			}
		};
	}

	private static void OnSceneViewRightClicked( SceneView sceneView )
	{
		// Find all UI objects under the cursor
		Vector2 pointerPos = HandleUtility.GUIPointToScreenPixelCoordinate( Event.current.mousePosition );
		Entry rootEntry = new Entry( null );
#if UNITY_2018_3_OR_NEWER
		PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
		if( prefabStage != null && prefabStage.stageHandle.IsValid() && prefabStage.prefabContentsRoot.transform is RectTransform prefabStageRoot )
			CheckRectTransformRecursive( prefabStageRoot, pointerPos, sceneView.camera, false, rootEntry.Children );
		else
#endif
		{
#if UNITY_2022_3_OR_NEWER
			Canvas[] canvases = FindObjectsByType<Canvas>( FindObjectsSortMode.None );
#else
			Canvas[] canvases = FindObjectsOfType<Canvas>();
#endif
			Array.Sort( canvases, ( c1, c2 ) => c1.sortingOrder.CompareTo( c2.sortingOrder ) );
			foreach( Canvas canvas in canvases )
			{
				if( canvas != null && canvas.gameObject.activeInHierarchy && canvas.isRootCanvas )
					CheckRectTransformRecursive( (RectTransform) canvas.transform, pointerPos, sceneView.camera, false, rootEntry.Children );
			}
		}

		// Remove non-Graphic root entries with no children from the results
		rootEntry.Children.RemoveAll( ( canvasEntry ) => canvasEntry.Children.Count == 0 && !canvasEntry.RectTransform.GetComponent<Graphic>() );

		// If any results found, show the context window
		if( rootEntry.Children.Count > 0 )
			CreateInstance<SceneUISelectionMenu>().ShowContextWindow( rootEntry.Children );
	}

	private static void CheckRectTransformRecursive( RectTransform rectTransform, Vector2 pointerPos, Camera camera, bool culledByCanvasGroup, List<Entry> result )
	{
		Canvas canvas = rectTransform.GetComponent<Canvas>();
		if( canvas != null && !canvas.enabled )
			return;

		if ( RectTransformUtility.RectangleContainsScreenPoint( rectTransform, pointerPos, camera ) && ShouldCheckRectTransform( rectTransform, pointerPos, camera, ref culledByCanvasGroup ) )
		{
			Entry entry = new Entry( rectTransform );
			result.Add( entry );
			result = entry.Children;
		}

		for( int i = 0, childCount = rectTransform.childCount; i < childCount; i++ )
		{
			RectTransform childRectTransform = rectTransform.GetChild( i ) as RectTransform;
			if( childRectTransform != null && childRectTransform.gameObject.activeSelf )
				CheckRectTransformRecursive( childRectTransform, pointerPos, camera, culledByCanvasGroup, result );
		}
	}

	private static bool ShouldCheckRectTransform( RectTransform rectTransform, Vector2 pointerPos, Camera camera, ref bool culledByCanvasGroup )
	{
#if UNITY_2019_3_OR_NEWER
		if( SceneVisibilityManager.instance.IsHidden( rectTransform.gameObject, false ) )
			return false;

		if( SceneVisibilityManager.instance.IsPickingDisabled( rectTransform.gameObject, false ) )
			return false;
#endif

		CanvasRenderer canvasRenderer = rectTransform.GetComponent<CanvasRenderer>();
		if( canvasRenderer != null && canvasRenderer.cull )
			return false;

		CanvasGroup canvasGroup = rectTransform.GetComponent<CanvasGroup>();
		if( canvasGroup != null )
		{
			if( canvasGroup.ignoreParentGroups )
				culledByCanvasGroup = canvasGroup.alpha == 0f;
			else if( canvasGroup.alpha == 0f )
				culledByCanvasGroup = true;
		}

		if( !culledByCanvasGroup )
		{
			// If the target is a MaskableGraphic that ignores masks (i.e. visible outside masks) and isn't fully transparent, accept it
			MaskableGraphic maskableGraphic = rectTransform.GetComponent<MaskableGraphic>();
			if( maskableGraphic != null && !maskableGraphic.maskable && maskableGraphic.color.a > 0f )
				return true;

			raycastFilters.Clear();
			rectTransform.GetComponentsInParent( false, raycastFilters );
			foreach( var raycastFilter in raycastFilters )
			{
				if( !raycastFilter.IsRaycastLocationValid( pointerPos, camera ) )
					return false;
			}
		}

		return !culledByCanvasGroup;
	}
}