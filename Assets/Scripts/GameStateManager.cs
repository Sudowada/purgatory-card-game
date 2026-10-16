using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public enum GameState
{
    Exploration,
    CardGame
}

public class GameStateManager : MonoBehaviour
{
    public static GameStateManager Instance { get; private set; }

    public GameState CurrentState { get; private set; }

    public event System.Action<GameState> OnStateChanged;

    [Header("Startup")]
    [SerializeField] private GameState startingState = GameState.Exploration;

    [Header("Player")]
    [SerializeField] private FirstPersonController playerController;
    [Tooltip("Renderers on the player's own body (e.g. the capsule mesh) that should be hidden while zoomed into card mode, so the player doesn't see themselves clipping into the camera view. Does NOT disable the GameObject, camera, or controller — just visibility.")]
    [SerializeField] private Renderer[] playerRenderers;

    [Header("Camera")]
    [SerializeField] private Transform playerCamera;
    [SerializeField] private Transform cardTableViewPoint;
    [SerializeField] private float cameraMoveSpeed = 8f;
    [SerializeField] private float cameraRotateSpeed = 8f;

    [Header("3D Card Game Root")]
    [SerializeField] private GameObject cardTable3DRoot;

    [Header("Debug")]
    [Tooltip("Press this key in Play mode to toggle between Exploration and CardGame instantly. Useful for positioning cardTableViewPoint without walking to the deck each time.")]
    [SerializeField] private KeyCode debugToggleKey = KeyCode.C;
    [SerializeField] private bool enableDebugToggle = true;

    private Vector3 explorationCameraLocalPosition;
    private Quaternion explorationCameraLocalRotation;

    private Coroutine cameraMoveRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        if (playerCamera != null)
        {
            explorationCameraLocalPosition = playerCamera.localPosition;
            explorationCameraLocalRotation = playerCamera.localRotation;
        }

        if (playerController == null)
            Debug.LogWarning("[GameStateManager] Player Controller is not assigned in the Inspector.");
        if (playerCamera == null)
            Debug.LogWarning("[GameStateManager] Player Camera is not assigned in the Inspector.");
        if (cardTableViewPoint == null)
            Debug.LogWarning("[GameStateManager] Card Table View Point is not assigned — GoToCardGame() will not move the camera.");

        ApplyState(startingState, true);
    }

    private void Update()
    {
        if (!enableDebugToggle) return;

        var kb = Keyboard.current;
        if (kb == null) return;

        Key mappedKey = MapKeyCodeToInputSystemKey(debugToggleKey);
        if (kb[mappedKey].wasPressedThisFrame)
        {
            if (CurrentState == GameState.Exploration)
                GoToCardGame();
            else
                GoToExploration();
        }
    }

    private static Key MapKeyCodeToInputSystemKey(KeyCode keyCode)
    {
        // Covers the common single-letter/digit debug keys. Extend if you
        // pick a different debugToggleKey that isn't a plain letter.
        string name = keyCode.ToString();
        if (System.Enum.TryParse<Key>(name, out var parsed))
            return parsed;
        return Key.C;
    }

    public void GoToExploration()
    {
        ApplyState(GameState.Exploration, false);
    }

    public void GoToCardGame()
    {
        ApplyState(GameState.CardGame, false);
    }

    private void ApplyState(GameState newState, bool instant)
    {
        CurrentState = newState;

        bool isCardGame = newState == GameState.CardGame;

        if (playerController != null)
            playerController.enabled = !isCardGame;

        SetPlayerVisible(!isCardGame);

        if (cardTable3DRoot != null)
            cardTable3DRoot.SetActive(true);

        Cursor.lockState = isCardGame ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = isCardGame;

        if (cameraMoveRoutine != null)
            StopCoroutine(cameraMoveRoutine);

        if (isCardGame)
        {
            if (playerCamera != null && cardTableViewPoint != null)
            {
                cameraMoveRoutine = StartCoroutine(MoveCameraToWorldPoint(
                    cardTableViewPoint.position,
                    cardTableViewPoint.rotation,
                    instant
                ));
            }
        }
        else
        {
            if (playerCamera != null)
            {
                cameraMoveRoutine = StartCoroutine(MoveCameraToLocalPoint(
                    explorationCameraLocalPosition,
                    explorationCameraLocalRotation,
                    instant
                ));
            }
        }

        OnStateChanged?.Invoke(newState);
        Debug.Log($"[GameStateManager] State -> {newState}");
    }

    private void SetPlayerVisible(bool visible)
    {
        if (playerRenderers == null) return;
        foreach (var r in playerRenderers)
        {
            if (r != null) r.enabled = visible;
        }
    }

    // Shop camera transitions also need to hide the player body while the
    // normal game state remains unchanged.
    public void SetPlayerModelVisible(bool visible) => SetPlayerVisible(visible);

    private IEnumerator MoveCameraToWorldPoint(Vector3 targetPosition, Quaternion targetRotation, bool instant)
    {
        if (instant)
        {
            playerCamera.position = targetPosition;
            playerCamera.rotation = targetRotation;
            yield break;
        }

        while (Vector3.Distance(playerCamera.position, targetPosition) > 0.01f ||
               Quaternion.Angle(playerCamera.rotation, targetRotation) > 0.5f)
        {
            playerCamera.position = Vector3.Lerp(
                playerCamera.position,
                targetPosition,
                Time.deltaTime * cameraMoveSpeed
            );

            playerCamera.rotation = Quaternion.Slerp(
                playerCamera.rotation,
                targetRotation,
                Time.deltaTime * cameraRotateSpeed
            );

            yield return null;
        }

        playerCamera.position = targetPosition;
        playerCamera.rotation = targetRotation;
    }

    private IEnumerator MoveCameraToLocalPoint(Vector3 targetLocalPosition, Quaternion targetLocalRotation, bool instant)
    {
        if (instant)
        {
            playerCamera.localPosition = targetLocalPosition;
            playerCamera.localRotation = targetLocalRotation;
            yield break;
        }

        while (Vector3.Distance(playerCamera.localPosition, targetLocalPosition) > 0.01f ||
               Quaternion.Angle(playerCamera.localRotation, targetLocalRotation) > 0.5f)
        {
            playerCamera.localPosition = Vector3.Lerp(
                playerCamera.localPosition,
                targetLocalPosition,
                Time.deltaTime * cameraMoveSpeed
            );

            playerCamera.localRotation = Quaternion.Slerp(
                playerCamera.localRotation,
                targetLocalRotation,
                Time.deltaTime * cameraRotateSpeed
            );

            yield return null;
        }

        playerCamera.localPosition = targetLocalPosition;
        playerCamera.localRotation = targetLocalRotation;
    }
}
