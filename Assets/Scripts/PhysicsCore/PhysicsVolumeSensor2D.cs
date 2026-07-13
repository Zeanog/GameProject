using UnityEngine;

namespace PhysicsWidgets2D
{
    /// <summary>
    /// Detects volume-based physics effects using Unity trigger colliders.
    ///
    /// Attach this component to the same GameObject as a Rigidbody2D
    /// and PhysicsReceiver2D.
    ///
    /// It tracks all IVolumeProvider2D components currently affecting
    /// the body and registers/unregisters them automatically.
    ///
    /// Examples:
    /// - Water
    /// - Gas
    /// - Low gravity zones
    /// - Slow fields
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(PhysicsReceiver2D))]
    public class PhysicsVolumeSensor2D :
        MonoBehaviour
    {
        private PhysicsReceiver2D receiver;



        private void Awake()
        {
            receiver =
                GetComponent<PhysicsReceiver2D>();
        }



        private void OnTriggerEnter2D(
            Collider2D other)
        {
            IVolumeProvider2D provider =
                other.GetComponentInParent<IVolumeProvider2D>();

            if(provider != null)
            {
                Debug.Log(
                    $"Entered volume: {provider}");

                receiver.RegisterVolumeProvider(provider);
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            IVolumeProvider2D provider =
                other.GetComponentInParent<IVolumeProvider2D>();

            if(provider != null)
            {
                Debug.Log(
                    $"Exited volume: {provider}");

                receiver.UnregisterVolumeProvider(provider);
            }
        }
    }
}