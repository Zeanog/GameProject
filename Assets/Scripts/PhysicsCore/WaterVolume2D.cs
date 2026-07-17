using System.Collections.Generic;
using UnityEngine;

namespace PhysicsWidgets2D
{
    /// <summary>
    /// Simplified 2D water volume.
    ///
    /// Version 1.0 behavior:
    /// - Supports BoxCollider2D objects.
    /// - Supports PolygonCollider2D objects with one path and N points.
    /// - Clips the object's world-space polygon against axis-aligned water bounds.
    /// - Applies buoyancy at the submerged area's centroid.
    /// - Applies linear and nonlinear fluid resistance.
    /// - Applies a uniform horizontal fluid current.
    ///
    /// Applying buoyancy at the submerged centroid naturally produces
    /// righting torque during partial submersion.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class WaterVolume2D :
        MonoBehaviour,
        IVolumeProvider2D
    {
        [Header("Fluid Properties")]

        [Tooltip(
            "Fluid density in mass per square Unity unit. " +
            "A value of 1 represents standard project water.")]
        [SerializeField]
        [Min(0.1f)]
        private float fluidDensity = 1f;

        [Header("Fluid Resistance")]

        [SerializeField]
        private float linearDamping = 2f;

        [Tooltip(
            "Linear rotational resistance. This remains effective as " +
            "rotation slows and controls final settling.")]
        [SerializeField]
        [Min(0f)]
        private float angularDamping = 0.2f;

        [Tooltip(
            "Additional rotational resistance that increases with the " +
            "square of angular speed. This primarily limits rapid spinning.")]
        [SerializeField]
        [Min(0f)]
        private float quadraticAngularDamping = 0.05f;

        [Header("Fluid Current")]

        [Tooltip(
            "Uniform horizontal fluid speed. " +
            "Positive values move right and negative values move left.")]
        [SerializeField]
        private float horizontalCurrentSpeed = 0f;

        [Header("Diagnostics")]

        [Tooltip(
            "Logs buoyancy calculations while a body is inside this volume.")]
        [SerializeField]
        private bool logDiagnostics = false;

        [Tooltip(
            "Time in seconds between diagnostic messages.")]
        [SerializeField]
        [Min(0.05f)]
        private float diagnosticInterval = 0.25f;

        public float HorizontalCurrentSpeed
        {
            get
            {
                return horizontalCurrentSpeed;
            }
        }

        private Collider2D waterCollider;
        private float nextDiagnosticTime;
        private readonly HashSet<EntityId> warnedUnsupportedBodies =
            new HashSet<EntityId>();


        private void Awake()
        {
            waterCollider = GetComponent<Collider2D>();

            if(!waterCollider.isTrigger)
            {
                Debug.LogWarning(
                    $"{name}: WaterVolume2D requires a trigger Collider2D.");
            }
        }


        public void EvaluateVolume(Rigidbody2D body, PhysicsFrame2D frame)
        {
            if(body == null)
                return;

            if(!TryGetBodyPolygon(
                    body,
                    out List<Vector2> bodyPolygon,
                    out string colliderType))
            {
                WarnUnsupportedBody(body);
                return;
            }

            if(!TryCalculatePolygonProperties(
                    bodyPolygon,
                    out float totalArea,
                    out _))
            {
                LogPolygonFailure(
                    body,
                    colliderType,
                    "source polygon has zero or invalid area",
                    bodyPolygon);
                return;
            }

            List<Vector2> submergedPolygon =
                ClipPolygonToWaterBounds(bodyPolygon);

            if(!TryCalculatePolygonProperties(
                    submergedPolygon,
                    out float submergedArea,
                    out Vector2 centerOfBuoyancy))
            {
                LogPolygonFailure(
                    body,
                    colliderType,
                    "no valid overlap with the water bounds",
                    bodyPolygon);
                return;
            }

            float submergedFraction =
                Mathf.Clamp01(submergedArea / totalArea);

            float buoyancyForce = CalculateBuoyancyForce(submergedArea);
            Vector2 currentForce = CalculateCurrentForce(body, submergedFraction);
            float quadraticDampingTorque =
                CalculateQuadraticAngularDampingTorque(
                    body,
                    submergedFraction);

            ApplyBuoyancy(
                buoyancyForce,
                centerOfBuoyancy,
                frame);

            ApplyCurrent(currentForce, frame);
            ApplyDamping(submergedFraction, frame);
            ApplyQuadraticAngularDamping(
                quadraticDampingTorque,
                frame);

            LogDiagnostics(
                body,
                colliderType,
                submergedFraction,
                totalArea,
                submergedArea,
                centerOfBuoyancy,
                buoyancyForce,
                currentForce,
                quadraticDampingTorque);
        }


        private bool TryGetBodyPolygon(
            Rigidbody2D body,
            out List<Vector2> polygon,
            out string colliderType)
        {
            Collider2D[] bodyColliders = body.GetComponents<Collider2D>();

            for(int i = 0; i < bodyColliders.Length; i++)
            {
                if(bodyColliders[i] is not PolygonCollider2D polygonCollider)
                    continue;

                if(!polygonCollider.enabled || polygonCollider.isTrigger)
                    continue;

                if(polygonCollider.attachedRigidbody != body)
                    continue;

                if(polygonCollider.pathCount != 1)
                    continue;

                polygon = BuildPolygonColliderPolygon(polygonCollider);
                colliderType = nameof(PolygonCollider2D);
                return polygon.Count >= 3;
            }

            for(int i = 0; i < bodyColliders.Length; i++)
            {
                if(bodyColliders[i] is not BoxCollider2D box)
                    continue;

                if(!box.enabled || box.isTrigger)
                    continue;

                if(box.attachedRigidbody != body)
                    continue;

                polygon = BuildBoxPolygon(box);
                colliderType = nameof(BoxCollider2D);
                return true;
            }

            polygon = null;
            colliderType = string.Empty;
            return false;
        }


        private List<Vector2> BuildBoxPolygon(BoxCollider2D box)
        {
            float halfWidth = box.size.x * 0.5f;
            float halfHeight = box.size.y * 0.5f;

            return new List<Vector2>
            {
                box.transform.TransformPoint(
                    box.offset +
                    new Vector2(-halfWidth, -halfHeight)),

                box.transform.TransformPoint(
                    box.offset +
                    new Vector2(halfWidth, -halfHeight)),

                box.transform.TransformPoint(
                    box.offset +
                    new Vector2(halfWidth, halfHeight)),

                box.transform.TransformPoint(
                    box.offset +
                    new Vector2(-halfWidth, halfHeight))
            };
        }


        private List<Vector2> BuildPolygonColliderPolygon(
            PolygonCollider2D polygonCollider)
        {
            Vector2[] path = polygonCollider.GetPath(0);
            List<Vector2> polygon = new List<Vector2>(path.Length);

            for(int i = 0; i < path.Length; i++)
            {
                polygon.Add(
                    polygonCollider.transform.TransformPoint(
                        polygonCollider.offset + path[i]));
            }

            return polygon;
        }


        private List<Vector2> ClipPolygonToWaterBounds(
            List<Vector2> polygon)
        {
            Bounds waterBounds = waterCollider.bounds;

            polygon = ClipAgainstVerticalBoundary(
                polygon,
                waterBounds.min.x,
                keepGreater: true);

            polygon = ClipAgainstVerticalBoundary(
                polygon,
                waterBounds.max.x,
                keepGreater: false);

            polygon = ClipAgainstHorizontalBoundary(
                polygon,
                waterBounds.min.y,
                keepGreater: true);

            polygon = ClipAgainstHorizontalBoundary(
                polygon,
                waterBounds.max.y,
                keepGreater: false);

            return polygon;
        }


        private float CalculateBuoyancyForce(float submergedArea)
        {
            return
                submergedArea *
                fluidDensity *
                Physics2D.gravity.magnitude;
        }


        private Vector2 CalculateCurrentForce(
            Rigidbody2D body,
            float submergedFraction)
        {
            Vector2 currentVelocity =
                Vector2.right *
                horizontalCurrentSpeed;

            return
                currentVelocity *
                linearDamping *
                body.mass *
                submergedFraction;
        }


        private float CalculateQuadraticAngularDampingTorque(
            Rigidbody2D body,
            float submergedFraction)
        {
            float angularVelocityRadians =
                body.angularVelocity *
                Mathf.Deg2Rad;

            return
                -angularVelocityRadians *
                Mathf.Abs(angularVelocityRadians) *
                quadraticAngularDamping *
                body.inertia *
                submergedFraction;
        }


        private void ApplyBuoyancy(
            float forceMagnitude,
            Vector2 centerOfBuoyancy,
            PhysicsFrame2D frame)
        {
            float gravityMagnitude = Physics2D.gravity.magnitude;

            if(gravityMagnitude <= 0f)
                return;

            Vector2 forceDirection = -Physics2D.gravity.normalized;

            frame.AddForceAtPosition(
                forceDirection * forceMagnitude,
                centerOfBuoyancy);
        }


        private void ApplyCurrent(
            Vector2 currentForce,
            PhysicsFrame2D frame)
        {
            if(currentForce != Vector2.zero)
                frame.AddForce(currentForce);
        }


        private void ApplyQuadraticAngularDamping(
            float dampingTorque,
            PhysicsFrame2D frame)
        {
            if(!Mathf.Approximately(dampingTorque, 0f))
                frame.AddTorque(dampingTorque);
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

                        output.Add(
                            Vector2.Lerp(
                                previous,
                                current,
                                t));
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

                        output.Add(
                            Vector2.Lerp(
                                previous,
                                current,
                                t));
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


        private void ApplyDamping(
            float submergedFraction,
            PhysicsFrame2D frame)
        {
            frame.AddLinearDamping(
                linearDamping *
                submergedFraction);

            frame.AddAngularDamping(
                angularDamping *
                submergedFraction);
        }


        private void LogPolygonFailure(
            Rigidbody2D body,
            string colliderType,
            string reason,
            List<Vector2> bodyPolygon)
        {
            if(!logDiagnostics || Time.time < nextDiagnosticTime)
                return;

            nextDiagnosticTime = Time.time + diagnosticInterval;

            Bounds waterBounds = waterCollider.bounds;
            Bounds polygonBounds = new Bounds();

            if(bodyPolygon != null && bodyPolygon.Count > 0)
            {
                polygonBounds = new Bounds(bodyPolygon[0], Vector3.zero);

                for(int i = 1; i < bodyPolygon.Count; i++)
                    polygonBounds.Encapsulate(bodyPolygon[i]);
            }

            Debug.LogWarning(
                $"{body.name} Water: collider={colliderType}, " +
                $"evaluation failed because {reason}. " +
                $"polygonPoints={bodyPolygon?.Count ?? 0}, " +
                $"polygonBounds={polygonBounds}, " +
                $"waterBounds={waterBounds}",
                body);
        }


        private void WarnUnsupportedBody(Rigidbody2D body)
        {
            EntityId entityId = body.GetEntityId();

            if(warnedUnsupportedBodies.Contains(entityId))
            {
                return;
            }

            warnedUnsupportedBodies.Add(entityId);

            PolygonCollider2D polygon =
                body.GetComponent<PolygonCollider2D>();

            if(polygon != null && polygon.pathCount != 1)
            {
                Debug.LogWarning(
                    $"{name}: {body.name} has a PolygonCollider2D with " +
                    $"{polygon.pathCount} paths. WaterVolume2D currently " +
                    "supports exactly one polygon path.",
                    body);

                return;
            }

            Debug.LogWarning(
                $"{name}: {body.name} requires an enabled BoxCollider2D " +
                "or a single-path PolygonCollider2D for water calculations.",
                body);
        }


        private void LogDiagnostics(
            Rigidbody2D body,
            string colliderType,
            float submergedFraction,
            float totalArea,
            float submergedArea,
            Vector2 centerOfBuoyancy,
            float buoyancyForce,
            Vector2 currentForce,
            float quadraticDampingTorque)
        {
            if(!logDiagnostics ||
               Time.time < nextDiagnosticTime)
            {
                return;
            }

            nextDiagnosticTime =
                Time.time + diagnosticInterval;

            float weight =
                body.mass *
                Physics2D.gravity.magnitude *
                body.gravityScale;

            float netVerticalForce =
                buoyancyForce -
                weight;

            Debug.Log(
                $"{body.name} Water: " +
                $"collider={colliderType}, " +
                $"submerged={submergedFraction:F3}, " +
                $"area={totalArea:F3}, " +
                $"submergedArea={submergedArea:F3}, " +
                $"centerOfBuoyancy=({centerOfBuoyancy.x:F3}, " +
                $"{centerOfBuoyancy.y:F3}), " +
                $"mass={body.mass:F3}, " +
                $"buoyancy={buoyancyForce:F3}, " +
                $"weight={weight:F3}, " +
                $"netUp={netVerticalForce:F3}, " +
                $"currentSpeed={horizontalCurrentSpeed:F3}, " +
                $"currentForceX={currentForce.x:F3}, " +
                $"velocity=({body.linearVelocity.x:F3}, " +
                $"{body.linearVelocity.y:F3}), " +
                $"angularVelocity={body.angularVelocity:F3}, " +
                $"quadraticTorque={quadraticDampingTorque:F3}");
        }
    }
}