using System.Collections.Generic;
using UnityEngine;
using PhysicsWidgets2D;

/// <summary>
/// Scene-wide runtime debug drawer for Unity 2D physics.
/// Draws collider outlines, Rigidbody2D velocity, conveyor directions,
/// and water-current direction and magnitude.
/// Add one instance to the scene.
/// </summary>
[AddComponentMenu("Debug/Physics Debug Drawer 2D")]
[ExecuteAlways]
public class PhysicsDebugDrawer2D : MonoBehaviour
{
    [Header("Filtering")]
    [SerializeField] private LayerMask layers = ~0;
    [SerializeField] private bool includeInactiveObjects = false;

    [Header("Collider Drawing")]
    [SerializeField] private bool drawColliders = true;
    [SerializeField] private Color solidColliderColor = new Color(0f, 1f, 0f, 0.9f);
    [SerializeField] private Color triggerColliderColor = new Color(0f, 0.8f, 1f, 0.9f);
    [SerializeField] private bool drawTriggers = true;
    [SerializeField] private bool depthTest = false;

    [Header("Rigidbody Drawing")]
    [SerializeField] private bool drawRigidbodies = true;
    [SerializeField] private Color rigidbodyColor = new Color(1f, 0.8f, 0f, 0.95f);
    [SerializeField] private bool drawCenterOfMass = true;
    [SerializeField, Min(0.001f)] private float centerOfMassSize = 0.08f;
    [SerializeField, Tooltip("Scale applied to Rigidbody2D velocity arrows.")]
    private float velocityScale = 0.2f;

    [Header("Conveyor Drawing")]
    [SerializeField] private bool drawConveyorDirections = true;
    [SerializeField] private bool drawBottomConveyorArrow = true;
    [SerializeField] private Color conveyorColor = new Color(1f, 0.25f, 0.1f, 1f);
    [SerializeField, Min(0.05f)] private float conveyorArrowLength = 1f;
    [SerializeField, Min(0f)] private float conveyorArrowInset = 0.06f;
    [SerializeField, Min(0.01f)] private float conveyorArrowHeadSize = 0.16f;

    [Header("Water Current Drawing")]
    [SerializeField] private bool drawWaterCurrents = true;
    [SerializeField] private Color waterCurrentColor = new Color(0.1f, 0.9f, 1f, 1f);

    [Tooltip("Arrow length when the current magnitude is nearly zero.")]
    [SerializeField, Min(0f)]
    private float waterCurrentBaseLength = 0.5f;

    [Tooltip("Additional arrow length per unit of current speed.")]
    [SerializeField, Min(0f)]
    private float waterCurrentMagnitudeScale = 0.5f;

    [SerializeField, Min(0.01f)]
    private float waterCurrentArrowHeadSize = 0.16f;

    [Tooltip(
        "Vertical location of the arrow inside the water volume. " +
        "Zero is the bottom and one is the top.")]
    [SerializeField, Range(0f, 1f)]
    private float waterCurrentVerticalPosition = 0.5f;

    [Tooltip(
        "Current magnitudes at or below this value are treated as zero " +
        "and do not draw an arrow.")]
    [SerializeField, Min(0f)]
    private float minimumWaterCurrentMagnitude = 0.001f;

    [Header("Refresh")]
    [SerializeField, Min(0.01f)] private float updateInterval = 0.05f;
    [SerializeField, Min(0.05f)] private float objectRefreshInterval = 0.5f;

    private float drawTimer;
    private float objectRefreshTimer;

    private Collider2D[] colliders = new Collider2D[0];
    private Rigidbody2D[] rigidbodies = new Rigidbody2D[0];
    private NewConveyorBelt2D[] conveyors = new NewConveyorBelt2D[0];
    private WaterVolume2D[] waterVolumes = new WaterVolume2D[0];

    private readonly Dictionary<Collider2D, ColliderGeometry> geometryCache =
        new Dictionary<Collider2D, ColliderGeometry>();

