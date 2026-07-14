using UnityEngine;

namespace PhysicsWidgets2D
{
    /// <summary>
    /// A physics-based 2D conveyor belt.
    ///
    /// The conveyor moves along the long axis of its Collider2D:
    /// - positive speed moves along the positive local belt axis
    /// - negative speed moves along the negative local belt axis
    ///
    /// For CapsuleCollider2D:
    /// - Horizontal uses local X
    /// - Vertical uses local Y
    ///
    /// Rotation controls the belt's world-space orientation.
    ///
    /// This component generates ContactConstraint2D entries which are
    /// solved by PhysicsSolver2D.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    [DisallowMultipleComponent]
    [AddComponentMenu("Physics2D/Conveyor Belt 2D")]
    [ExecuteAlways]
    public class NewConveyorBelt2D : MonoBehaviour, IContactProvider2D
    {
        [Header("Movement")]

        [Tooltip(
            "Signed surface speed in world units per second. " +
            "Positive follows the collider's positive long axis; negative reverses it.")]
        [SerializeField]
        private float speed = 3f;

        [Header("Physics")]

        [Tooltip(
            "Controls how strongly the package velocity follows the belt.")]
        [SerializeField]
        [Range(0f, 1f)]
        private float friction = 1f;

        [Tooltip("Overall strength multiplier.")]
        [SerializeField]
        [Min(0f)]
        private float strength = 1f;

        private Collider2D beltCollider;


        public float Speed
        {
            get
            {
                return speed;
            }
        }

        public Vector2 BeltAxis
        {
            get
            {
                Vector2 localAxis = GetLocalBeltAxis();
                return transform.TransformDirection(localAxis).normalized;
            }
        }

        public Vector2 BeltNormal
        {
            get
            {
                Vector2 localAxis = GetLocalBeltAxis();
                Vector2 localNormal = new Vector2(-localAxis.y, localAxis.x);
                return transform.TransformDirection(localNormal).normalized;
            }
        }

        public Vector2 SurfaceVelocity
        {
            get
            {
                return BeltAxis * speed;
            }
        }


        private void Awake()
        {
            beltCollider = GetComponent<Collider2D>();

            if(beltCollider != null &&
               beltCollider.isTrigger)
            {
                Debug.LogWarning(
                    name +
                    ": ConveyorBelt2D should use a non-trigger Collider2D.");
            }
        }


        private void OnValidate()
        {
            beltCollider = GetComponent<Collider2D>();
        }


        private Vector2 GetLocalBeltAxis()
        {
            if(beltCollider == null)
                beltCollider = GetComponent<Collider2D>();

            if(beltCollider is CapsuleCollider2D capsule)
            {
                return
                    capsule.direction == CapsuleDirection2D.Horizontal
                    ? Vector2.right
                    : Vector2.up;
            }

            if(beltCollider is BoxCollider2D box)
            {
                return
                    box.size.x >= box.size.y
                    ? Vector2.right
                    : Vector2.up;
            }

            return Vector2.right;
        }


        public void EvaluateContact(
            Rigidbody2D body,
            ContactPoint2D contact,
            PhysicsFrame2D frame)
        {
            if(body == null || frame == null)
                return;

            ContactConstraint2D constraint =
                new ContactConstraint2D(
                    point: contact.point,
                    normal: contact.normal,
                    tangent: BeltAxis,
                    surfaceVelocity: SurfaceVelocity,
                    friction: friction,
                    strength: strength);

            frame.AddContactConstraint(constraint);
        }
    }
}
