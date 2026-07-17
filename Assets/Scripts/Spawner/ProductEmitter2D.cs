using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PhysicsWidgets2D
{
    public enum ProductEmitterState2D
    {
        Idle,
        StartDelay,
        WaitingForRelease,
        Blocked,
        Paused,
        Complete
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("Physics2D/Product Emitter 2D")]
    public class ProductEmitter2D : MonoBehaviour
    {
        [Header("Batch")]
        [SerializeField] private ProductDefinition2D productDefinition;
        [SerializeField, Min(1)] private int quantity = 50;
        [SerializeField] private ProductReleaseRate2D releaseRate = ProductReleaseRate2D.Medium;
        [SerializeField, Range(0f, 100f)] private float defectPercentage = 5f;
        [SerializeField] private int randomSeed = 48291;
        [SerializeField, Min(0f)] private float startDelay = 0f;
        [SerializeField] private bool startAutomatically = true;

        [Header("Emitter")]
        [Tooltip("Products spawn at this transform. If omitted, this GameObject's transform is used.")]
        [SerializeField] private Transform spawnPoint;
        [Tooltip("World-space clearance box checked before each product is released.")]
        [SerializeField] private Vector2 spawnClearanceSize = new Vector2(1.1f, 1.1f);
        [SerializeField] private LayerMask obstructionLayers = ~0;
        [SerializeField] private bool ignoreTriggerObstructions = true;
        [SerializeField] private Transform spawnedProductParent;
        [SerializeField] private bool clearSpawnedProductsOnRestart = true;

        [Header("Debug")]
        [SerializeField] private bool logStateChanges = false;
        [SerializeField] private bool showRuntimeOverlay = true;
        [SerializeField] private Vector2 overlayPosition = new Vector2(12f, 12f);

        [Header("Runtime Status")]
        [SerializeField] private ProductEmitterState2D state = ProductEmitterState2D.Idle;
        [SerializeField] private int spawnedCount;
        [SerializeField] private int remainingCount;
        [SerializeField] private int activeSeed;
        [SerializeField] private float nextReleaseCountdown;
        [SerializeField] private float blockedDuration;

        public ProductEmitterState2D State => state;
        public int SpawnedCount => spawnedCount;
        public int RemainingCount => remainingCount;
        public int ActiveSeed => activeSeed;
        public bool IsPaused => paused;

        private readonly List<ManifestEntry> manifest = new List<ManifestEntry>();
        private readonly List<GameObject> spawnedProducts = new List<GameObject>();
        private Coroutine batchCoroutine;
        private bool paused;
        private ProductEmitterState2D stateBeforePause = ProductEmitterState2D.Idle;

        [Serializable]
        private struct ManifestEntry
        {
            public bool Defective;
            public int VisualIndex;
            public float Mass;
            public float DelayAfterSpawn;
            public Vector2 SpawnOffset;
            public float InitialRotation;
        }

        private void Start()
        {
            if(startAutomatically) RestartSameSeed();
        }

        private void OnValidate()
        {
            quantity = Mathf.Max(1, quantity);
            startDelay = Mathf.Max(0f, startDelay);
            spawnClearanceSize.x = Mathf.Max(0.01f, spawnClearanceSize.x);
            spawnClearanceSize.y = Mathf.Max(0.01f, spawnClearanceSize.y);
        }

        [ContextMenu("Start Batch")]
        public void StartBatch() { BeginBatch(randomSeed, clearSpawnedProductsOnRestart); }

        [ContextMenu("Pause or Resume")]
        public void TogglePause() { SetPaused(!paused); }

        [ContextMenu("Restart Same Seed")]
        public void RestartSameSeed() { BeginBatch(randomSeed, clearSpawnedProductsOnRestart); }

        [ContextMenu("Restart New Seed")]
        public void RestartNewSeed()
        {
            randomSeed = GenerateNewSeed();
            BeginBatch(randomSeed, clearSpawnedProductsOnRestart);
        }

        [ContextMenu("Stop and Clear")]
        public void StopAndClear()
        {
            StopBatch();
            ClearSpawnedProducts();
            manifest.Clear();
            spawnedCount = 0;
            remainingCount = 0;
            nextReleaseCountdown = 0f;
            blockedDuration = 0f;
            SetState(ProductEmitterState2D.Idle);
        }

        public void SetPaused(bool shouldPause)
        {
            if(paused == shouldPause) return;
            paused = shouldPause;
            if(paused)
            {
                stateBeforePause = state;
                SetState(ProductEmitterState2D.Paused);
            }
            else SetState(stateBeforePause);
        }

        private void BeginBatch(int seed, bool clearExistingProducts)
        {
            StopBatch();
            if(productDefinition == null || productDefinition.PhysicsPrefab == null)
            {
                Debug.LogError($"{name}: ProductEmitter2D requires a Product Definition with a physics prefab.", this);
                SetState(ProductEmitterState2D.Idle);
                return;
            }

            if(clearExistingProducts) ClearSpawnedProducts();
            activeSeed = seed;
            paused = false;
            blockedDuration = 0f;
            spawnedCount = 0;
            remainingCount = quantity;
            nextReleaseCountdown = startDelay;
            GenerateManifest(seed);
            batchCoroutine = StartCoroutine(RunBatch());
        }

        private void StopBatch()
        {
            if(batchCoroutine != null)
            {
                StopCoroutine(batchCoroutine);
                batchCoroutine = null;
            }
            paused = false;
        }

        private IEnumerator RunBatch()
        {
            if(startDelay > 0f)
            {
                SetState(ProductEmitterState2D.StartDelay);
                yield return WaitForDuration(startDelay);
            }

            for(int i = 0; i < manifest.Count; i++)
            {
                ManifestEntry entry = manifest[i];
                bool reportedBlocked = false;

                while(IsSpawnAreaObstructed(entry))
                {
                    if(!reportedBlocked)
                    {
                        blockedDuration = 0f;
                        SetState(ProductEmitterState2D.Blocked);
                        reportedBlocked = true;
                    }

                    yield return WaitWhilePaused();
                    blockedDuration += Time.fixedDeltaTime;
                    yield return new WaitForFixedUpdate();
                }

                blockedDuration = 0f;
                SpawnProduct(i, entry);
                spawnedCount = i + 1;
                remainingCount = manifest.Count - spawnedCount;

                if(i < manifest.Count - 1)
                {
                    SetState(ProductEmitterState2D.WaitingForRelease);
                    yield return WaitForDuration(entry.DelayAfterSpawn);
                }
            }

            nextReleaseCountdown = 0f;
            batchCoroutine = null;
            SetState(ProductEmitterState2D.Complete);
        }

        private IEnumerator WaitForDuration(float duration)
        {
            nextReleaseCountdown = duration;
            while(nextReleaseCountdown > 0f)
            {
                yield return WaitWhilePaused();
                nextReleaseCountdown -= Time.deltaTime;
                yield return null;
            }
            nextReleaseCountdown = 0f;
        }

        private IEnumerator WaitWhilePaused()
        {
            while(paused) yield return null;
        }

        private void GenerateManifest(int seed)
        {
            manifest.Clear();
            System.Random random = new System.Random(seed);
            float baseDelay = productDefinition.GetBaseReleaseDelay(releaseRate);

            for(int i = 0; i < quantity; i++)
            {
                ManifestEntry entry = new ManifestEntry();
                entry.Defective = Next01(random) < defectPercentage / 100f;

                int visualCount = entry.Defective
                    ? productDefinition.DefectiveVisualCount
                    : productDefinition.NormalVisualCount;

                entry.VisualIndex = visualCount > 0 ? random.Next(0, visualCount) : -1;

                float massFactor = 1f + NextSigned(random) * productDefinition.MassVariation;
                entry.Mass = Mathf.Max(0.001f, productDefinition.NominalMass * massFactor);

                float delayFactor = 1f + NextSigned(random) * productDefinition.ReleaseDelayJitter;
                entry.DelayAfterSpawn = Mathf.Max(0.01f, baseDelay * delayFactor);

                Vector2 variation = productDefinition.SpawnPositionVariation;
                entry.SpawnOffset = new Vector2(
                    NextSigned(random) * variation.x,
                    NextSigned(random) * variation.y);

                Vector2 rotationRange = productDefinition.InitialRotationRange;
                entry.InitialRotation = Mathf.Lerp(rotationRange.x, rotationRange.y, Next01(random));
                manifest.Add(entry);
            }
        }

        private void SpawnProduct(int productIndex, ManifestEntry entry)
        {
            Transform emitterTransform = spawnPoint != null ? spawnPoint : transform;
            Vector3 position = emitterTransform.position + (Vector3)entry.SpawnOffset;
            Quaternion rotation = Quaternion.Euler(0f, 0f, entry.InitialRotation);

            GameObject instance = Instantiate(
                productDefinition.PhysicsPrefab,
                position,
                rotation,
                spawnedProductParent);

            instance.name = $"{productDefinition.ProductName} {productIndex + 1:000}";

            Rigidbody2D body = instance.GetComponent<Rigidbody2D>();
            if(body != null) body.mass = entry.Mass;

            SpawnedProduct2D product = instance.GetComponent<SpawnedProduct2D>();
            if(product == null) product = instance.AddComponent<SpawnedProduct2D>();

            product.Configure(
                productDefinition,
                activeSeed,
                productIndex,
                entry.Defective,
                entry.VisualIndex,
                entry.Mass);

            spawnedProducts.Add(instance);
        }

        private bool IsSpawnAreaObstructed(ManifestEntry entry)
        {
            Transform emitterTransform = spawnPoint != null ? spawnPoint : transform;
            Vector2 center = (Vector2)emitterTransform.position + entry.SpawnOffset;
            Collider2D[] overlaps = Physics2D.OverlapBoxAll(center, spawnClearanceSize, 0f, obstructionLayers);

            for(int i = 0; i < overlaps.Length; i++)
            {
                Collider2D overlap = overlaps[i];
                if(overlap == null) continue;
                if(ignoreTriggerObstructions && overlap.isTrigger) continue;

                Transform overlapTransform = overlap.transform;
                if(overlapTransform == transform || overlapTransform.IsChildOf(transform)) continue;
                return true;
            }

            return false;
        }

        private void ClearSpawnedProducts()
        {
            for(int i = spawnedProducts.Count - 1; i >= 0; i--)
            {
                GameObject product = spawnedProducts[i];
                if(product == null) continue;
                product.SetActive(false);
                if(Application.isPlaying) Destroy(product);
                else DestroyImmediate(product);
            }
            spawnedProducts.Clear();
        }

        private void SetState(ProductEmitterState2D newState)
        {
            if(state == newState) return;
            state = newState;
            if(logStateChanges) Debug.Log($"{name}: Product emitter state = {state}", this);
        }

        private void OnGUI()
        {
            if(!showRuntimeOverlay || !Application.isPlaying) return;

            string text =
                $"{name}\n" +
                $"State: {state}\n" +
                $"Seed: {activeSeed}\n" +
                $"Spawned: {spawnedCount} / {quantity}\n" +
                $"Remaining: {remainingCount}\n" +
                $"Next release: {nextReleaseCountdown:F2}s";

            if(state == ProductEmitterState2D.Blocked)
                text += $"\nBlocked: {blockedDuration:F2}s";

            GUI.Box(
                new Rect(
                    overlayPosition.x,
                    overlayPosition.y,
                    210f,
                    state == ProductEmitterState2D.Blocked ? 130f : 110f),
                text);
        }

        private void OnDrawGizmosSelected()
        {
            Transform emitterTransform = spawnPoint != null ? spawnPoint : transform;
            Gizmos.DrawWireCube(emitterTransform.position, spawnClearanceSize);
        }

        private static float Next01(System.Random random) { return (float)random.NextDouble(); }
        private static float NextSigned(System.Random random) { return Next01(random) * 2f - 1f; }

        private static int GenerateNewSeed()
        {
            unchecked
            {
                long ticks = DateTime.UtcNow.Ticks;
                return (int)(ticks ^ (ticks >> 32) ^ Environment.TickCount);
            }
        }
    }
}