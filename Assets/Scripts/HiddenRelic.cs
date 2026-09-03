using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

/// <summary>
/// Attach to a hidden relic object in the exploration room.
/// When the player looks at it and presses E within range, it triggers collection.
/// </summary>
public class HiddenRelic : MonoBehaviour
{
    [Header("Relic Data")]
    public RelicModifierData relicData;

    [Header("Interaction")]
    public float interactDistance = 2.5f;
    public Camera playerCamera;
    public FirstPersonController playerController;

    [Header("Visuals")]
    [Tooltip("Optional glow/highlight when player is close")]
    public Light relicGlow;
    public float glowRange = 3f;

    [Header("On Collect")]
    [Tooltip("Spawn this object at relicSpawnPoint when collected (the visible reward)")]
    public GameObject rewardPrefab;
    public Transform rewardSpawnPoint;

    [Tooltip("UI panel to show deck choice — assign your DeckChoiceUI here")]
    public DeckChoiceUI deckChoiceUI;

    private bool _collected = false;
    private bool _playerNearby = false;

    void Start()
    {
        // If already collected in a previous run, hide this object
        if (PuzzleManager.Instance != null &&
            PuzzleManager.Instance.HasRelic(relicData.relicID))
        {
            gameObject.SetActive(false);
            SpawnReward(); // reward persists in room
        }

        if (relicGlow != null) relicGlow.intensity = 0f;
    }

    void Update()
    {
        if (_collected) return;

        float dist = playerCamera != null
            ? Vector3.Distance(playerCamera.transform.position, transform.position)
            : float.MaxValue;

        _playerNearby = dist <= glowRange;

        // Subtle glow when nearby
        if (relicGlow != null)
        {
            float targetIntensity = _playerNearby ? 0.8f : 0f;
            relicGlow.intensity = Mathf.Lerp(relicGlow.intensity,
                targetIntensity, Time.deltaTime * 3f);
        }

        if (!_playerNearby) return;

        var kb = Keyboard.current;
        if (kb == null || !kb.eKey.wasPressedThisFrame) return;

        // Raycast check — player must be looking at it
        Ray ray = playerCamera.ScreenPointToRay(
            new Vector2(Screen.width / 2f, Screen.height / 2f));
        if (!Physics.Raycast(ray, out RaycastHit hit, interactDistance)) return;
        if (hit.transform != transform && !hit.transform.IsChildOf(transform)) return;

        StartCoroutine(CollectRelic());
    }

    IEnumerator CollectRelic()
    {
        _collected = true;

        // Disable player movement for the choice UI
        if (playerController != null) playerController.enabled = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Register with PuzzleManager
        PuzzleManager.Instance?.CollectRelic(relicData.relicID);

        // Spawn the reward object in the room
        SpawnReward();

        // Show deck choice UI if this relic grants a deck modifier
        if (deckChoiceUI != null &&
            relicData.deckModifier != RelicModifierData.DeckModifierType.None)
        {
            deckChoiceUI.Show(relicData, OnDeckChosen);
            yield break; // wait for player choice before re-enabling movement
        }

        // No deck choice needed — just re-enable movement
        yield return new WaitForSeconds(0.5f);
        ReenablePlayer();
    }

    void OnDeckChosen()
    {
        ReenablePlayer();
    }

    void ReenablePlayer()
    {
        if (playerController != null) playerController.enabled = true;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        gameObject.SetActive(false);
    }

    void SpawnReward()
    {
        if (rewardPrefab == null || rewardSpawnPoint == null) return;
        Instantiate(rewardPrefab, rewardSpawnPoint.position, rewardSpawnPoint.rotation);
    }
}
