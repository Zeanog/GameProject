using UnityEngine;

namespace PhysicsWidgets2D
{
    /// <summary>
    /// Interface for physics components that apply continuous forces
    /// to Rigidbody2D objects.
    ///
    /// Force providers contribute to PhysicsFrame2D during a physics step.
    /// The PhysicsSolver2D later applies the accumulated force to the body.
    ///
    /// Examples:
    /// - Fans
    /// - Wind fields
    /// - Magnets
    /// - Thrusters
    /// - Gravity wells
    /// </summary>
    public interface IForceProvider2D
    {
        /// <summary>
        /// Evaluates the force contribution from this provider.
        ///
        /// Implementations should:
        /// - Calculate the force based on the body's position,
        ///   distance, orientation, or other parameters.
        /// - Add the result to the PhysicsFrame2D.
        ///
        /// Implementations should NOT:
        /// - Call Rigidbody2D.AddForce().
        /// - Modify Rigidbody2D.velocity.
        /// - Move transforms.
        /// </summary>
        /// <param name="body">
        /// The Rigidbody2D receiving the effect.
        /// </param>
        /// <param name="frame">
        /// The current physics frame to contribute to.
        /// </param>
        void EvaluateForce(
            Rigidbody2D body,
            PhysicsFrame2D frame);
    }
}