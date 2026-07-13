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



        [Header("Contact Solver")]

        [SerializeField]
        private int contactIterations = 8;



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
            ApplyForces(
                body,
                frame);


            ApplyDamping(
                body,
                frame);


            SolveContacts(
                body,
                frame);
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
            for(int i = 0;
                i < frame.ForceApplications.Count;
                i++)
            {
                ForceApplication2D application =
                    frame.ForceApplications[i];


                body.AddForceAtPosition(
                    application.Force,
                    application.Position,
                    ForceMode2D.Force);
            }



            if(!Mathf.Approximately(
                frame.Torque,
                0f))
            {
                body.AddTorque(
                    frame.Torque,
                    ForceMode2D.Force);
            }
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
            float linearDamping =
                frame.LinearDamping;


            if(linearDamping > 0f)
            {
                Vector2 dampingForce =
                    -body.linearVelocity *
                    linearDamping *
                    body.mass;


                body.AddForce(
                    dampingForce,
                    ForceMode2D.Force);
            }



            float angularDamping =
                frame.AngularDamping;


            if(angularDamping > 0f)
            {
                float angularVelocityRadians =
                    body.angularVelocity * Mathf.Deg2Rad;

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
        /// Multiple conveyors are solved together.
        /// </summary>
        private void SolveContacts(
            Rigidbody2D body,
            PhysicsFrame2D frame)
        {
            if(frame.ContactConstraints.Count == 0)
                return;



            for(int iteration = 0;
                iteration < contactIterations;
                iteration++)
            {
                for(int i = 0;
                    i < frame.ContactConstraints.Count;
                    i++)
                {
                    SolveContact(
                        body,
                        frame.ContactConstraints[i]);
                }
            }
        }



        /// <summary>
        /// Solves one moving surface constraint.
        /// </summary>
        private void SolveContact(
            Rigidbody2D body,
            ContactConstraint2D constraint)
        {
            Vector2 pointVelocity =
                body.GetPointVelocity(
                    constraint.Point);



            Vector2 relativeVelocity =
                pointVelocity -
                constraint.SurfaceVelocity;



            float tangentSpeed =
                Vector2.Dot(
                    relativeVelocity,
                    constraint.Tangent);



            if(Mathf.Abs(tangentSpeed) < 0.001f)
            {
                return;
            }



            float impulse =
                -tangentSpeed *
                constraint.Friction *
                constraint.Strength;



            Vector2 force =
                constraint.Tangent *
                impulse *
                body.mass /
                Time.fixedDeltaTime;



            body.AddForceAtPosition(
                force,
                constraint.Point,
                ForceMode2D.Force);
        }
    }
}