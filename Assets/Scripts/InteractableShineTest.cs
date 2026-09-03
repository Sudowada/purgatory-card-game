using System.Collections;
using UnityEngine;

public class InteractableShineTest : MonoBehaviour
{
    [Header("Shine")]
    [SerializeField] private Renderer shineRenderer;

    [SerializeField] private float shineDuration = 0.5f;
    [SerializeField] private float delayBetweenShines = 2f;

    [SerializeField] private float startProgress = -0.2f;
    [SerializeField] private float endProgress = 2.2f;

    private MaterialPropertyBlock propertyBlock;

    private static readonly int ShineProgressID =
        Shader.PropertyToID("_ShineProgress");

    private void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();

        if (shineRenderer == null)
            shineRenderer = GetComponent<Renderer>();
    }

    private void Start()
    {
        StartCoroutine(ShineLoop());
    }

    private IEnumerator ShineLoop()
    {
        while (true)
        {
            yield return StartCoroutine(PlayShine());
            yield return new WaitForSeconds(delayBetweenShines);
        }
    }

    private IEnumerator PlayShine()
    {
        float elapsed = 0f;

        while (elapsed < shineDuration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / shineDuration);

            float progress = Mathf.Lerp(
                startProgress,
                endProgress,
                t
            );

            shineRenderer.GetPropertyBlock(propertyBlock);

            propertyBlock.SetFloat(
                ShineProgressID,
                progress
            );

            shineRenderer.SetPropertyBlock(propertyBlock);

            yield return null;
        }
    }
}
