using UnityEngine;

public class ConveyorBeltAnimator : MonoBehaviour
{
    private Renderer targetRenderer;

    public float TextureSpeed
    {
        get;
        set;
    } = 0.0f;

    protected void Awake()
    {
        targetRenderer = GetComponent<Renderer>();
    }

    private void Update()
    {
        if (targetRenderer != null)
        {
            var textureOffset = targetRenderer.material.mainTextureOffset;
            textureOffset.y += TextureSpeed * Time.deltaTime;
            targetRenderer.material.mainTextureOffset = textureOffset;
        }
    }
}