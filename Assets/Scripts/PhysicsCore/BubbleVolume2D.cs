using System.Collections.Generic;
using UnityEngine;

namespace PhysicsWidgets2D
{
    /// <summary>
    /// A world-up lift volume intended to represent an underwater bubble stream.
    /// Lift scales with overlap. Torque Influence blends the application point
    /// between the body's center of mass and the exact overlap centroid.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(BoxCollider2D))]
    [DisallowMultipleComponent]
    [AddComponentMenu("Physics2D/Bubble Volume 2D")]
    public class BubbleVolume2D : MonoBehaviour, IVolumeProvider2D
    {
        [Header("Lift")]
        [Tooltip("World-up acceleration at full overlap. Normal gravity is approximately 9.81.")]
        [SerializeField, Min(0f)] private float liftAcceleration = 5f;
        [Tooltip("0 applies lift through the center of mass. 1 applies it at the exact overlap centroid.")]
        [SerializeField, Range(0f, 1f)] private float torqueInfluence = 0.35f;

        [Header("Debug Visualization")]
        [SerializeField] private bool drawDebugVisualization = true;
        [SerializeField] private Color bubbleColor = new Color(0.35f, 0.9f, 1f, 1f);
        [SerializeField] private Color liftArrowColor = new Color(0.15f, 1f, 0.75f, 1f);
        [SerializeField, Range(3, 20)] private int debugBubbleCount = 8;
        [SerializeField, Range(6, 24)] private int debugCircleSegments = 10;
        [SerializeField, Min(0.01f)] private float debugBubbleRadius = 0.07f;
        [SerializeField, Min(0f)] private float debugBubbleSpeed = 0.55f;
        [SerializeField, Min(0.05f)] private float debugArrowLength = 0.8f;
        [SerializeField, Min(0.01f)] private float debugArrowHeadSize = 0.14f;

        [Header("Diagnostics")]
        [SerializeField] private bool logDiagnostics = false;
        [SerializeField, Min(0.05f)] private float diagnosticInterval = 0.25f;

        public float LiftAcceleration => liftAcceleration;
        public float TorqueInfluence => torqueInfluence;
        public Vector2 WorldLiftDirection => Vector2.up;

        private BoxCollider2D bubbleCollider;
        private float nextDiagnosticTime;

        private void Awake()
        {
            CacheCollider();
            ValidateTrigger();
        }

        private void OnEnable()
        {
            CacheCollider();
        }

        private void OnValidate()
        {
            CacheCollider();
            liftAcceleration = Mathf.Max(0f, liftAcceleration);
            debugBubbleCount = Mathf.Max(3, debugBubbleCount);
            debugCircleSegments = Mathf.Max(6, debugCircleSegments);
            debugBubbleRadius = Mathf.Max(0.01f, debugBubbleRadius);
            debugBubbleSpeed = Mathf.Max(0f, debugBubbleSpeed);
            debugArrowLength = Mathf.Max(0.05f, debugArrowLength);
            debugArrowHeadSize = Mathf.Max(0.01f, debugArrowHeadSize);
            diagnosticInterval = Mathf.Max(0.05f, diagnosticInterval);
        }

        private void Update()
        {
            if(drawDebugVisualization)
                DrawDebugVisualization();
        }

        public void EvaluateVolume(Rigidbody2D body, PhysicsFrame2D frame)
        {
            if(body == null || bubbleCollider == null)
                return;

            BoxCollider2D box = body.GetComponent<BoxCollider2D>();

            if(box == null)
                return;

            float totalArea = CalculateBoxArea(box);

            if(totalArea <= 0.000001f)
                return;

            List<Vector2> overlapPolygon = CalculateOverlapPolygon(box);

            if(!TryCalculatePolygonProperties(
                    overlapPolygon,
                    out float overlapArea,
                    out Vector2 overlapCentroid))
            {
                return;
            }

            float overlapFraction = Mathf.Clamp01(overlapArea / totalArea);
            Vector2 liftForce =
                Vector2.up *
                liftAcceleration *
                body.mass *
                overlapFraction;

            Vector2 applicationPoint =
                Vector2.Lerp(
                    body.worldCenterOfMass,
                    overlapCentroid,
                    torqueInfluence);

            frame.AddForceAtPosition(liftForce, applicationPoint);

            LogDiagnostics(
                body,
                overlapFraction,
                overlapCentroid,
                applicationPoint,
                liftForce);
        }

        private void CacheCollider()
        {
            if(bubbleCollider == null)
                bubbleCollider = GetComponent<BoxCollider2D>();
        }

