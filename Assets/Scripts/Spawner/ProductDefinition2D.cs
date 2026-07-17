using System;
using UnityEngine;

namespace PhysicsWidgets2D
{
    public enum ProductReleaseRate2D
    {
        Slow,
        Medium,
        Fast
    }

    [Serializable]
    public class ProductVisualVariant2D
    {
        [SerializeField] private string displayName = "Variant";
        [SerializeField] private Sprite sprite;

        public string DisplayName => displayName;
        public Sprite Sprite => sprite;
    }

    [CreateAssetMenu(fileName = "ProductDefinition2D", menuName = "Physics2D/Product Definition 2D")]
    public class ProductDefinition2D : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string productName = "Apple";
        [SerializeField] private GameObject physicsPrefab;

        [Header("Visual Variants")]
        [SerializeField] private ProductVisualVariant2D[] normalVisuals = Array.Empty<ProductVisualVariant2D>();
        [SerializeField] private ProductVisualVariant2D[] defectiveVisuals = Array.Empty<ProductVisualVariant2D>();

        [Header("Release Timing")]
        [SerializeField, Min(0.01f)] private float slowReleaseDelay = 1f;
        [SerializeField, Min(0.01f)] private float mediumReleaseDelay = 0.5f;
        [SerializeField, Min(0.01f)] private float fastReleaseDelay = 0.2f;
        [Tooltip("Maximum proportional variation around the selected delay. 0.15 means plus or minus 15 percent.")]
        [SerializeField, Range(0f, 0.95f)] private float releaseDelayJitter = 0.15f;

        [Header("Mass")]
        [SerializeField, Min(0.001f)] private float nominalMass = 1f;
        [Tooltip("Maximum proportional variation around nominal mass. 0.10 means plus or minus 10 percent.")]
        [SerializeField, Range(0f, 0.95f)] private float massVariation = 0.10f;

        [Header("Spawn Variation")]
        [Tooltip("Maximum seeded world-space offset from the emitter position.")]
        [SerializeField] private Vector2 spawnPositionVariation = new Vector2(0.05f, 0f);
        [SerializeField] private Vector2 initialRotationRange = new Vector2(0f, 360f);

        public string ProductName => productName;
        public GameObject PhysicsPrefab => physicsPrefab;
        public float NominalMass => nominalMass;
        public float ReleaseDelayJitter => releaseDelayJitter;
        public float MassVariation => massVariation;
        public Vector2 SpawnPositionVariation => spawnPositionVariation;
        public Vector2 InitialRotationRange => initialRotationRange;
        public int NormalVisualCount => normalVisuals?.Length ?? 0;
        public int DefectiveVisualCount => defectiveVisuals?.Length ?? 0;

        public float GetBaseReleaseDelay(ProductReleaseRate2D rate)
        {
            switch(rate)
            {
                case ProductReleaseRate2D.Slow: return slowReleaseDelay;
                case ProductReleaseRate2D.Fast: return fastReleaseDelay;
                default: return mediumReleaseDelay;
            }
        }

        public ProductVisualVariant2D GetVisual(bool defective, int index)
        {
            ProductVisualVariant2D[] collection = defective ? defectiveVisuals : normalVisuals;
            if(collection == null || collection.Length == 0) return null;
            index = Mathf.Clamp(index, 0, collection.Length - 1);
            return collection[index];
        }

        private void OnValidate()
        {
            slowReleaseDelay = Mathf.Max(0.01f, slowReleaseDelay);
            mediumReleaseDelay = Mathf.Max(0.01f, mediumReleaseDelay);
            fastReleaseDelay = Mathf.Max(0.01f, fastReleaseDelay);
            nominalMass = Mathf.Max(0.001f, nominalMass);
            if(initialRotationRange.x > initialRotationRange.y)
                initialRotationRange = new Vector2(initialRotationRange.y, initialRotationRange.x);
        }
    }
}