using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;

public class DeckReturnInteractable : MonoBehaviour
{
    [Header("Interaction")]
    public float interactDistance = 3f;

    [Header("Highlight")]
    public Color highlightColor     = new Color(1f, 0.85f, 0.4f);
    [Range(0f, 2f)] public float highlightIntensity = 0.5f;
    public float fadeSpeed = 8f;

    [Header("Prompt UI (auto-built if empty)")]
    public GameObject promptObject;

    private Camera     _cam;
    private Renderer[] _renderers;
    private Material[] _materials;
    private Color[]    _originalEmissions;
    private float      _currentIntensity = 0f;
    private float      _targetIntensity  = 0f;
    private bool       _isLooking        = false;
    private GameObject _builtPrompt;

    void Awake()
    {
        _cam = Camera.main;
        _renderers = GetComponentsInChildren<Renderer>();
        var matList = new System.Collections.Generic.List<Material>();
        foreach (var r in _renderers)
            foreach (var m in r.materials)
                matList.Add(m);
        _materials         = matList.ToArray();
        _originalEmissions = new Color[_materials.Length];
        for (int i = 0; i < _materials.Length; i++)
        {
            _materials[i].EnableKeyword("_EMISSION");
            _originalEmissions[i] = _materials[i].GetColor("_EmissionColor");
        }
    }

    void Start()
    {
        if (promptObject == null) BuildPromptUI();
        SetPromptVisible(false);
    }

    void Update()
    {
        if (GameStateManager.Instance == null) return;
        if (GameStateManager.Instance.CurrentState != GameState.Exploration) return;

        CheckLook();
        UpdateHighlight();

        var kb = Keyboard.current;
        if (_isLooking && kb != null && kb.eKey.wasPressedThisFrame)
            ReturnToCardGame();
    }

    void CheckLook()
    {
        if (_cam == null) { _cam = Camera.main; return; }
        Ray ray = _cam.ScreenPointToRay(new Vector2(Screen.width / 2f, Screen.height / 2f));
        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance))
        {
            bool hitting = hit.transform == transform || hit.transform.IsChildOf(transform);
            if (hitting != _isLooking)
            {
                _isLooking       = hitting;
                _targetIntensity = _isLooking ? highlightIntensity : 0f;
                SetPromptVisible(_isLooking);
            }
        }
        else if (_isLooking)
        {
            _isLooking = false; _targetIntensity = 0f; SetPromptVisible(false);
        }
    }

    void UpdateHighlight()
    {
        _currentIntensity = Mathf.Lerp(_currentIntensity, _targetIntensity, Time.deltaTime * fadeSpeed);
        for (int i = 0; i < _materials.Length; i++)
            _materials[i].SetColor("_EmissionColor", _originalEmissions[i] + highlightColor * _currentIntensity);
    }

    void ReturnToCardGame()
    {
        SetPromptVisible(false);
        _isLooking = false; _targetIntensity = 0f;
        GameManager.Instance?.OnDeckInteracted();
        GameStateManager.Instance?.GoToCardGame();
    }

    void SetPromptVisible(bool visible)
    {
        if (promptObject  != null) promptObject.SetActive(visible);
        if (_builtPrompt  != null) _builtPrompt.SetActive(visible);
    }

    void BuildPromptUI()
    {
        var canvasGO = new GameObject("DeckPromptCanvas");
        var canvas   = canvasGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 55;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>().enabled = false;

        _builtPrompt = new GameObject("DeckPrompt");
        _builtPrompt.transform.SetParent(canvasGO.transform, false);
        var rt = _builtPrompt.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f); rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 60f);
        rt.sizeDelta = new Vector2(340f, 48f);
        _builtPrompt.AddComponent<Image>().color = new Color(0.04f, 0.03f, 0.02f, 0.88f);

        var textGO = new GameObject("Label");
        textGO.transform.SetParent(_builtPrompt.transform, false);
        var trt = textGO.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;
        var tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text      = "[E]  Return to the Table";
        tmp.fontSize  = 18f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color     = new Color(0.82f, 0.73f, 0.52f);
        tmp.fontStyle = FontStyles.Bold;
    }
}