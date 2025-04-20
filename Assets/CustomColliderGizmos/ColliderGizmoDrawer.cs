using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

// Extension methods must be in a non-generic static class
public static class HandleExtensions
{
    // Draw a mesh wireframe
    public static void DrawWireFrame(this Handles handles, Mesh mesh)
    {
        int[] triangles = mesh.triangles;
        Vector3[] vertices = mesh.vertices;
        
        for (int i = 0; i < triangles.Length; i += 3)
        {
            Vector3 v1 = vertices[triangles[i]];
            Vector3 v2 = vertices[triangles[i + 1]];
            Vector3 v3 = vertices[triangles[i + 2]];
            
            Handles.DrawLine(v1, v2);
            Handles.DrawLine(v2, v3);
            Handles.DrawLine(v3, v1);
        }
    }
}

[InitializeOnLoad]
public class ColliderGizmoDrawer
{
    static ColliderGizmoDrawer()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        EditorApplication.update += OnEditorUpdate;
    }

    private static void OnEditorUpdate()
    {
        // This will force scene views to repaint, ensuring our gizmos update even in prefab mode
        foreach (SceneView sceneView in SceneView.sceneViews)
        {
            sceneView.Repaint();
        }
    }

    private static void OnSceneGUI(SceneView sceneView)
    {
        if (Event.current.type != EventType.Repaint)
            return;

        ColliderGizmoSettings settings = ColliderGizmoSettings.Instance;
        
        if (!settings.showTriggers && !settings.showNormalColliders)
            return;

        // Process scene and prefab colliders
        ProcessAllColliders(settings);
    }

    private static void ProcessAllColliders(ColliderGizmoSettings settings)
    {
        // Get the selected objects (works in both scene and prefab modes)
        Object[] selectedObjects = Selection.objects;
        List<GameObject> processedRoots = new List<GameObject>();

        // Process each selected object and its children
        foreach (Object selectedObject in selectedObjects)
        {
            GameObject selectedGameObject = selectedObject as GameObject;
            if (selectedGameObject != null)
            {
                processedRoots.Add(selectedGameObject);

                // Process all 3D colliders in this GameObject and its children
                ProcessCollidersInHierarchy<BoxCollider>(selectedGameObject, settings);
                ProcessCollidersInHierarchy<SphereCollider>(selectedGameObject, settings);
                ProcessCollidersInHierarchy<CapsuleCollider>(selectedGameObject, settings);
                ProcessCollidersInHierarchy<MeshCollider>(selectedGameObject, settings);

                // Process all 2D colliders in this GameObject and its children
                ProcessCollidersInHierarchy<BoxCollider2D>(selectedGameObject, settings);
                ProcessCollidersInHierarchy<CircleCollider2D>(selectedGameObject, settings);
                ProcessCollidersInHierarchy<CapsuleCollider2D>(selectedGameObject, settings);
                ProcessCollidersInHierarchy<PolygonCollider2D>(selectedGameObject, settings);
                ProcessCollidersInHierarchy<EdgeCollider2D>(selectedGameObject, settings);
            }
        }

        // If nothing is selected or settings allow showing all colliders, process everything in the scene
        if (selectedObjects.Length == 0 || !settings.showOnlyWhenSelected)
        {
            // 3D Colliders
            DrawColliders3D<BoxCollider>(settings, processedRoots);
            DrawColliders3D<SphereCollider>(settings, processedRoots);
            DrawColliders3D<CapsuleCollider>(settings, processedRoots);
            DrawColliders3D<MeshCollider>(settings, processedRoots);

            // 2D Colliders
            DrawColliders2D<BoxCollider2D>(settings, processedRoots);
            DrawColliders2D<CircleCollider2D>(settings, processedRoots);
            DrawColliders2D<CapsuleCollider2D>(settings, processedRoots);
            DrawColliders2D<PolygonCollider2D>(settings, processedRoots);
            DrawColliders2D<EdgeCollider2D>(settings, processedRoots);
        }
    }

    private static void ProcessCollidersInHierarchy<T>(GameObject root, ColliderGizmoSettings settings) where T : Component
    {
        T[] colliders = root.GetComponentsInChildren<T>(true);
        foreach (T component in colliders)
        {
            if (component is Collider collider3D)
            {
                // Skip triggers if not showing triggers
                if (collider3D.isTrigger && !settings.showTriggers)
                    continue;

                // Skip non-triggers if not showing normal colliders
                if (!collider3D.isTrigger && !settings.showNormalColliders)
                    continue;

                // Get color based on trigger state
                Color color = collider3D.isTrigger ? settings.triggerColliderColor : settings.normalColliderColor;
                Handles.color = color;

                // Draw custom gizmo based on collider type
                if (collider3D is BoxCollider boxCollider)
                {
                    DrawBoxCollider(boxCollider, settings);
                }
                else if (collider3D is SphereCollider sphereCollider)
                {
                    DrawSphereCollider(sphereCollider, settings);
                }
                else if (collider3D is CapsuleCollider capsuleCollider)
                {
                    DrawCapsuleCollider(capsuleCollider, settings);
                }
                else if (collider3D is MeshCollider meshCollider)
                {
                    DrawMeshCollider(meshCollider, settings);
                }
            }
            else if (component is Collider2D collider2D)
            {
                // Skip triggers if not showing triggers
                if (collider2D.isTrigger && !settings.showTriggers)
                    continue;

                // Skip non-triggers if not showing normal colliders
                if (!collider2D.isTrigger && !settings.showNormalColliders)
                    continue;

                // Get color based on trigger state
                Color color = collider2D.isTrigger ? settings.triggerColliderColor : settings.normalColliderColor;
                Handles.color = color;

                // Draw custom gizmo based on collider type
                if (collider2D is BoxCollider2D boxCollider)
                {
                    DrawBoxCollider2D(boxCollider, settings);
                }
                else if (collider2D is CircleCollider2D circleCollider)
                {
                    DrawCircleCollider2D(circleCollider, settings);
                }
                else if (collider2D is CapsuleCollider2D capsuleCollider)
                {
                    DrawCapsuleCollider2D(capsuleCollider, settings);
                }
                else if (collider2D is PolygonCollider2D polygonCollider)
                {
                    DrawPolygonCollider2D(polygonCollider, settings);
                }
                else if (collider2D is EdgeCollider2D edgeCollider)
                {
                    DrawEdgeCollider2D(edgeCollider, settings);
                }
            }
        }
    }

    private static void DrawColliders3D<T>(ColliderGizmoSettings settings, List<GameObject> excludeRoots) where T : Collider
    {
        T[] colliders = Object.FindObjectsOfType<T>();
        foreach (T collider in colliders)
        {
            // Skip if this collider belongs to a GameObject that's already been processed
            if (IsInExcludedHierarchy(collider.gameObject, excludeRoots))
                continue;

            // Skip drawing if collider's gameobject is not selected and showOnlyWhenSelected is true
            if (settings.showOnlyWhenSelected && !IsObjectOrParentSelected(collider.gameObject))
                continue;

            // Skip triggers if not showing triggers
            if (collider.isTrigger && !settings.showTriggers)
                continue;
                
            // Skip non-triggers if not showing normal colliders
            if (!collider.isTrigger && !settings.showNormalColliders)
                continue;

            // Get color based on trigger state
            Color color = collider.isTrigger ? settings.triggerColliderColor : settings.normalColliderColor;
            Handles.color = color;

            // Draw custom gizmo based on collider type
            if (collider is BoxCollider boxCollider)
            {
                DrawBoxCollider(boxCollider, settings);
            }
            else if (collider is SphereCollider sphereCollider)
            {
                DrawSphereCollider(sphereCollider, settings);
            }
            else if (collider is CapsuleCollider capsuleCollider)
            {
                DrawCapsuleCollider(capsuleCollider, settings);
            }
            else if (collider is MeshCollider meshCollider)
            {
                DrawMeshCollider(meshCollider, settings);
            }
        }
    }

    private static void DrawColliders2D<T>(ColliderGizmoSettings settings, List<GameObject> excludeRoots) where T : Collider2D
    {
        T[] colliders = Object.FindObjectsOfType<T>();
        foreach (T collider in colliders)
        {
            // Skip if this collider belongs to a GameObject that's already been processed
            if (IsInExcludedHierarchy(collider.gameObject, excludeRoots))
                continue;

            // Skip drawing if collider's gameobject is not selected and showOnlyWhenSelected is true
            if (settings.showOnlyWhenSelected && !IsObjectOrParentSelected(collider.gameObject))
                continue;

            // Skip triggers if not showing triggers
            if (collider.isTrigger && !settings.showTriggers)
                continue;
                
            // Skip non-triggers if not showing normal colliders
            if (!collider.isTrigger && !settings.showNormalColliders)
                continue;

            // Get color based on trigger state
            Color color = collider.isTrigger ? settings.triggerColliderColor : settings.normalColliderColor;
            Handles.color = color;

            // Draw custom gizmo based on collider type
            if (collider is BoxCollider2D boxCollider)
            {
                DrawBoxCollider2D(boxCollider, settings);
            }
            else if (collider is CircleCollider2D circleCollider)
            {
                DrawCircleCollider2D(circleCollider, settings);
            }
            else if (collider is CapsuleCollider2D capsuleCollider)
            {
                DrawCapsuleCollider2D(capsuleCollider, settings);
            }
            else if (collider is PolygonCollider2D polygonCollider)
            {
                DrawPolygonCollider2D(polygonCollider, settings);
            }
            else if (collider is EdgeCollider2D edgeCollider)
            {
                DrawEdgeCollider2D(edgeCollider, settings);
            }
        }
    }

    // Helper to check if an object is in a hierarchy that we have already processed
    private static bool IsInExcludedHierarchy(GameObject obj, List<GameObject> excludeRoots)
    {
        foreach (GameObject root in excludeRoots)
        {
            if (obj == root || IsChildOf(obj.transform, root.transform))
                return true;
        }
        return false;
    }

    // Helper to check if a transform is a child of another transform
    private static bool IsChildOf(Transform child, Transform parent)
    {
        Transform current = child;
        while (current != null)
        {
            if (current == parent)
                return true;
            current = current.parent;
        }
        return false;
    }

    // Helper to check if an object or any of its parents is selected
    private static bool IsObjectOrParentSelected(GameObject obj)
    {
        Transform current = obj.transform;
        while (current != null)
        {
            if (Selection.Contains(current.gameObject))
                return true;
            current = current.parent;
        }
        return false;
    }

    // 3D Collider drawing methods
    private static void DrawBoxCollider(BoxCollider collider, ColliderGizmoSettings settings)
    {
        Matrix4x4 matrix = Matrix4x4.TRS(
            collider.transform.TransformPoint(collider.center),
            collider.transform.rotation,
            Vector3.Scale(collider.transform.lossyScale, collider.size)
        );

        using (new Handles.DrawingScope(matrix))
        {
            if (settings.showWireframe)
                Handles.DrawWireCube(Vector3.zero, Vector3.one);
                
            if (settings.showFilled)
            {
                Color color = Handles.color;
                color.a *= 0.3f; // Make fill slightly more transparent
                Handles.color = color;
                
                // Draw the filled cube sides
                Vector3[] points = new Vector3[8];
                points[0] = new Vector3(-0.5f, -0.5f, -0.5f);
                points[1] = new Vector3(0.5f, -0.5f, -0.5f);
                points[2] = new Vector3(0.5f, -0.5f, 0.5f);
                points[3] = new Vector3(-0.5f, -0.5f, 0.5f);
                points[4] = new Vector3(-0.5f, 0.5f, -0.5f);
                points[5] = new Vector3(0.5f, 0.5f, -0.5f);
                points[6] = new Vector3(0.5f, 0.5f, 0.5f);
                points[7] = new Vector3(-0.5f, 0.5f, 0.5f);
                
                // Bottom
                Handles.DrawSolidRectangleWithOutline(new Vector3[] { points[0], points[1], points[2], points[3] }, color, Color.clear);
                // Top
                Handles.DrawSolidRectangleWithOutline(new Vector3[] { points[7], points[6], points[5], points[4] }, color, Color.clear);
                // Left
                Handles.DrawSolidRectangleWithOutline(new Vector3[] { points[0], points[3], points[7], points[4] }, color, Color.clear);
                // Right
                Handles.DrawSolidRectangleWithOutline(new Vector3[] { points[1], points[5], points[6], points[2] }, color, Color.clear);
                // Front
                Handles.DrawSolidRectangleWithOutline(new Vector3[] { points[0], points[4], points[5], points[1] }, color, Color.clear);
                // Back
                Handles.DrawSolidRectangleWithOutline(new Vector3[] { points[3], points[2], points[6], points[7] }, color, Color.clear);
            }
        }
    }

    private static void DrawSphereCollider(SphereCollider collider, ColliderGizmoSettings settings)
    {
        Vector3 position = collider.transform.TransformPoint(collider.center);
        float radius = collider.radius * Mathf.Max(
            Mathf.Abs(collider.transform.lossyScale.x),
            Mathf.Abs(collider.transform.lossyScale.y),
            Mathf.Abs(collider.transform.lossyScale.z)
        );

        if (settings.showWireframe)
        {
            Handles.DrawWireDisc(position, Vector3.up, radius);
            Handles.DrawWireDisc(position, Vector3.right, radius);
            Handles.DrawWireDisc(position, Vector3.forward, radius);
        }
        
        if (settings.showFilled)
        {
            Color color = Handles.color;
            color.a *= 0.3f; // Make fill slightly more transparent
            Handles.color = color;
            
            Handles.DrawSolidDisc(position, Vector3.up, radius);
            Handles.DrawSolidDisc(position, Vector3.right, radius);
            Handles.DrawSolidDisc(position, Vector3.forward, radius);
        }
    }

    private static void DrawCapsuleCollider(CapsuleCollider collider, ColliderGizmoSettings settings)
    {
        Vector3 center = collider.transform.TransformPoint(collider.center);
        Vector3 direction = Vector3.zero;
        
        switch (collider.direction)
        {
            case 0: direction = collider.transform.right; break;
            case 1: direction = collider.transform.up; break;
            case 2: direction = collider.transform.forward; break;
        }
        
        float radius = collider.radius * Mathf.Max(
            collider.direction == 0 ? 0 : Mathf.Abs(collider.transform.lossyScale.x),
            collider.direction == 1 ? 0 : Mathf.Abs(collider.transform.lossyScale.y),
            collider.direction == 2 ? 0 : Mathf.Abs(collider.transform.lossyScale.z)
        );
        
        float height = collider.height * Mathf.Abs(
            collider.direction == 0 ? collider.transform.lossyScale.x :
            collider.direction == 1 ? collider.transform.lossyScale.y :
            collider.transform.lossyScale.z
        );
        
        // Calculate the endpoints of the capsule
        Vector3 p1 = center - direction * (height * 0.5f - radius);
        Vector3 p2 = center + direction * (height * 0.5f - radius);
        
        // Draw the end caps
        Quaternion rotationPlaneX = Quaternion.FromToRotation(Vector3.up, direction);
        Quaternion rotationPlaneY = Quaternion.FromToRotation(Vector3.forward, direction);
        
        if (settings.showWireframe)
        {
            Handles.DrawWireDisc(p1, direction, radius);
            Handles.DrawWireDisc(p2, direction, radius);
            
            // Draw the connecting lines
            Handles.DrawWireArc(p1, rotationPlaneX * Vector3.right, rotationPlaneX * Vector3.back, 180, radius);
            Handles.DrawWireArc(p1, rotationPlaneX * Vector3.left, rotationPlaneX * Vector3.back, 180, radius);
            Handles.DrawWireArc(p2, rotationPlaneX * Vector3.right, rotationPlaneX * Vector3.forward, 180, radius);
            Handles.DrawWireArc(p2, rotationPlaneX * Vector3.left, rotationPlaneX * Vector3.forward, 180, radius);
            
            Handles.DrawLine(p1 + rotationPlaneX * Vector3.right * radius, p2 + rotationPlaneX * Vector3.right * radius);
            Handles.DrawLine(p1 + rotationPlaneX * Vector3.left * radius, p2 + rotationPlaneX * Vector3.left * radius);
            Handles.DrawLine(p1 + rotationPlaneY * Vector3.up * radius, p2 + rotationPlaneY * Vector3.up * radius);
            Handles.DrawLine(p1 + rotationPlaneY * Vector3.down * radius, p2 + rotationPlaneY * Vector3.down * radius);
        }
        
        if (settings.showFilled)
        {
            Color color = Handles.color;
            color.a *= 0.3f; // Make fill slightly more transparent
            Handles.color = color;
            
            Handles.DrawSolidDisc(p1, direction, radius);
            Handles.DrawSolidDisc(p2, direction, radius);
        }
    }

    private static void DrawMeshCollider(MeshCollider collider, ColliderGizmoSettings settings)
    {
        if (collider.sharedMesh == null)
            return;

        Mesh mesh = collider.sharedMesh;
        Handles.matrix = collider.transform.localToWorldMatrix;
        
        if (settings.showWireframe)
        {
            // Draw mesh wireframe manually
            int[] triangles = mesh.triangles;
            Vector3[] vertices = mesh.vertices;
            
            for (int i = 0; i < triangles.Length; i += 3)
            {
                if (i + 2 < triangles.Length)
                {
                    Vector3 v1 = vertices[triangles[i]];
                    Vector3 v2 = vertices[triangles[i + 1]];
                    Vector3 v3 = vertices[triangles[i + 2]];
                    
                    Handles.DrawLine(v1, v2);
                    Handles.DrawLine(v2, v3);
                    Handles.DrawLine(v3, v1);
                }
            }
        }
            
        if (settings.showFilled)
        {
            Color color = Handles.color;
            color.a *= 0.2f; // Make mesh fill very transparent
            Handles.color = color;
            
            // Only show filled if collider is convex (for performance)
            if (collider.convex)
            {
                // Draw convex polygon
                Vector3[] vertices = mesh.vertices;
                if (vertices.Length > 0)
                {
                    // For simplicity, we'll just draw a few faces if the mesh is convex
                    // For a better representation, a convex hull algorithm would be needed
                    for (int i = 0; i < mesh.triangles.Length; i += 3)
                    {
                        if (i + 2 < mesh.triangles.Length)
                        {
                            Vector3[] points = new Vector3[3];
                            points[0] = vertices[mesh.triangles[i]];
                            points[1] = vertices[mesh.triangles[i + 1]];
                            points[2] = vertices[mesh.triangles[i + 2]];
                            
                            Handles.DrawAAConvexPolygon(points);
                        }
                    }
                }
            }
        }
    }

    // 2D Collider drawing methods
    private static void DrawBoxCollider2D(BoxCollider2D collider, ColliderGizmoSettings settings)
    {
        Vector3 position = collider.transform.TransformPoint(collider.offset);
        Vector3 size = new Vector3(
            collider.size.x * collider.transform.lossyScale.x,
            collider.size.y * collider.transform.lossyScale.y,
            0.01f
        );

        Matrix4x4 matrix = Matrix4x4.TRS(position, collider.transform.rotation, Vector3.one);
        using (new Handles.DrawingScope(matrix))
        {
            Vector3 topLeft = new Vector3(-size.x / 2, size.y / 2, 0);
            Vector3 topRight = new Vector3(size.x / 2, size.y / 2, 0);
            Vector3 bottomLeft = new Vector3(-size.x / 2, -size.y / 2, 0);
            Vector3 bottomRight = new Vector3(size.x / 2, -size.y / 2, 0);

            if (settings.showWireframe)
            {
                Handles.DrawLine(topLeft, topRight);
                Handles.DrawLine(topRight, bottomRight);
                Handles.DrawLine(bottomRight, bottomLeft);
                Handles.DrawLine(bottomLeft, topLeft);
            }
            
            if (settings.showFilled)
            {
                Color color = Handles.color;
                color.a *= 0.3f; // Make fill slightly more transparent
                Handles.color = color;
                
                Handles.DrawSolidRectangleWithOutline(
                    new Vector3[] { bottomLeft, bottomRight, topRight, topLeft }, 
                    color, Color.clear);
            }
        }
    }

    private static void DrawCircleCollider2D(CircleCollider2D collider, ColliderGizmoSettings settings)
    {
        Vector3 position = collider.transform.TransformPoint(collider.offset);
        float radius = collider.radius * Mathf.Max(
            Mathf.Abs(collider.transform.lossyScale.x),
            Mathf.Abs(collider.transform.lossyScale.y)
        );

        if (settings.showWireframe)
            Handles.DrawWireDisc(position, Vector3.forward, radius);
            
        if (settings.showFilled)
        {
            Color color = Handles.color;
            color.a *= 0.3f; // Make fill slightly more transparent
            Handles.color = color;
            
            Handles.DrawSolidDisc(position, Vector3.forward, radius);
        }
    }

    private static void DrawCapsuleCollider2D(CapsuleCollider2D collider, ColliderGizmoSettings settings)
    {
        Vector3 position = collider.transform.TransformPoint(collider.offset);
        Vector2 size = Vector2.Scale(collider.size, collider.transform.lossyScale);
        float radius;
        Vector3 topCenter, bottomCenter;

        if (collider.direction == CapsuleDirection2D.Vertical)
        {
            radius = size.x * 0.5f;
            float height = Mathf.Max(0, size.y - size.x);
            topCenter = position + new Vector3(0, height * 0.5f, 0);
            bottomCenter = position - new Vector3(0, height * 0.5f, 0);
        }
        else
        {
            radius = size.y * 0.5f;
            float width = Mathf.Max(0, size.x - size.y);
            topCenter = position + new Vector3(width * 0.5f, 0, 0);
            bottomCenter = position - new Vector3(width * 0.5f, 0, 0);
        }

        if (settings.showWireframe)
        {
            Handles.DrawWireDisc(topCenter, Vector3.forward, radius);
            Handles.DrawWireDisc(bottomCenter, Vector3.forward, radius);

            if (collider.direction == CapsuleDirection2D.Vertical)
            {
                Handles.DrawLine(topCenter + new Vector3(-radius, 0, 0), bottomCenter + new Vector3(-radius, 0, 0));
                Handles.DrawLine(topCenter + new Vector3(radius, 0, 0), bottomCenter + new Vector3(radius, 0, 0));
            }
            else
            {
                Handles.DrawLine(topCenter + new Vector3(0, -radius, 0), bottomCenter + new Vector3(0, -radius, 0));
                Handles.DrawLine(topCenter + new Vector3(0, radius, 0), bottomCenter + new Vector3(0, radius, 0));
            }
        }
        
        if (settings.showFilled)
        {
            Color color = Handles.color;
            color.a *= 0.3f; // Make fill slightly more transparent
            Handles.color = color;
            
            Handles.DrawSolidDisc(topCenter, Vector3.forward, radius);
            Handles.DrawSolidDisc(bottomCenter, Vector3.forward, radius);
            
            if (collider.direction == CapsuleDirection2D.Vertical)
            {
                Vector3 p1 = topCenter + new Vector3(-radius, 0, 0);
                Vector3 p2 = bottomCenter + new Vector3(-radius, 0, 0);
                Vector3 p3 = bottomCenter + new Vector3(radius, 0, 0);
                Vector3 p4 = topCenter + new Vector3(radius, 0, 0);
                
                Handles.DrawSolidRectangleWithOutline(new Vector3[] { p1, p2, p3, p4 }, color, Color.clear);
            }
            else
            {
                Vector3 p1 = topCenter + new Vector3(0, radius, 0);
                Vector3 p2 = topCenter + new Vector3(0, -radius, 0);
                Vector3 p3 = bottomCenter + new Vector3(0, -radius, 0);
                Vector3 p4 = bottomCenter + new Vector3(0, radius, 0);
                
                Handles.DrawSolidRectangleWithOutline(new Vector3[] { p1, p2, p3, p4 }, color, Color.clear);
            }
        }
    }

    private static void DrawPolygonCollider2D(PolygonCollider2D collider, ColliderGizmoSettings settings)
    {
        for (int pathIndex = 0; pathIndex < collider.pathCount; pathIndex++)
        {
            Vector2[] points = collider.GetPath(pathIndex);
            if (points.Length < 2)
                continue;

            Vector3[] worldPoints = new Vector3[points.Length];
            
            for (int i = 0; i < points.Length; i++)
            {
                Vector2 point = points[i];
                worldPoints[i] = collider.transform.TransformPoint(point + collider.offset);
            }
            
            if (settings.showWireframe)
            {
                for (int i = 0; i < worldPoints.Length; i++)
                {
                    Vector3 worldPoint = worldPoints[i];
                    Vector3 worldNextPoint = worldPoints[(i + 1) % worldPoints.Length];
                    
                    Handles.DrawLine(worldPoint, worldNextPoint);
                }
            }
            
            if (settings.showFilled && worldPoints.Length > 2)
            {
                Color color = Handles.color;
                color.a *= 0.3f; // Make fill slightly more transparent
                Handles.color = color;
                
                Handles.DrawSolidRectangleWithOutline(worldPoints, color, Color.clear);
            }
        }
    }

    private static void DrawEdgeCollider2D(EdgeCollider2D collider, ColliderGizmoSettings settings)
    {
        Vector2[] points = collider.points;
        if (points.Length < 2)
            return;

        if (settings.showWireframe)
        {
            for (int i = 0; i < points.Length - 1; i++)
            {
                Vector2 point = points[i];
                Vector2 nextPoint = points[i + 1];
                
                Vector3 worldPoint = collider.transform.TransformPoint(point + collider.offset);
                Vector3 worldNextPoint = collider.transform.TransformPoint(nextPoint + collider.offset);
                
                Handles.DrawLine(worldPoint, worldNextPoint);
            }
        }
    }
} 