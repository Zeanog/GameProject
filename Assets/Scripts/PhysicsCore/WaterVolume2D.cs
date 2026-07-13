using UnityEngine;

namespace PhysicsWidgets2D
{
    /// <summary>
    /// Simplified 2D water volume.
    ///
    /// Version 0.5 behavior:
    /// - Assumes BoxCollider2D objects.
    /// - Calculates submerged fraction.
    /// - Applies buoyancy proportional to Rigidbody2D mass.
    ///
    /// This intentionally does NOT apply buoyancy torque yet.
    /// The goal is to establish stable buoyancy behavior first.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class WaterVolume2D :
        MonoBehaviour,
        IVolumeProvider2D
    {
        [Header("Buoyancy")]

        [Tooltip(
            "Multiplier for buoyancy relative to object weight. " +
            "1.0 = neutral water.")]
        [SerializeField]
        private float buoyancyMultiplier = 1f;



        [Header("Fluid Resistance")]

        [SerializeField]
        private float linearDamping = 2f;


        [SerializeField]
        private float angularDamping = 0.5f;



        private Collider2D waterCollider;



        private void Awake()
        {
            waterCollider =
                GetComponent<Collider2D>();


            if(!waterCollider.isTrigger)
            {
                Debug.LogWarning(
                    $"{name}: WaterVolume2D requires a trigger Collider2D.");
            }
        }



        public void EvaluateVolume(
            Rigidbody2D body,
            PhysicsFrame2D frame)
        {
            BoxCollider2D box =
                body.GetComponent<BoxCollider2D>();


            if(box == null)
                return;



            float submergedFraction =
                CalculateSubmergedFraction(
                    box);

            Debug.Log(
                $"{body.name} submerged fraction {submergedFraction}");

            if(submergedFraction <= 0f)
                return;



            ApplyBuoyancy(
                body,
                submergedFraction,
                frame);



            ApplyDamping(
                body,
                submergedFraction,
                frame);
        }



        private void ApplyBuoyancy(
            Rigidbody2D body,
            float submergedFraction,
            PhysicsFrame2D frame)
        {
            float gravity =
                Physics2D.gravity.magnitude;



            float forceMagnitude =
                body.mass *
                gravity *
                submergedFraction *
                buoyancyMultiplier;



            frame.AddForce(
                Vector2.up *
                forceMagnitude);
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


            float waterSurface =
                waterCollider.bounds.max.y;


            float height =
                maxY - minY;


            if(height <= 0f)
                return 0f;


            float submerged =
                waterSurface - minY;


            return Mathf.Clamp01(
                submerged / height);
        }



        private void ApplyDamping(
            Rigidbody2D body,
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
    }
}