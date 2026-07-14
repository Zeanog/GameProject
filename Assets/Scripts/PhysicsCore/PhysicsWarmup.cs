using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PhysicsWidgets2D
{
    /// <summary>
    /// Delays the start of dynamic 2D physics so scene initialization,
    /// debug geometry generation, rendering, and other startup work can
    /// complete before the simulation begins.
    ///
    /// Add one instance to the scene.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    [AddComponentMenu("Physics2D/Simulation Warmup 2D")]
    public class PhysicsWarmup : MonoBehaviour
    {
        [Header("Warmup")]

        [Tooltip(
            "Real-time delay before dynamic physics begins. " +
            "This is not affected by Time.timeScale.")]
        [SerializeField]
        [Min(0f)]
        private float warmupSeconds = 0.5f;

        [Tooltip(
            "Minimum rendered frames allowed before physics begins. " +
            "This ensures initialization can span multiple frames.")]
        [SerializeField]
        [Min(1)]
        private int minimumWarmupFrames = 5;

        [Tooltip(
            "Automatically finds every simulated Dynamic Rigidbody2D " +
            "in the loaded scene.")]
        [SerializeField]
        private bool findDynamicBodiesAutomatically = true;

        [Tooltip(
            "Additional Rigidbody2D objects to pause. This is useful " +
            "when automatic discovery is disabled.")]
        [SerializeField]
        private Rigidbody2D[] additionalBodies;

        [Header("Status")]

        [SerializeField]
        private bool logWarmup = false;

        public bool IsWarmingUp
        {
            get;
            private set;
        }

        public bool IsSimulationRunning
        {
            get;
            private set;
        }

        private readonly List<BodyState> bodyStates =
            new List<BodyState>();


        private sealed class BodyState
        {
            public Rigidbody2D Body;
            public Vector2 Position;
            public float Rotation;
            public Vector2 LinearVelocity;
            public float AngularVelocity;
            public bool Simulated;
        }


        private void Awake()
        {
            CaptureBodies();
            PauseBodies();
        }


        private IEnumerator Start()
        {
            IsWarmingUp = true;
            IsSimulationRunning = false;

            if(logWarmup)
            {
                Debug.Log(
                    $"{name}: Beginning {warmupSeconds:F2}-second " +
                    $"simulation warmup for {bodyStates.Count} bodies.");
            }

            float startTime = Time.realtimeSinceStartup;
            int elapsedFrames = 0;

            while(Time.realtimeSinceStartup - startTime < warmupSeconds ||
                  elapsedFrames < minimumWarmupFrames)
            {
                elapsedFrames++;
                yield return null;
            }

            StartSimulation();
        }


        private void CaptureBodies()
        {
            bodyStates.Clear();

            if(findDynamicBodiesAutomatically)
            {
                Rigidbody2D[] sceneBodies =
                    FindObjectsByType<Rigidbody2D>(
                        FindObjectsInactive.Exclude,
                        FindObjectsSortMode.None);

                for(int i = 0; i < sceneBodies.Length; i++)
                {
                    Rigidbody2D body = sceneBodies[i];

                    if(body.bodyType == RigidbodyType2D.Dynamic)
                        AddBody(body);
                }
            }

            if(additionalBodies == null)
                return;

            for(int i = 0; i < additionalBodies.Length; i++)
                AddBody(additionalBodies[i]);
        }


        private void AddBody(Rigidbody2D body)
        {
            if(body == null)
                return;

            for(int i = 0; i < bodyStates.Count; i++)
            {
                if(bodyStates[i].Body == body)
                    return;
            }

            bodyStates.Add(
                new BodyState
                {
                    Body = body,
                    Position = body.position,
                    Rotation = body.rotation,
                    LinearVelocity = body.linearVelocity,
                    AngularVelocity = body.angularVelocity,
                    Simulated = body.simulated
                });
        }


        private void PauseBodies()
        {
            for(int i = 0; i < bodyStates.Count; i++)
            {
                BodyState state = bodyStates[i];

                if(state.Body != null &&
                   state.Simulated)
                {
                    state.Body.simulated = false;
                }
            }
        }


        private void StartSimulation()
        {
            for(int i = 0; i < bodyStates.Count; i++)
            {
                BodyState state = bodyStates[i];
                Rigidbody2D body = state.Body;

                if(body == null)
                    continue;

                body.position = state.Position;
                body.rotation = state.Rotation;
                body.linearVelocity = state.LinearVelocity;
                body.angularVelocity = state.AngularVelocity;
                body.simulated = state.Simulated;

                if(state.Simulated)
                    body.WakeUp();
            }

            Physics2D.SyncTransforms();

            IsWarmingUp = false;
            IsSimulationRunning = true;

            if(logWarmup)
            {
                Debug.Log(
                    $"{name}: Warmup complete. " +
                    "Dynamic physics simulation started.");
            }
        }
    }
}