using UnityEngine;

namespace PhysicsWidgets2D
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Physics2D/Spawned Product 2D")]
    public class SpawnedProduct2D : MonoBehaviour
    {
        [Header("Visual")]
        [Tooltip("Renderer that receives the selected product sprite. If omitted, the first child SpriteRenderer is used.")]
        [SerializeField] private SpriteRenderer visualRenderer;

        [Header("Runtime Product Data")]
        [SerializeField] private ProductDefinition2D productDefinition;
        [SerializeField] private int batchSeed;
        [SerializeField] private int productIndex;
        [SerializeField] private bool defective;
        [SerializeField] private int visualIndex;
        [SerializeField] private float generatedMass;

        public ProductDefinition2D ProductDefinition => productDefinition;
        public int BatchSeed => batchSeed;
        public int ProductIndex => productIndex;
        public bool IsDefective => defective;
        public int VisualIndex => visualIndex;
        public float GeneratedMass => generatedMass;

        public void Configure(ProductDefinition2D definition, int seed, int index, bool isDefective, int selectedVisualIndex, float mass)
        {
            productDefinition = definition;
            batchSeed = seed;
            productIndex = index;
            defective = isDefective;
            visualIndex = selectedVisualIndex;
            generatedMass = mass;
            ApplyVisual();
        }

        private void Awake() { FindVisualRenderer(); }
        private void OnValidate() { FindVisualRenderer(); }

        private void FindVisualRenderer()
        {
            if(visualRenderer == null)
                visualRenderer = GetComponentInChildren<SpriteRenderer>(true);
        }

        private void ApplyVisual()
        {
            FindVisualRenderer();
            if(visualRenderer == null || productDefinition == null) return;
            ProductVisualVariant2D variant = productDefinition.GetVisual(defective, visualIndex);
            if(variant != null) visualRenderer.sprite = variant.Sprite;
        }
    }
}