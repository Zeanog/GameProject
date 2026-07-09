using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime debug drawer for 2D physics objects.
/// - Draws outlines for common Collider2D types (Box, Circle, Capsule, Polygon, Edge, Composite).
/// - Draws velocity arrows for Rigidbody2D.
/// Designed for quick visual debugging in Play mode. Tweak colors, layer mask and update interval.
/// </summary>
[AddComponentMenu("Debug/Physics Debug Drawer 2D")]
[ExecuteAlways]
public class PhysicsDebugDrawer2D : MonoBehaviour
{
    [SerializeField] LayerMask layers = ~0;
    [SerializeField] bool drawColliders = true;
    [SerializeField] Color colliderColor = new Color(0f, 1f, 0f, 0.9f);
    [SerializeField] bool drawRigidbodies = true;
    [SerializeField] Color rigidbodyColor = new Color(1f, 0.8f, 0f, 0.95f);
    [SerializeField, Tooltip("Scale applied to velocity when drawing arrows")]
    float velocityScale = 0.2f;
    [SerializeField, Tooltip("Seconds between debug redraws (low cost if > 0).")]
    float updateInterval = 0.05f;
    [SerializeField, Range(8, 64)] int circleSegments = 24;

    float _timer = 0f;

    void Update()
    {
        // Keep light-weight: redraw only every updateInterval seconds
        _timer += Application.isPlaying ? Time.unscaledDeltaTime : Time.deltaTime;
        if (_timer >= Mathf.Max(0.0001f, updateInterval))
        {
            _timer = 0f;
            DrawAll();
        }
    }

    void DrawAll()
    {
        if (drawColliders)
        {
            var colliders = Object.FindObjectsOfType<Collider2D>();
            for (int i = 0; i < colliders.Length; ++i)
            {
                var c = colliders[i];
                if (((1 << c.gameObject.layer) & layers) == 0)
                    continue;

                DrawCollider(c, colliderColor);
            }
        }

        if (drawRigidbodies)
        {
            var bodies = Object.FindObjectsOfType<Rigidbody2D>();
            for (int i = 0; i < bodies.Length; ++i)
            {
                var rb = bodies[i];
                if (((1 << rb.gameObject.layer) & layers) == 0)
                    continue;

                DrawRigidbody(rb, rigidbodyColor);
            }
        }
    }

    void DrawCollider(Collider2D c, Color col)
    {
        // Use short lifetime so lines refresh - using updateInterval * 0.9f keeps them visible between updates
        float duration = Mathf.Max(0.01f, updateInterval * 0.9f);

        if (c is BoxCollider2D box)
        {
            Vector2 size = Vector2.Scale(box.size, box.transform.lossyScale);
            Vector2 offset = box.offset;
            var t = box.transform;
            Vector3[] corners = new Vector3[4];
            corners[0] = t.TransformPoint(offset + new Vector2(-size.x, -size.y) * 0.5f);
            corners[1] = t.TransformPoint(offset + new Vector2(size.x, -size.y) * 0.5f);
            corners[2] = t.TransformPoint(offset + new Vector2(size.x, size.y) * 0.5f);
            corners[3] = t.TransformPoint(offset + new Vector2(-size.x, size.y) * 0.5f);
            DrawLoop(corners, col, duration);
        }
        else if (c is CircleCollider2D circ)
        {
            Vector2 offset = circ.offset;
            var t = circ.transform;
            float scale = Mathf.Max(Mathf.Abs(t.lossyScale.x), Mathf.Abs(t.lossyScale.y));
            Vector3 center = t.TransformPoint(offset);
            float radius = circ.radius * scale;
            DrawCircle(center, radius, circleSegments, col, duration);
        }
        else if (c is CapsuleCollider2D cap)
        {
            DrawCapsuleApprox(cap, col, duration);
        }
        else if (c is PolygonCollider2D poly)
        {
            var t = poly.transform;
            for (int p = 0; p < poly.pathCount; ++p)
            {
                Vector2[] pts = poly.GetPath(p);
                if (pts.Length < 2) continue;
                Vector3[] world = new Vector3[pts.Length];
                for (int i = 0; i < pts.Length; ++i)
                    world[i] = t.TransformPoint(poly.offset + pts[i]);
                DrawLoop(world, col, duration);
            }
        }
        else if (c is EdgeCollider2D edge)
        {
            var t = edge.transform;
            Vector2[] pts = edge.points;
            for (int i = 0; i < pts.Length - 1; ++i)
            {
                Vector3 a = t.TransformPoint(edge.offset + pts[i]);
                Vector3 b = t.TransformPoint(edge.offset + pts[i + 1]);
                Debug.DrawLine(a, b, col, duration);
            }
        }
        else if (c is CompositeCollider2D comp)
        {
            var t = comp.transform;
            int paths = comp.pathCount;
            for (int p = 0; p < paths; ++p)
            {
                Vector2[] pts = new Vector2[comp.GetPathPointCount(p)];
                comp.GetPath(p, pts);
                if (pts.Length < 2) continue;
                Vector3[] world = new Vector3[pts.Length];
                for (int i = 0; i < pts.Length; ++i)
                    world[i] = t.TransformPoint(pts[i]);
                DrawLoop(world, col, duration);
            }
        }
        else
        {
            // Fallback: draw bounds
            Bounds b = c.bounds;
            Vector3 a = b.min;
            Vector3 b0 = new Vector3(b.max.x, b.min.y, b.min.z);
            Vector3 c0 = new Vector3(b.max.x, b.max.y, b.min.z);
            Vector3 d = new Vector3(b.min.x, b.max.y, b.min.z);
            Debug.DrawLine(a, b0, col, duration);
            Debug.DrawLine(b0, c0, col, duration);
            Debug.DrawLine(c0, d, col, duration);
            Debug.DrawLine(d, a, col, duration);
        }
    }