        private void ValidateTrigger()
        {
            if(bubbleCollider != null && !bubbleCollider.isTrigger)
            {
                Debug.LogWarning(
                    $"{name}: BubbleVolume2D requires its BoxCollider2D to be a trigger.",
                    this);
            }
        }

        private float CalculateBoxArea(BoxCollider2D box)
        {
            Vector3 scale = box.transform.lossyScale;
            float width = box.size.x * Mathf.Abs(scale.x);
            float height = box.size.y * Mathf.Abs(scale.y);
            return width * height;
        }

        private List<Vector2> CalculateOverlapPolygon(BoxCollider2D box)
        {
            float halfWidth = box.size.x * 0.5f;
            float halfHeight = box.size.y * 0.5f;

            List<Vector2> polygon =
                new List<Vector2>
                {
                    box.transform.TransformPoint(
                        box.offset + new Vector2(-halfWidth, -halfHeight)),
                    box.transform.TransformPoint(
                        box.offset + new Vector2(halfWidth, -halfHeight)),
                    box.transform.TransformPoint(
                        box.offset + new Vector2(halfWidth, halfHeight)),
                    box.transform.TransformPoint(
                        box.offset + new Vector2(-halfWidth, halfHeight))
                };

            Bounds bounds = bubbleCollider.bounds;

            polygon = ClipAgainstVerticalBoundary(
                polygon,
                bounds.min.x,
                keepGreater: true);

            polygon = ClipAgainstVerticalBoundary(
                polygon,
                bounds.max.x,
                keepGreater: false);

            polygon = ClipAgainstHorizontalBoundary(
                polygon,
                bounds.min.y,
                keepGreater: true);

            polygon = ClipAgainstHorizontalBoundary(
                polygon,
                bounds.max.y,
                keepGreater: false);

            return polygon;
        }

        private List<Vector2> ClipAgainstVerticalBoundary(
            List<Vector2> input,
            float boundary,
            bool keepGreater)
        {
            List<Vector2> output = new List<Vector2>();

            if(input == null || input.Count == 0)
                return output;

            Vector2 previous = input[input.Count - 1];
            bool previousInside =
                keepGreater
                ? previous.x >= boundary
                : previous.x <= boundary;

            for(int i = 0; i < input.Count; i++)
            {
                Vector2 current = input[i];
                bool currentInside =
                    keepGreater
                    ? current.x >= boundary
                    : current.x <= boundary;

                if(currentInside != previousInside)
                {
                    float denominator = current.x - previous.x;

                    if(Mathf.Abs(denominator) > 0.000001f)
                    {
                        float t = (boundary - previous.x) / denominator;
                        output.Add(Vector2.Lerp(previous, current, t));
                    }
                }

                if(currentInside)
                    output.Add(current);

                previous = current;
                previousInside = currentInside;
            }

            return output;
        }

        private List<Vector2> ClipAgainstHorizontalBoundary(
            List<Vector2> input,
            float boundary,
            bool keepGreater)
        {
            List<Vector2> output = new List<Vector2>();

            if(input == null || input.Count == 0)
                return output;

            Vector2 previous = input[input.Count - 1];
            bool previousInside =
                keepGreater
                ? previous.y >= boundary
                : previous.y <= boundary;

            for(int i = 0; i < input.Count; i++)
            {
                Vector2 current = input[i];
                bool currentInside =
                    keepGreater
                    ? current.y >= boundary
                    : current.y <= boundary;

                if(currentInside != previousInside)
                {
                    float denominator = current.y - previous.y;

                    if(Mathf.Abs(denominator) > 0.000001f)
                    {
                        float t = (boundary - previous.y) / denominator;
                        output.Add(Vector2.Lerp(previous, current, t));
                    }
                }

                if(currentInside)
                    output.Add(current);

                previous = current;
                previousInside = currentInside;
            }

            return output;
        }

        private bool TryCalculatePolygonProperties(
            List<Vector2> polygon,
            out float area,
            out Vector2 centroid)
        {
            area = 0f;
            centroid = Vector2.zero;

            if(polygon == null || polygon.Count < 3)
                return false;

            float signedDoubleArea = 0f;
            Vector2 centroidSum = Vector2.zero;

            for(int i = 0; i < polygon.Count; i++)
            {
                Vector2 current = polygon[i];
                Vector2 next = polygon[(i + 1) % polygon.Count];

                float cross =
                    current.x * next.y -
                    next.x * current.y;

                signedDoubleArea += cross;
                centroidSum += (current + next) * cross;
            }

            if(Mathf.Abs(signedDoubleArea) <= 0.000001f)
                return false;

            area = Mathf.Abs(signedDoubleArea) * 0.5f;
            centroid = centroidSum / (3f * signedDoubleArea);
            return true;
        }

