using UnityEngine;

namespace PhysicsWidgets2D
{
    /// <summary>
    /// Represents a moving contact surface that should apply frictional impulses
    /// to a Rigidbody2D.
    ///
    /// Unlike a force, a contact constraint describes the desired motion of the
    /// surface itself. The solver compares the body's velocity at the contact point
    /// against the surface velocity and applies an impulse to reduce the difference.
    /// </summary>
    public struct ContactConstraint2D
    {
        /// <summary>
        /// World-space point where the contact occurs.
        /// </summary>
        public Vector2 Point;

        /// <summary>
        /// World-space surface normal pointing away from the surface.
        /// </summary>
        public Vector2 Normal;

        /// <summary>
        /// World-space tangent of the surface.
        /// Must be normalized.
        /// </summary>
        public Vector2 Tangent;

        /// <summary>
        /// Velocity of the surface at this contact point.
        /// A stationary surface has Vector2.zero.
        /// A conveyor moving at 3 units/sec to the right would be (3,0).
        /// </summary>
        public Vector2 SurfaceVelocity;

        /// <summary>
        /// Maximum friction coefficient for this contact.
        /// Typical values:
        ///
        /// 0.0 = Ice
        /// 0.3 = Slippery
        /// 0.8 = Rubber
        /// 1.0 = Conveyor belt
        /// 2.0 = Extremely sticky
        /// </summary>
        public float Friction;

        /// <summary>
        /// Optional strength multiplier.
        /// Allows effects such as weakened conveyors or damaged rollers.
        /// Defaults to 1.
        /// </summary>
        public float Strength;

        /// <summary>
        /// Creates a contact constraint.
        /// </summary>
        public ContactConstraint2D(
            Vector2 point,
            Vector2 normal,
            Vector2 tangent,
            Vector2 surfaceVelocity,
            float friction,
            float strength = 1f)
        {
            Point = point;
            Normal = normal;
            Tangent = tangent.normalized;
            SurfaceVelocity = surfaceVelocity;
            Friction = Mathf.Max(0f, friction);
            Strength = Mathf.Max(0f, strength);
        }

        /// <summary>
        /// Returns the relative velocity between the body's contact point
        /// and the moving surface.
        /// </summary>
        public Vector2 GetRelativeVelocity(Rigidbody2D body)
        {
            return body.GetPointVelocity(Point) - SurfaceVelocity;
        }

        /// <summary>
        /// Returns the component of the relative velocity along the tangent.
        /// Negative means the body is lagging behind the surface.
        /// Positive means it is moving faster than the surface.
        /// </summary>
        public float GetTangentialSpeed(Rigidbody2D body)
        {
            return Vector2.Dot(GetRelativeVelocity(body), Tangent);
        }

#if UNITY_EDITOR

        /// <summary>
        /// Debug drawing helper.
        /// </summary>
        public void DrawGizmos(float scale = 0.35f)
        {
            Debug.DrawLine(
                Point,
                Point + Normal * scale,
                Color.green);

            Debug.DrawLine(
                Point,
                Point + Tangent * scale,
                Color.blue);

            Debug.DrawLine(
                Point,
                Point + SurfaceVelocity * 0.1f,
                Color.red);
        }

#endif
    }
}