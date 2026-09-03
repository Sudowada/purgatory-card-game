using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Replaces HUDBuilder's runtime wiring.
/// Add this to Canvas after baking the HUD.
/// It finds all named UI elements and passes them to GameManager.
/// 
/// Name your UI elements in the scene to match these names:
/// ScoreVal, RoundVal, EnemyVal, HandsVal, GoldVal
/// PreviewBase, PreviewMult, PreviewFinal, ComboText
/// PreviewTier, PreviewNextHint
/// BreakSrc0, BreakSrc1, BreakSrc2
/// BreakVal0, BreakVal1, BreakVal2
/// PlayButton, ShopPanel
/// </summary>
public class UIReferenceLinker : MonoBehaviour
{
    [Header("Auto-found by name — override here if needed")]
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI roundText;
    public TextMeshProUGUI enemyText;
    public TextMeshProUGUI handsText;
    public TextMeshProUGUI goldText;
    public TextMeshProUGUI comboText;
    public Button          playButton;
    public GameObject      shopPanel;
    public GameObject      gameOverPanel;
    public TextMeshProUGUI gameOverScore;

    // Shop references
    public TextMeshProUGUI shopRelicName, shopRelicDesc, shopRelicCost;
    public TextMeshProUGUI shopCardName, shopCardRarity, shopCardCost;
    public TextMeshProUGUI shopConsumName, shopConsumDesc, shopConsumCost;
    public TextMeshProUGUI shopBurnCost;
    public Button shopBuyRelic, shopBuyCard, shopBuyConsumable, shopBurn, shopLeave;

    void Start()
    {
        // Auto-find any that aren't manually assigned
        scoreText   = scoreText   ?? Find<TextMeshProUGUI>("ScoreVal");
        roundText   = roundText   ?? Find<TextMeshProUGUI>("RoundVal");
        enemyText   = enemyText   ?? Find<TextMeshProUGUI>("EnemyVal");
        handsText   = handsText   ?? Find<TextMeshProUGUI>("HandsVal");
        goldText    = goldText    ?? Find<TextMeshProUGUI>("GoldVal");
        comboText   = comboText   ?? Find<TextMeshProUGUI>("ComboText");
        playButton  = playButton  ?? Find<Button>("PlayButton");
        shopPanel   = shopPanel   ?? GameObject.Find("ShopPanel");
        gameOverPanel = gameOverPanel ?? GameObject.Find("GameOverPanel");
        gameOverScore = gameOverScore ?? Find<TextMeshProUGUI>("Score");

        // Shop
        shopRelicName  = shopRelicName  ?? FindInShop<TextMeshProUGUI>("RelicSlot/Title");
        shopRelicDesc  = shopRelicDesc  ?? FindInShop<TextMeshProUGUI>("RelicSlot/Desc");
        shopRelicCost  = shopRelicCost  ?? FindInShop<TextMeshProUGUI>("RelicSlot/Cost");
        shopBuyRelic   = shopBuyRelic   ?? FindInShop<Button>("RelicSlot/BuyBtn");
        shopCardName   = shopCardName   ?? FindInShop<TextMeshProUGUI>("CardSlot/Title");
        shopCardRarity = shopCardRarity ?? FindInShop<TextMeshProUGUI>("CardSlot/Desc");
        shopCardCost   = shopCardCost   ?? FindInShop<TextMeshProUGUI>("CardSlot/Cost");
        shopBuyCard    = shopBuyCard    ?? FindInShop<Button>("CardSlot/BuyBtn");
        shopConsumName = shopConsumName ?? FindInShop<TextMeshProUGUI>("ConsumSlot/Title");
        shopConsumDesc = shopConsumDesc ?? FindInShop<TextMeshProUGUI>("ConsumSlot/Desc");
        shopConsumCost = shopConsumCost ?? FindInShop<TextMeshProUGUI>("ConsumSlot/Cost");
        shopBuyConsumable = shopBuyConsumable ?? FindInShop<Button>("ConsumSlot/BuyBtn");
        shopBurnCost   = shopBurnCost   ?? FindInShop<TextMeshProUGUI>("BurnSlot/Cost");
        shopBurn       = shopBurn       ?? FindInShop<Button>("BurnSlot/BuyBtn");
        shopLeave      = shopLeave      ?? Find<Button>("LeaveBtn");

        // Wire everything to GameManager
        var gm = GameManager.Instance;
        if (gm == null) { Debug.LogError("[UIReferenceLinker] No GameManager found!"); return; }

        gm.SetUIReferences(
            scoreText, roundText, enemyText, comboText, handsText, goldText,
            playButton, shopPanel,
            shopRelicName, shopRelicDesc, shopRelicCost,
            shopCardName, shopCardRarity, shopCardCost,
            shopConsumName, shopConsumDesc, shopConsumCost,
            shopBurnCost,
            shopBuyRelic, shopBuyCard, shopBuyConsumable, shopBurn, shopLeave,
            gameOverPanel, gameOverScore, null);

        Debug.Log("[UIReferenceLinker] UI references wired to GameManager.");
    }

    T Find<T>(string goName) where T : Component
    {
        var go = GameObject.Find(goName);
        return go?.GetComponent<T>();
    }

    T FindInShop<T>(string path) where T : Component
    {
        if (shopPanel == null) return null;
        var t = shopPanel.transform.Find(path);
        return t?.GetComponent<T>();
    }
}
