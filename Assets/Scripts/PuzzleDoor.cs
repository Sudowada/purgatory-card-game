using UnityEngine;
using System.Collections;

/// <summary>
/// Attach to the door or compartment that unlocks when the switch puzzle is solved.
/// Hook OnPuzzleSolved() into SwitchSequencePuzzle's onPuzzleSolved UnityEvent.
/// </summary>
public class PuzzleDoor : MonoBehaviour
{
    [Header("Open Animation")]
    public Transform doorTransform;
    public Vector3 closedPosition;
    public Vector3 openPosition;
    public Vector3 closedRotation;
    public Vector3 openRotation;
    public float openDuration = 1.2f;
    public AnimationCurve openCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip   openClip;
    public AudioClip   unlockClip;

    [Header("Reward")]
    [Tooltip("Object to reveal/enable when door opens (relic, item, etc.)")]
    public GameObject hiddenReward;

    private bool _isOpen = false;

    void Start()
    {
        // Set initial closed state
        if (doorTransform != null)
        {
            doorTransform.localPosition = closedPosition;
            doorTransform.localEulerAngles = closedRotation;
        }

        // Hide reward until solved
        if (hiddenReward != null)
            hiddenReward.SetActive(false);
    }

    /// <summary>
    /// Called by SwitchSequencePuzzle's onPuzzleSolved UnityEvent.
    /// Also called on Start if puzzle was already solved in a previous run.
    /// </summary>
    public void OnPuzzleSolved()
    {
        if (_isOpen) return;
        StartCoroutine(OpenDoor());
    }

    IEnumerator OpenDoor()
    {
        _isOpen = true;

        // Play unlock sound first
        if (audioSource != null && unlockClip != null)
            audioSource.PlayOneShot(unlockClip);

        yield return new WaitForSeconds(0.4f);

        // Play open sound
        if (audioSource != null && openClip != null)
            audioSource.PlayOneShot(openClip);

        // Animate door open
        if (doorTransform != null)
        {
            float elapsed = 0f;
            Vector3    startPos = doorTransform.localPosition;
            Quaternion startRot = doorTransform.localRotation;
            Quaternion targetRot = Quaternion.Euler(openRotation);

            while (elapsed < openDuration)
            {
                elapsed += Time.deltaTime;
                float t = openCurve.Evaluate(Mathf.Clamp01(elapsed / openDuration));
                doorTransform.localPosition = Vector3.Lerp(startPos, openPosition, t);
                doorTransform.localRotation = Quaternion.Slerp(startRot, targetRot, t);
                yield return null;
            }

            doorTransform.localPosition    = openPosition;
            doorTransform.localEulerAngles = openRotation;
        }

        // Reveal the reward
        if (hiddenReward != null)
        {
            yield return new WaitForSeconds(0.3f);
            hiddenReward.SetActive(true);
        }

        Debug.Log("[PuzzleDoor] Door opened and reward revealed.");
    }
}
