using UnityEngine;

namespace PhysicsWidgets2D
{
    /// <summary>
    /// Interface for physics components that affect bodies while they are
    /// inside a volume.
    ///
    /// Volume providers do not directly modify Rigidbody2D state.
    /// Instead, they contribute environmental effects to PhysicsFrame2D.
    ///
    /// Examples:
    /// - Water
    /// - Oil
    /// - Mud
    /// - Gas clouds
    /// - Low gravity zones
    /// - Zero gravity zones
    /// </summary>
    public interface IVolumeProvider2D
    {
        /// <summary>
        /// Evaluates the volume effects applied to a Rigidbody2D.
        ///
        /// Implementations should:
        /// - Determine how much of the body is affected.
        /// - Add forces, damping, or other environmental effects
        ///   to the PhysicsFrame2D.
        ///
        /// Implementations should NOT:
        /// - Call Rigidbody2D.AddForce().
        /// - Directly modify Rigidbody2D.drag.
        /// - Directly modify Rigidbody2D.velocity.
        /// - Move transforms.
        /// </summary>
        /// <param name="body">
        /// The Rigidbody2D currently inside the volume.
        /// </param>
        /// <param name="frame">
        /// The current physics frame to contribute to.
        /// </param>
        void EvaluateVolume(
            Rigidbody2D body,
            PhysicsFrame2D frame);
    }
}