    void DrawRigidbody(Rigidbody2D rb, Color col)
    {
        float duration = Mathf.Max(0.01f, updateInterval * 0.9f);
        Vector3 origin = rb.worldCenterOfMass;
        Vector3 end = origin + (Vector3)(rb.linearVelocity * velocityScale);
        Debug.DrawLine(origin, end, col, duration);
        // Arrow head
        Vector3 dir = (end - origin).normalized;
        if (dir.sqrMagnitude > 0.0001f)
        {
            Vector3 right = Quaternion.Euler(0, 0, 135) * dir * 0.25f * velocityScale * 2f;
            Vector3 left = Quaternion.Euler(0, 0, -135) * dir * 0.25f * velocityScale * 2f;
            Debug.DrawLine(end, end + right, col, duration);
            Debug.DrawLine(end, end + left, col, duration);
        }
    }

    void DrawLoop(Vector3[] pts, Color col, float duration)
    {
        if (pts == null || pts.Length < 2) return;
        for (int i = 0; i < pts.Length; ++i)
        {
            Vector3 a = pts[i];
            Vector3 b = pts[(i + 1) % pts.Length];
            Debug.DrawLine(a, b, col, duration);
        }
    }

    void DrawCircle(Vector3 center, float radius, int segments, Color col, float duration)
    {
        if (segments < 3) segments = 3;
        Vector3 prev = center + new Vector3(radius, 0f, 0f);
        float step = 360f / segments;
        for (int i = 1; i <= segments; ++i)
        {
            float ang = step * i * Mathf.Deg2Rad;
            Vector3 next = center + new Vector3(Mathf.Cos(ang) * radius, Mathf.Sin(ang) * radius, 0f);
            Debug.DrawLine(prev, next, col, duration);
            prev = next;
        }
    }

    void DrawCapsuleApprox(CapsuleCollider2D cap, Color col, float duration)
    {
        // approximate capsule: draw rectangle + semicircles
        var t = cap.transform;
        Vector2 offset = cap.offset;
        Vector2 size = Vector2.Scale(cap.size, t.lossyScale);
        bool horizontal = cap.direction == CapsuleDirection2D.Horizontal;

        float radius = horizontal ? Mathf.Abs(size.y) * 0.5f : Mathf.Abs(size.x) * 0.5f;
        float length = (horizontal ? size.x : size.y) - 2f * radius;
        length = Mathf.Max(0f, length);

        Vector3 center = t.TransformPoint(offset);
        Vector3 axis = horizontal ? t.right : t.up;
        Vector3 ortho = horizontal ? t.up : t.right;

        Vector3 midA = center - axis * (length * 0.5f);
        Vector3 midB = center + axis * (length * 0.5f);

        // rectangle corners
        Vector3 a = midA - ortho * radius;
        Vector3 b = midB - ortho * radius;
        Vector3 c = midB + ortho * radius;
        Vector3 d = midA + ortho * radius;
        Debug.DrawLine(a, b, col, duration);
        Debug.DrawLine(b, c, col, duration);
        Debug.DrawLine(c, d, col, duration);
        Debug.DrawLine(d, a, col, duration);

        // semicircles at ends (approx)
        DrawHalfCircle(midB, ortho, -axis, radius, circleSegments / 2, col, duration);
        DrawHalfCircle(midA, ortho, axis, radius, circleSegments / 2, col, duration);
    }

    void DrawHalfCircle(Vector3 center, Vector3 ortho, Vector3 dir, float radius, int segments, Color col, float duration)
    {
        if (segments < 2) segments = 2;
        float startAng = Mathf.Atan2(ortho.y * dir.x - ortho.x * dir.y, Vector2.Dot(ortho, dir)); // not strictly needed
        // build points rotating from -ortho to ortho around dir axis
        Vector3 prev = center + (-ortho.normalized) * radius;
        for (int i = 1; i <= segments; ++i)
        {
            float t = (float)i / segments;
            float ang = Mathf.PI * t; // 0..PI
            Vector3 next = center + (Quaternion.AngleAxis(ang * Mathf.Rad2Deg, dir) * (-ortho.normalized)) * radius;
            Debug.DrawLine(prev, next, col, duration);
            prev = next;
        }
    }
}