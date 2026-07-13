using UnityEngine;

namespace PhysicsWidgets2D
{
    /// <summary>
    /// A physics-based 2D conveyor belt.
    ///
    /// This component represents a moving contact surface.
    ///
    /// It does NOT:
    /// - modify Rigidbody2D velocity
    /// - apply forces directly
    /// - move objects
    ///
    /// Instead, it generates ContactConstraint2D entries which are solved
    /// by PhysicsSolver2D.
    ///
    /// This allows:
    /// - multiple conveyors affecting the same object
    /// - opposing conveyors
    /// - sloped conveyors
    /// - underside collisions
    /// - future moving platforms and rollers
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    [DisallowMultipleComponent]
    [AddComponentMenu("Physics2D/Conveyor Belt 2D")]
    [ExecuteAlways]
    public class NewConveyorBelt2D : MonoBehaviour, IContactProvider2D
    {
        [Header("Movement")]
        [Tooltip("Direction of conveyor motion in local space.")]
        [SerializeField]
        private Vector2 localDirection = Vector2.right;

        [Tooltip("Surface speed in world units per second.")]
        [SerializeField]
        private float speed = 3f;

        [Header("Physics")]
        [Tooltip("Friction coefficient controlling how strongly the belt transfers motion.")]
        [SerializeField]
        private float friction = 1f;

        [Tooltip("Overall strength multiplier.")]
        [SerializeField]
        private float strength = 1f;


        public Vector2 WorldDirection
        {
            get
            {
                return transform.TransformDirection(localDirection).normalized;
            }
        }

        public Vector2 SurfaceVelocity
        {
            get
            {
                return WorldDirection * speed;
            }
        }

        private void Awake()
        {
            Collider2D beltCollider = GetComponent<Collider2D>();
            if(beltCollider != null && beltCollider.isTrigger)
            {
                Debug.LogWarning(name + ": ConveyorBelt2D should use a non-trigger Collider2D.");
            }

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
                    tangent: WorldDirection,
                    surfaceVelocity: SurfaceVelocity,
                    friction: friction,
                    strength: strength);

            frame.AddContactConstraint(constraint);
        }
    }
}