using System.Collections.Generic;
using UnityEngine;

namespace PhysicsWidgets2D
{
    /// <summary>
    /// A force applied at a specific world position.
    ///
    /// Used for effects where the application point matters:
    /// - buoyancy
    /// - explosions
    /// - thrusters
    /// - magnets
    /// </summary>
    public struct ForceApplication2D
    {
        public Vector2 Force;
        public Vector2 Position;


        public ForceApplication2D(
            Vector2 force,
            Vector2 position)
        {
            Force = force;
            Position = position;
        }
    }



    /// <summary>
    /// Collection of physics effects generated during one FixedUpdate step.
    ///
    /// Physics widgets contribute their effects here.
    ///
    /// PhysicsSolver2D consumes this frame and applies the final result.
    /// </summary>
    public class PhysicsFrame2D
    {
        /// <summary>
        /// Forces applied through the center of mass.
        /// </summary>
        public Vector2 Force
        {
            get;
            private set;
        }



        /// <summary>
        /// Forces applied at specific world positions.
        /// </summary>
        public readonly List<ForceApplication2D>
            ForceApplications;



        /// <summary>
        /// Additional torque.
        /// </summary>
        public float Torque
        {
            get;
            private set;
        }



        public float LinearDamping
        {
            get;
            private set;
        }



        public float AngularDamping
        {
            get;
            private set;
        }



        public readonly List<ContactConstraint2D>
            ContactConstraints;



        public PhysicsFrame2D()
        {
            ForceApplications =
                new List<ForceApplication2D>(16);


            ContactConstraints =
                new List<ContactConstraint2D>(16);


            Reset();
        }



        public void Reset()
        {
            Force =
                Vector2.zero;


            Torque =
                0f;


            LinearDamping =
                0f;


            AngularDamping =
                0f;


            ForceApplications.Clear();

            ContactConstraints.Clear();
        }



        /// <summary>
        /// Adds a force through the center of mass.
        /// </summary>
        public void AddForce(
            Vector2 force)
        {
            Force += force;
        }



        /// <summary>
        /// Adds a force applied at a world position.
        /// </summary>
        public void AddForceAtPosition(
            Vector2 force,
            Vector2 position)
        {
            ForceApplications.Add(
                new ForceApplication2D(
                    force,
                    position));
        }



        public void AddTorque(
            float torque)
        {
            Torque += torque;
        }



        public void AddLinearDamping(
            float damping)
        {
            LinearDamping +=
                Mathf.Max(
                    0f,
                    damping);
        }



        public void AddAngularDamping(
            float damping)
        {
            AngularDamping +=
                Mathf.Max(
                    0f,
                    damping);
        }



        public void AddContactConstraint(
            ContactConstraint2D constraint)
        {
            ContactConstraints.Add(
                constraint);
        }
    }
}