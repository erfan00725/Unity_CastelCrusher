using UnityEngine;

public class DO_LowHealthRedTint : MonoBehaviour
{
    [SerializeField] private Renderer targetRenderer;
    [SerializeField] private Color fullHealthColor = Color.white;
    [SerializeField] private Color lowHealthColor = Color.lightCoral;
    
    // Shader property ID ("_BaseColor" for URP/HDRP, "_Color" for Built-in standard shader)
    private static readonly int ColorPropertyId = Shader.PropertyToID("_BaseColor");
    
    private MaterialPropertyBlock propertyBlock;

    private void Awake()
    {
        if (targetRenderer == null)
            targetRenderer = GetComponent<Renderer>();

        propertyBlock = new MaterialPropertyBlock();
    }
    
    /// <summary>
    /// Call this whenever health changes.
    /// </summary>
    /// <param name="currentHealth">Current health value</param>
    /// <param name="maxHealth">Maximum health value</param>
    public void UpdateHealthColor(float currentHealth, float maxHealth)
    {
        // Calculate health percentage clamped between 0 and 1
        float healthPercent = Mathf.Clamp01(currentHealth / maxHealth);

        // Interpolate: 1 = fullHealthColor, 0 = lowHealthColor
        Color targetColor = Color.Lerp(lowHealthColor, fullHealthColor, healthPercent);

        // Apply via MaterialPropertyBlock to avoid creating new material instances
        targetRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor(ColorPropertyId, targetColor);
        targetRenderer.SetPropertyBlock(propertyBlock);
    }
}
