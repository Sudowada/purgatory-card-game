using UnityEngine;
using UnityEngine.Events;
using System.Collections;

/// <summary>
/// Tracks a color sequence puzzle across all 4 switches.
/// Each step checks which switch was flipped AND what state it landed on.
/// Attach to an empty GameObject in the scene.
/// </summary>
public class ColorSequencePuzzle : MonoBehaviour
{
    [Header("Puzzle Identity")]
    public string puzzleID = "puzzle_color_sequence";

    [System.Serializable]
    public class SequenceStep
    {
        [Tooltip("The switch that must be flipped for this step")]
        public PuzzleSwitch targetSwitch;

        [Tooltip("The state the switch must land on. Lever: true=up/on, false=down/off. Binary: true=up, false=down")]
        public bool requiredState = true;

        [Tooltip("Optional label for debugging e.g. 'Switch 1 UP (red)'")]
        public string stepLabel = "";
    }

    [Header("Sequence — set steps in order")]
    public SequenceStep[] sequence = new SequenceStep[4];

    [Header("On Complete")]
    public UnityEvent onPuzzleSolved;

    [Header("Feedback")]
    public AudioSource audioSource;
    public AudioClip correctClip;
    public AudioClip wrongClip;
    public AudioClip solvedClip;

    [Header("Reset Settings")]
    public float resetDelay = 0.6f;

    private int  _currentStep = 0;
    private bool _isSolved    = false;
    private bool _isResetting = false;

    void Start()
    {
        if (PlayerPrefs.GetInt(puzzleID + "_solved", 0) == 1)
        {
            _isSolved = true;
            onPuzzleSolved?.Invoke();
            return;
        }

        // Register this puzzle with all switches in the sequence
        foreach (var step in sequence)
            if (step.targetSwitch != null)
                step.targetSwitch.RegisterColorPuzzle(this);
    }

    /// <summary>
    /// Called by PuzzleSwitch whenever any switch state changes.
    /// </summary>
    public void OnSwitchStateChanged(PuzzleSwitch changedSwitch, bool newState)
    {
        if (_isSolved || _isResetting) return;

        SequenceStep expected = sequence[_currentStep];

        bool correctSwitch = changedSwitch == expected.targetSwitch;
        bool correctState  = newState == expected.requiredState;

        if (correctSwitch && correctState)
        {
            // Correct!
            PlaySound(correctClip);
            Debug.Log($"[ColorSequencePuzzle] Step {_currentStep + 1} correct: {expected.stepLabel}");
            _currentStep++;

            if (_currentStep >= sequence.Length)
                StartCoroutine(SolvePuzzle());
        }
        else if (correctSwitch && !correctState)
        {
            // Right switch, wrong direction
            PlaySound(wrongClip);
            StartCoroutine(ResetSequence());
        }
        // Wrong switch entirely is ignored — player is just fiddling
    }

    IEnumerator SolvePuzzle()
    {
        _isSolved = true;
        PlayerPrefs.SetInt(puzzleID + "_solved", 1);
        PlayerPrefs.Save();
        PuzzleManager.Instance?.SetFlag(puzzleID + "_solved", true);

        yield return new WaitForSeconds(0.5f);
        PlaySound(solvedClip);
        yield return new WaitForSeconds(0.6f);

        onPuzzleSolved?.Invoke();
        Debug.Log("[ColorSequencePuzzle] Puzzle solved!");
    }

    IEnumerator ResetSequence()
    {
        _isResetting = true;
        yield return new WaitForSeconds(resetDelay);
        _currentStep = 0;
        _isResetting = false;
        Debug.Log("[ColorSequencePuzzle] Wrong — sequence reset.");
    }

    void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }

    [ContextMenu("Reset Puzzle Progress")]
    public void ResetProgress()
    {
        PlayerPrefs.DeleteKey(puzzleID + "_solved");
        _isSolved    = false;
        _currentStep = 0;
        Debug.Log("[ColorSequencePuzzle] Progress reset.");
    }
}