        private void DrawDebugVisualization()
        {
            CacheCollider();

            if(bubbleCollider == null || !bubbleCollider.enabled)
                return;

            Bounds bounds = bubbleCollider.bounds;
            float time =
                Application.isPlaying
                ? Time.unscaledTime
                : Time.realtimeSinceStartup;

            float radius =
                Mathf.Min(
                    debugBubbleRadius,
                    Mathf.Min(bounds.size.x, bounds.size.y) * 0.2f);

            float horizontalMargin = Mathf.Min(radius, bounds.extents.x);
            float usableWidth = Mathf.Max(0f, bounds.size.x - horizontalMargin * 2f);
            float height = Mathf.Max(0.0001f, bounds.size.y);

            for(int i = 0; i < debugBubbleCount; i++)
            {
                float seed = Hash01(i * 17 + 3);
                float phase = Hash01(i * 31 + 11);
                float normalizedY =
                    Mathf.Repeat(
                        phase + time * debugBubbleSpeed / height,
                        1f);

                float x =
                    bounds.min.x +
                    horizontalMargin +
                    usableWidth * seed;

                float y =
                    Mathf.Lerp(
                        bounds.min.y + radius,
                        bounds.max.y - radius,
                        normalizedY);

                float pulse =
                    0.75f +
                    0.25f *
                    Mathf.Sin(time * 2f + i * 1.73f);

                DrawCircle(
                    new Vector2(x, y),
                    radius * pulse,
                    bubbleColor);
            }

            Vector2 arrowStart =
                new Vector2(
                    bounds.center.x,
                    bounds.center.y - debugArrowLength * 0.5f);

            Vector2 arrowEnd = arrowStart + Vector2.up * debugArrowLength;

            Debug.DrawLine(
                arrowStart,
                arrowEnd,
                liftArrowColor,
                0f,
                false);

            DrawArrowHead(
                arrowEnd,
                Vector2.up,
                debugArrowHeadSize,
                liftArrowColor);
        }

        private void DrawCircle(
            Vector2 center,
            float radius,
            Color color)
        {
            Vector2 previous = center + Vector2.right * radius;

            for(int i = 1; i <= debugCircleSegments; i++)
            {
                float angle =
                    i *
                    Mathf.PI *
                    2f /
                    debugCircleSegments;

                Vector2 current =
                    center +
                    new Vector2(
                        Mathf.Cos(angle),
                        Mathf.Sin(angle)) *
                    radius;

                Debug.DrawLine(
                    previous,
                    current,
                    color,
                    0f,
                    false);

                previous = current;
            }
        }

        private void DrawArrowHead(
            Vector2 end,
            Vector2 direction,
            float size,
            Color color)
        {
            Vector2 right =
                Quaternion.Euler(0f, 0f, 150f) *
                direction *
                size;

            Vector2 left =
                Quaternion.Euler(0f, 0f, -150f) *
                direction *
                size;

            Debug.DrawLine(end, end + right, color, 0f, false);
            Debug.DrawLine(end, end + left, color, 0f, false);
        }

        private static float Hash01(int value)
        {
            unchecked
            {
                uint x = (uint)value;
                x ^= x >> 16;
                x *= 0x7feb352d;
                x ^= x >> 15;
                x *= 0x846ca68b;
                x ^= x >> 16;
                return (x & 0x00ffffff) / 16777215f;
            }
        }

        private void LogDiagnostics(
            Rigidbody2D body,
            float overlapFraction,
            Vector2 overlapCentroid,
            Vector2 applicationPoint,
            Vector2 liftForce)
        {
            if(!logDiagnostics || Time.time < nextDiagnosticTime)
                return;

            nextDiagnosticTime = Time.time + diagnosticInterval;

            Vector2 leverArm =
                applicationPoint -
                body.worldCenterOfMass;

            float generatedTorque =
                leverArm.x * liftForce.y -
                leverArm.y * liftForce.x;

            Debug.Log(
                $"{body.name} Bubble: " +
                $"overlap={overlapFraction:F3}, " +
                $"overlapCentroid=({overlapCentroid.x:F3}, {overlapCentroid.y:F3}), " +
                $"applicationPoint=({applicationPoint.x:F3}, {applicationPoint.y:F3}), " +
                $"liftForce=({liftForce.x:F3}, {liftForce.y:F3}), " +
                $"torque={generatedTorque:F3}, " +
                $"velocity=({body.linearVelocity.x:F3}, {body.linearVelocity.y:F3}), " +
                $"angularVelocity={body.angularVelocity:F3}");
        }
    }
}