using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>A non-interactive price slip. Click the relic to purchase it.</summary>
public class RelicPaperCard : MonoBehaviour
{
    public Vector2 paperSize = new Vector2(100f, 35f);
    public float worldScale = 0.001f;
    public Color paperColor = new Color(0.85f, 0.80f, 0.65f);
    private TextMeshProUGUI price;
    private CanvasGroup group;
    private Vector3 hiddenPosition, visiblePosition;
    private int cost;
    private bool purchased;
    public void Initialize(Relic relic, int cost, bool purchased, int index,
        Vector3 hiddenLocalPos, Vector3 visibleLocalPos, System.Action<int> onBuyCallback)
    {
        this.cost = cost;
        this.purchased = purchased;
        hiddenPosition = hiddenLocalPos;
        visiblePosition = visibleLocalPos;
        BuildPaper();
        transform.localPosition = hiddenPosition;
        group.alpha = 0f;
        RefreshGold();
    }
    void BuildPaper()
    {
        if (price != null) return;
        // This generated child also works when the supplied prefab has no Canvas.
        var paper = new GameObject("PriceOnly", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup), typeof(Image));
        paper.transform.SetParent(transform, false);
        paper.transform.localScale = Vector3.one * worldScale;
        paper.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        paper.GetComponent<RectTransform>().sizeDelta = paperSize;
        group = paper.GetComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;
        Image image = paper.GetComponent<Image>();
        image.color = paperColor;
        image.raycastTarget = false;
        var label = new GameObject("Price", typeof(RectTransform), typeof(TextMeshProUGUI));
        label.transform.SetParent(paper.transform, false);
        price = label.GetComponent<TextMeshProUGUI>();
        price.fontSize = 20f;
        price.fontStyle = FontStyles.Bold;
        price.color = new Color(0.12f, 0.10f, 0.08f);
        price.alignment = TextAlignmentOptions.Center;
        price.raycastTarget = false;
        price.rectTransform.anchorMin = Vector2.zero;
        price.rectTransform.anchorMax = Vector2.one;
        price.rectTransform.offsetMin = Vector2.zero;
        price.rectTransform.offsetMax = Vector2.zero;
        // Retire old full-card UI if this component is on an existing paper prefab.
        foreach (Canvas canvas in GetComponentsInChildren<Canvas>(true))
            if (canvas.gameObject != paper) canvas.gameObject.SetActive(false);
        foreach (Collider collider in GetComponentsInChildren<Collider>(true)) collider.enabled = false;
    }
    public IEnumerator SlideOut(float duration = 0.45f) { yield return Slide(visiblePosition, 1f, duration); }
    public IEnumerator SlideIn(float duration = 0.28f) { yield return Slide(hiddenPosition, 0f, duration); }
    IEnumerator Slide(Vector3 target, float alpha, float duration)
    {
        Vector3 start = transform.localPosition;
        float startAlpha = group != null ? group.alpha : 0f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.001f, duration));
            t = 1f - Mathf.Pow(1f - t, 3f);
            transform.localPosition = Vector3.Lerp(start, target, t);
            if (group != null) group.alpha = Mathf.Lerp(startAlpha, alpha, t);
            yield return null;
        }
        transform.localPosition = target;
        if (group != null) group.alpha = alpha;
    }
    public void MarkPurchased() { purchased = true; RefreshGold(); }
    public void RefreshGold() { if (price != null) price.text = purchased ? "SOLD" : cost + "g"; }
}
