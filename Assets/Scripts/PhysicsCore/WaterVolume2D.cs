using UnityEngine;

namespace PhysicsWidgets2D
{
    /// <summary>
    /// Simplified 2D water volume.
    ///
    /// Version 0.7 behavior:
    /// - Assumes BoxCollider2D objects.
    /// - Calculates approximate submerged area.
    /// - Applies buoyancy based on fluid density.
    /// - Applies damping based on submerged fraction.
    /// - Applies a uniform horizontal fluid current.
    ///
    /// This intentionally does NOT apply buoyancy torque yet.
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

        [SerializeField]
        private float angularDamping = 0.5f;

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
            BoxCollider2D box = body.GetComponent<BoxCollider2D>();

            if(box == null)
                return;

            float submergedFraction = CalculateSubmergedFraction(box);

            if(submergedFraction <= 0f)
                return;

            float totalArea = CalculateBoxArea(box);
            float submergedArea = totalArea * submergedFraction;
            float buoyancyForce = CalculateBuoyancyForce(submergedArea);
            Vector2 currentForce = CalculateCurrentForce(body, submergedFraction);

            ApplyBuoyancy(buoyancyForce, frame);
            ApplyCurrent(currentForce, frame);
            ApplyDamping(submergedFraction, frame);

            LogDiagnostics(
                body,
                submergedFraction,
                totalArea,
                submergedArea,
                buoyancyForce,
                currentForce);
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


        private void ApplyBuoyancy(
            float forceMagnitude,
            PhysicsFrame2D frame)
        {
            float gravityMagnitude = Physics2D.gravity.magnitude;

            if(gravityMagnitude <= 0f)
                return;

            Vector2 forceDirection = -Physics2D.gravity.normalized;

            frame.AddForce(
                forceDirection *
                forceMagnitude);
        }


        private void ApplyCurrent(
            Vector2 currentForce,
            PhysicsFrame2D frame)
        {
            if(currentForce != Vector2.zero)
                frame.AddForce(currentForce);
        }


        private float CalculateBoxArea(BoxCollider2D box)
        {
            Vector3 scale = box.transform.lossyScale;

            float width =
                box.size.x *
                Mathf.Abs(scale.x);

            float height =
                box.size.y *
                Mathf.Abs(scale.y);

            return width * height;
        }


        private float CalculateSubmergedFraction(
            BoxCollider2D box)
        {
            Vector2[] corners =
            {
                box.transform.TransformPoint(
                    box.offset +
                    new Vector2(-box.size.x * 0.5f,
                                -box.size.y * 0.5f)),

                box.transform.TransformPoint(
                    box.offset +
                    new Vector2(-box.size.x * 0.5f,
                                box.size.y * 0.5f)),

                box.transform.TransformPoint(
                    box.offset +
                    new Vector2(box.size.x * 0.5f,
                                box.size.y * 0.5f)),

                box.transform.TransformPoint(
                    box.offset +
                    new Vector2(box.size.x * 0.5f,
                                -box.size.y * 0.5f))
            };

            float minY = float.MaxValue;
            float maxY = float.MinValue;

            foreach(Vector2 corner in corners)
            {
                minY = Mathf.Min(minY, corner.y);
                maxY = Mathf.Max(maxY, corner.y);
            }

            float waterSurface = waterCollider.bounds.max.y;
            float height = maxY - minY;

            if(height <= 0f)
                return 0f;

            float submerged = waterSurface - minY;

            return Mathf.Clamp01(
                submerged / height);
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


        private void LogDiagnostics(
            Rigidbody2D body,
            float submergedFraction,
            float totalArea,
            float submergedArea,
            float buoyancyForce,
            Vector2 currentForce)
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
                $"submerged={submergedFraction:F3}, " +
                $"area={totalArea:F3}, " +
                $"submergedArea={submergedArea:F3}, " +
                $"mass={body.mass:F3}, " +
                $"buoyancy={buoyancyForce:F3}, " +
                $"weight={weight:F3}, " +
                $"netUp={netVerticalForce:F3}, " +
                $"currentSpeed={horizontalCurrentSpeed:F3}, " +
                $"currentForceX={currentForce.x:F3}, " +
                $"velocity=({body.linearVelocity.x:F3}, " +
                $"{body.linearVelocity.y:F3})");
        }
    }
}
