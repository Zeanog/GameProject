using UnityEngine;

namespace PhysicsWidgets2D
{
    /// <summary>
    /// Central solver for custom environmental physics interactions.
    ///
    /// Unity Physics2D remains responsible for:
    /// - collision detection
    /// - collision resolution
    /// - gravity
    /// - rigidbody integration
    ///
    /// This solver handles:
    /// - environmental forces
    /// - environmental forces applied at positions
    /// - environmental damping
    /// - moving surface constraints
    /// </summary>
    public class PhysicsSolver2D : MonoBehaviour
    {
        public static PhysicsSolver2D Instance
        {
            get;
            private set;
        }

        private void Awake()
        {
            if(Instance != null &&
                Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }


        /// <summary>
        /// Solves all environmental physics contributions.
        /// </summary>
        public void Solve(
            Rigidbody2D body,
            PhysicsFrame2D frame)
        {
            ApplyForces(body, frame);
            ApplyDamping(body, frame);
            SolveContacts(body, frame);
        }


        /// <summary>
        /// Applies accumulated external forces.
        /// </summary>
        private void ApplyForces(
            Rigidbody2D body,
            PhysicsFrame2D frame)
        {
            // Forces applied through the center of mass.
            if(frame.Force != Vector2.zero)
            {
                body.AddForce(
                    frame.Force,
                    ForceMode2D.Force);
            }

            // Forces applied at specific world positions.
            //
            // Examples:
            // - center of buoyancy
            // - explosion impulse
            // - thruster location
            //
            // Unity automatically converts these
            // into linear force + rotational torque.
            for(int i = 0; i < frame.ForceApplications.Count; i++)
            {
                ForceApplication2D application =
                    frame.ForceApplications[i];

                body.AddForceAtPosition(
                    application.Force,
                    application.Position,
                    ForceMode2D.Force);
            }

            if(!Mathf.Approximately(frame.Torque, 0f))
                body.AddTorque(frame.Torque, ForceMode2D.Force);
        }


        /// <summary>
        /// Applies environmental damping effects.
        ///
        /// This is implemented as a velocity-proportional force:
        ///
        ///     F = -velocity * damping
        ///
        /// This allows:
        /// - water slowing objects
        /// - fans pushing against drag
        /// - multiple volumes stacking
        /// </summary>
        private void ApplyDamping(
            Rigidbody2D body,
            PhysicsFrame2D frame)
        {
            float linearDamping = frame.LinearDamping;

            if(linearDamping > 0f)
            {
                Vector2 dampingForce =
                    -body.linearVelocity *
                    linearDamping *
                    body.mass;

                body.AddForce(dampingForce, ForceMode2D.Force);
            }

            float angularDamping = frame.AngularDamping;

            if(angularDamping > 0f)
            {
                float angularVelocityRadians =
                    body.angularVelocity *
                    Mathf.Deg2Rad;

                float dampingTorque =
                    -angularVelocityRadians *
                    angularDamping *
                    body.inertia;

                body.AddTorque(
                    dampingTorque,
                    ForceMode2D.Force);
            }
        }


        /// <summary>
        /// Solves moving contact constraints.
        ///
        /// Each contact receives one bounded impulse. Repeated force
        /// iterations are intentionally avoided because Rigidbody2D
        /// velocity is not integrated between calls.
        /// </summary>
        private void SolveContacts(
            Rigidbody2D body,
            PhysicsFrame2D frame)
        {
            if(body.bodyType != RigidbodyType2D.Dynamic ||
               frame.ContactConstraints.Count == 0)
            {
                return;
            }

            for(int i = 0; i < frame.ContactConstraints.Count; i++)
                SolveContact(body, frame.ContactConstraints[i]);
        }


        /// <summary>
        /// Solves one moving surface constraint.
        ///
        /// The impulse is calculated from the effective mass at the contact
        /// point and limited so one contact cannot intentionally overshoot
        /// the surface velocity.
        /// </summary>
        private void SolveContact(
            Rigidbody2D body,
            ContactConstraint2D constraint)
        {
            Vector2 tangent = constraint.Tangent.normalized;

            if(tangent.sqrMagnitude < 0.000001f)
                return;

            Vector2 pointVelocity =
                body.GetPointVelocity(constraint.Point);

            Vector2 relativeVelocity =
                pointVelocity -
                constraint.SurfaceVelocity;

            float tangentSpeed =
                Vector2.Dot(
                    relativeVelocity,
                    tangent);

            if(Mathf.Abs(tangentSpeed) < 0.001f)
                return;

            float inverseMass =
                body.mass > 0f
                ? 1f / body.mass
                : 0f;

            Vector2 contactOffset =
                constraint.Point -
                body.worldCenterOfMass;

            float leverArm =
                contactOffset.x * tangent.y -
                contactOffset.y * tangent.x;

            float inverseInertia =
                body.inertia > 0f
                ? 1f / body.inertia
                : 0f;

            float effectiveInverseMass =
                inverseMass +
                leverArm *
                leverArm *
                inverseInertia;

            if(effectiveInverseMass <= 0f)
                return;

            float correctionStrength =
                Mathf.Clamp01(
                    constraint.Friction *
                    constraint.Strength);

            float impulseMagnitude =
                -tangentSpeed /
                effectiveInverseMass *
                correctionStrength;

            Vector2 impulse =
                tangent *
                impulseMagnitude;

            body.AddForceAtPosition(
                impulse,
                constraint.Point,
                ForceMode2D.Impulse);
        }
    }
}