    private sealed class ColliderGeometry
    {
        public uint ShapeHash;
        public Vector3[] Vertices;
        public BoundaryEdge[] Edges;
        public bool VerticesAreWorldSpace;
    }

    private readonly struct BoundaryEdge
    {
        public readonly int Start;
        public readonly int End;

        public BoundaryEdge(int start, int end)
        {
            Start = start;
            End = end;
        }
    }

    private readonly struct EdgeKey
    {
        public readonly int A;
        public readonly int B;

        public EdgeKey(int a, int b)
        {
            if(a <= b)
            {
                A = a;
                B = b;
            }
            else
            {
                A = b;
                B = a;
            }
        }

        public override bool Equals(object obj)
        {
            return obj is EdgeKey other &&
                   A == other.A &&
                   B == other.B;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (A * 397) ^ B;
            }
        }
    }

    private void OnEnable()
    {
        RefreshObjects();
    }

    private void OnDisable()
    {
        geometryCache.Clear();
    }

    private void OnDestroy()
    {
        geometryCache.Clear();
    }

    private void OnValidate()
    {
        updateInterval = Mathf.Max(0.01f, updateInterval);
        objectRefreshInterval = Mathf.Max(0.05f, objectRefreshInterval);
        centerOfMassSize = Mathf.Max(0.001f, centerOfMassSize);
        conveyorArrowLength = Mathf.Max(0.05f, conveyorArrowLength);
        conveyorArrowInset = Mathf.Max(0f, conveyorArrowInset);
        conveyorArrowHeadSize = Mathf.Max(0.01f, conveyorArrowHeadSize);
        waterCurrentBaseLength = Mathf.Max(0f, waterCurrentBaseLength);
        waterCurrentMagnitudeScale = Mathf.Max(0f, waterCurrentMagnitudeScale);
        waterCurrentArrowHeadSize = Mathf.Max(0.01f, waterCurrentArrowHeadSize);
        minimumWaterCurrentMagnitude = Mathf.Max(0f, minimumWaterCurrentMagnitude);

        RefreshObjects();
        geometryCache.Clear();
    }

    private void Update()
    {
        float deltaTime =
            Application.isPlaying
            ? Time.unscaledDeltaTime
            : Time.deltaTime;

        objectRefreshTimer += deltaTime;

        if(objectRefreshTimer >= objectRefreshInterval)
        {
            objectRefreshTimer = 0f;
            RefreshObjects();
            RemoveDestroyedCacheEntries();
        }

        drawTimer += deltaTime;

        if(drawTimer < updateInterval)
            return;

        drawTimer = 0f;
        DrawAll();
    }

    private void RefreshObjects()
    {
        FindObjectsInactive inactiveMode =
            includeInactiveObjects
            ? FindObjectsInactive.Include
            : FindObjectsInactive.Exclude;

        colliders = FindObjectsByType<Collider2D>(
            inactiveMode,
            FindObjectsSortMode.None);

        rigidbodies = FindObjectsByType<Rigidbody2D>(
            inactiveMode,
            FindObjectsSortMode.None);

        conveyors = FindObjectsByType<NewConveyorBelt2D>(
            inactiveMode,
            FindObjectsSortMode.None);

        waterVolumes = FindObjectsByType<WaterVolume2D>(
            inactiveMode,
            FindObjectsSortMode.None);
    }

    private void DrawAll()
    {
        float duration = Mathf.Max(0.01f, updateInterval * 1.1f);

        if(drawColliders)
            DrawAllColliders(duration);

        if(drawRigidbodies)
            DrawAllRigidbodies(duration);

        if(drawConveyorDirections)
            DrawAllConveyors(duration);

        if(drawWaterCurrents)
            DrawAllWaterCurrents(duration);
    }

    private void DrawAllColliders(float duration)
    {
        for(int i = 0; i < colliders.Length; i++)
        {
            Collider2D collider = colliders[i];

            if(!ShouldDraw(collider) || !collider.enabled)
                continue;

            if(collider.isTrigger && !drawTriggers)
                continue;

            Color color =
                collider.isTrigger
                ? triggerColliderColor
                : solidColliderColor;

            DrawCollider(collider, color, duration);
        }
    }

    private void DrawAllRigidbodies(float duration)
    {
        for(int i = 0; i < rigidbodies.Length; i++)
        {
            Rigidbody2D body = rigidbodies[i];

            if(!ShouldDraw(body))
                continue;

            DrawRigidbody(body, rigidbodyColor, duration);
        }
    }

    private void DrawAllConveyors(float duration)
    {
        for(int i = 0; i < conveyors.Length; i++)
        {
            NewConveyorBelt2D conveyor = conveyors[i];

            if(!ShouldDraw(conveyor) || !conveyor.enabled)
                continue;

            Collider2D collider = conveyor.GetComponent<Collider2D>();

            if(collider == null || !collider.enabled)
                continue;

            DrawConveyor(conveyor, collider, duration);
        }
    }

    private void DrawAllWaterCurrents(float duration)
    {
        for(int i = 0; i < waterVolumes.Length; i++)
        {
            WaterVolume2D water = waterVolumes[i];

            if(!ShouldDraw(water) || !water.enabled)
                continue;

            Collider2D collider = water.GetComponent<Collider2D>();

            if(collider == null || !collider.enabled)
                continue;

            DrawWaterCurrent(water, collider, duration);
        }
    }

    private bool ShouldDraw(Component component)
    {
        if(component == null ||
           !component.gameObject.activeInHierarchy)
        {
            return false;
        }

        return ((1 << component.gameObject.layer) & layers.value) != 0;
    }

    private void DrawCollider(
        Collider2D collider,
        Color color,
        float duration)
    {
        ColliderGeometry geometry = GetOrCreateGeometry(collider);

        if(geometry != null)
        {
            DrawMeshBoundary(collider, geometry, color, duration);
            return;
        }

        if(collider is EdgeCollider2D edge)
        {
            DrawEdgeCollider(edge, color, duration);
            return;
        }

        DrawBounds(collider.bounds, color, duration);
    }

    private ColliderGeometry GetOrCreateGeometry(Collider2D collider)
    {
        uint shapeHash = collider.GetShapeHash();

        if(geometryCache.TryGetValue(
                collider,
                out ColliderGeometry geometry) &&
           geometry.ShapeHash == shapeHash)
        {
            return geometry;
        }

        Mesh mesh = collider.CreateMesh(false, false, true);

        if(mesh == null)
        {
            geometryCache.Remove(collider);
            return null;
        }

        geometry = new ColliderGeometry
        {
            ShapeHash = shapeHash,
            Vertices = mesh.vertices,
            Edges = ExtractBoundaryEdges(mesh.triangles),
            VerticesAreWorldSpace = collider.attachedRigidbody == null
        };

        geometryCache[collider] = geometry;
        DestroyGeneratedMesh(mesh);

        return geometry;
    }

    private static BoundaryEdge[] ExtractBoundaryEdges(int[] triangles)
    {
        Dictionary<EdgeKey, int> edgeCounts =
            new Dictionary<EdgeKey, int>();

        for(int i = 0; i + 2 < triangles.Length; i += 3)
        {
            CountEdge(edgeCounts, triangles[i], triangles[i + 1]);
            CountEdge(edgeCounts, triangles[i + 1], triangles[i + 2]);
            CountEdge(edgeCounts, triangles[i + 2], triangles[i]);
        }

        List<BoundaryEdge> boundaries =
            new List<BoundaryEdge>();

        foreach(KeyValuePair<EdgeKey, int> entry in edgeCounts)
        {
            if(entry.Value == 1)
            {
                boundaries.Add(
                    new BoundaryEdge(
                        entry.Key.A,
                        entry.Key.B));
            }
        }

        return boundaries.ToArray();
    }

    private static void CountEdge(
        Dictionary<EdgeKey, int> edgeCounts,
        int start,
        int end)
    {
        EdgeKey key = new EdgeKey(start, end);

        if(edgeCounts.TryGetValue(key, out int count))
            edgeCounts[key] = count + 1;
        else
            edgeCounts.Add(key, 1);
    }

    private void DrawMeshBoundary(
        Collider2D collider,
        ColliderGeometry geometry,
        Color color,
        float duration)
    {
        Rigidbody2D attachedBody = collider.attachedRigidbody;

        for(int i = 0; i < geometry.Edges.Length; i++)
        {
            BoundaryEdge edge = geometry.Edges[i];

            Vector3 start = geometry.Vertices[edge.Start];
            Vector3 end = geometry.Vertices[edge.End];

            if(!geometry.VerticesAreWorldSpace &&
               attachedBody != null)
            {
                start = attachedBody.transform.TransformPoint(start);
                end = attachedBody.transform.TransformPoint(end);
            }

            Debug.DrawLine(
                start,
                end,
                color,
                duration,
                depthTest);
        }
    }

    private void DrawEdgeCollider(
        EdgeCollider2D edge,
        Color color,
        float duration)
    {
        Vector2[] points = edge.points;
        Transform edgeTransform = edge.transform;

        for(int i = 0; i + 1 < points.Length; i++)
        {
            Vector3 start =
                edgeTransform.TransformPoint(
                    edge.offset + points[i]);

            Vector3 end =
                edgeTransform.TransformPoint(
                    edge.offset + points[i + 1]);

            Debug.DrawLine(
                start,
                end,
                color,
                duration,
                depthTest);
        }
    }

    private void DrawBounds(
        Bounds bounds,
        Color color,
        float duration)
    {
        Vector3 bottomLeft =
            new Vector3(bounds.min.x, bounds.min.y, bounds.center.z);

        Vector3 bottomRight =
            new Vector3(bounds.max.x, bounds.min.y, bounds.center.z);

        Vector3 topRight =
            new Vector3(bounds.max.x, bounds.max.y, bounds.center.z);

        Vector3 topLeft =
            new Vector3(bounds.min.x, bounds.max.y, bounds.center.z);

        Debug.DrawLine(bottomLeft, bottomRight, color, duration, depthTest);
        Debug.DrawLine(bottomRight, topRight, color, duration, depthTest);
        Debug.DrawLine(topRight, topLeft, color, duration, depthTest);
        Debug.DrawLine(topLeft, bottomLeft, color, duration, depthTest);
    }

    private void DrawRigidbody(
        Rigidbody2D body,
        Color color,
        float duration)
    {
        Vector3 origin = body.worldCenterOfMass;

        Vector3 velocityEnd =
            origin +
            (Vector3)(body.linearVelocity * velocityScale);

        Debug.DrawLine(
            origin,
            velocityEnd,
            color,
            duration,
            depthTest);

        DrawArrowHead(
            velocityEnd,
            velocityEnd - origin,
            centerOfMassSize,
            color,
            duration);

        if(drawCenterOfMass)
        {
            DrawCross(
                origin,
                centerOfMassSize,
                color,
                duration);
        }
    }

    private void DrawConveyor(
        NewConveyorBelt2D conveyor,
        Collider2D collider,
        float duration)
    {
        Vector2 surfaceVelocity = conveyor.SurfaceVelocity;

        if(surfaceVelocity.sqrMagnitude < 0.000001f)
            return;

        Vector2 direction = surfaceVelocity.normalized;
        Vector2 topNormal = new Vector2(-direction.y, direction.x);

        if(Vector2.Dot(topNormal, conveyor.transform.up) < 0f)
            topNormal = -topNormal;

        Vector2 center = collider.bounds.center;
        float searchDistance = collider.bounds.extents.magnitude + 1f;

        Vector2 topSurface =
            collider.ClosestPoint(
                center + topNormal * searchDistance);

        Vector2 bottomSurface =
            collider.ClosestPoint(
                center - topNormal * searchDistance);

        Vector2 topCenter =
            topSurface -
            topNormal * conveyorArrowInset;

        DrawCenteredArrow(
            topCenter,
            direction,
            conveyorArrowLength,
            conveyorArrowHeadSize,
            conveyorColor,
            duration);

        if(drawBottomConveyorArrow)
        {
            Vector2 bottomCenter =
                bottomSurface +
                topNormal * conveyorArrowInset;

            DrawCenteredArrow(
                bottomCenter,
                -direction,
                conveyorArrowLength,
                conveyorArrowHeadSize,
                conveyorColor,
                duration);
        }
    }

    private void DrawWaterCurrent(
        WaterVolume2D water,
        Collider2D collider,
        float duration)
    {
        float speed = water.HorizontalCurrentSpeed;
        float magnitude = Mathf.Abs(speed);

        if(magnitude <= minimumWaterCurrentMagnitude)
            return;

        Bounds bounds = collider.bounds;

        Vector2 center =
            new Vector2(
                bounds.center.x,
                Mathf.Lerp(
                    bounds.min.y,
                    bounds.max.y,
                    waterCurrentVerticalPosition));

        Vector2 direction =
            speed > 0f
            ? Vector2.right
            : Vector2.left;

        float length =
            waterCurrentBaseLength +
            magnitude *
            waterCurrentMagnitudeScale;

        DrawCenteredArrow(
            center,
            direction,
            length,
            waterCurrentArrowHeadSize,
            waterCurrentColor,
            duration);
    }

    private void DrawCenteredArrow(
        Vector2 center,
        Vector2 direction,
        float length,
        float headSize,
        Color color,
        float duration)
    {
        Vector2 halfArrow = direction.normalized * length * 0.5f;
        Vector2 start = center - halfArrow;
        Vector2 end = center + halfArrow;

        Debug.DrawLine(
            start,
            end,
            color,
            duration,
            depthTest);

        DrawArrowHead(
            end,
            direction,
            headSize,
            color,
            duration);
    }

    private void DrawArrowHead(
        Vector3 end,
        Vector3 direction,
        float size,
        Color color,
        float duration)
    {
        if(direction.sqrMagnitude < 0.000001f)
            return;

        direction.Normalize();

        Vector3 right =
            Quaternion.Euler(0f, 0f, 150f) *
            direction *
            size;

        Vector3 left =
            Quaternion.Euler(0f, 0f, -150f) *
            direction *
            size;

        Debug.DrawLine(
            end,
            end + right,
            color,
            duration,
            depthTest);

        Debug.DrawLine(
            end,
            end + left,
            color,
            duration,
            depthTest);
    }

    private void DrawCross(
        Vector3 center,
        float size,
        Color color,
        float duration)
    {
        Debug.DrawLine(
            center + Vector3.left * size,
            center + Vector3.right * size,
            color,
            duration,
            depthTest);

        Debug.DrawLine(
            center + Vector3.down * size,
            center + Vector3.up * size,
            color,
            duration,
            depthTest);
    }

    private void RemoveDestroyedCacheEntries()
    {
        if(geometryCache.Count == 0)
            return;

        List<Collider2D> destroyed = null;

        foreach(
            KeyValuePair<Collider2D, ColliderGeometry> entry
            in geometryCache)
        {
            if(entry.Key != null)
                continue;

            destroyed ??= new List<Collider2D>();
            destroyed.Add(entry.Key);
        }

        if(destroyed == null)
            return;

        for(int i = 0; i < destroyed.Count; i++)
            geometryCache.Remove(destroyed[i]);
    }

    private static void DestroyGeneratedMesh(Mesh mesh)
    {
        if(mesh == null)
            return;

        if(Application.isPlaying)
            Object.Destroy(mesh);
        else
            Object.DestroyImmediate(mesh);
    }
}