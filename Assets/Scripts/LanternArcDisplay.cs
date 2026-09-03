using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LanternArcDisplay : MonoBehaviour
{
    [Header("Arc Images")]
    [SerializeField] private Image arcFill;         // Radial 180 fill image (bright amber)
    [SerializeField] private Image arcTrack;        // Background track image (dark amber)

    [Header("Label")]
    [SerializeField] private TextMeshPro valueLabel; // Shows percentage while turning

    private float _labelTimer = 0f;
    private const float LABEL_DURATION = 1.2f;

    void Start()
    {
        // Hide label on start
        if (valueLabel != null)
            valueLabel.gameObject.SetActive(false);
    }

    void Update()
    {
        if (_labelTimer > 0f)
        {
            _labelTimer -= Time.deltaTime;
            if (valueLabel != null && _labelTimer <= 0f)
                valueLabel.gameObject.SetActive(false);
        }
    }

    public void SetFill(float normalized)
    {
        // Update arc fill amount
        if (arcFill != null)
            arcFill.fillAmount = normalized;

        // Show percentage label temporarily
        if (valueLabel != null)
        {
            int pct = Mathf.RoundToInt(normalized * 100f);
            valueLabel.text = pct + "%";
            valueLabel.gameObject.SetActive(true);
            _labelTimer = LABEL_DURATION;
        }

        // Warm amber glow — brighter at higher brightness
        if (arcFill != null)
        {
            float glow = Mathf.Lerp(0.4f, 1f, normalized);
            arcFill.color = new Color(1f, 0.75f, 0.2f, glow);
        }
    }
}
