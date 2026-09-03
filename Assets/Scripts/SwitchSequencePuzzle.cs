using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Manages the 5-switch sequence puzzle.
/// Attach to an empty GameObject in the scene.
/// Assign the 5 PuzzleSwitch components in the correct order in the Inspector.
/// </summary>
public class SwitchSequencePuzzle : MonoBehaviour
{
    [Header("Puzzle Identity")]
    [Tooltip("Unique key for PlayerPrefs persistence")]
    public string puzzleID = "puzzle_switch_sequence";

    [Header("Switches — assign in correct order")]
    public PuzzleSwitch[] correctSequence = new PuzzleSwitch[4];

    [Header("On Complete")]
    public UnityEvent onPuzzleSolved;

    [Header("Feedback")]
    public AudioSource audioSource;
    public AudioClip correctSwitchClip;
    public AudioClip wrongSwitchClip;
    public AudioClip puzzleSolvedClip;

    [Header("Reset Settings")]
    [Tooltip("Delay in seconds before resetting after a wrong press")]
    public float resetDelay = 0.8f;

    // Tracks which step the player is on
    private int _currentStep = 0;
    private bool _isSolved = false;
    private bool _isResetting = false;

    void Start()
    {
        // Check if already solved in a previous run
        if (PlayerPrefs.GetInt(puzzleID + "_solved", 0) == 1)
        {
            _isSolved = true;
            // Immediately fire solved event to restore room state
            onPuzzleSolved?.Invoke();
            // Lock all switches in their solved state
            foreach (var sw in correctSequence)
                if (sw != null) sw.SetSolvedState();
            return;
        }

        // Register this puzzle with each switch
        foreach (var sw in correctSequence)
            if (sw != null) sw.RegisterPuzzle(this);
    }

    /// <summary>Called by PuzzleSwitch when player interacts with it.</summary>
    public void OnSwitchPressed(PuzzleSwitch pressedSwitch)
    {
        if (_isSolved || _isResetting) return;

        // Check if this is the correct next switch
        if (correctSequence[_currentStep] == pressedSwitch)
        {
            // Correct!
            pressedSwitch.SetActivated(true);
            PlaySound(correctSwitchClip);
            _currentStep++;

            if (_currentStep >= correctSequence.Length)
                StartCoroutine(SolvePuzzle());
        }
        else
        {
            // Wrong switch — reset
            PlaySound(wrongSwitchClip);
            StartCoroutine(ResetSequence());
        }
    }

    IEnumerator SolvePuzzle()
    {
        _isSolved = true;

        // Save permanently
        PlayerPrefs.SetInt(puzzleID + "_solved", 1);
        PlayerPrefs.Save();
        PuzzleManager.Instance?.SetFlag(puzzleID + "_solved", true);

        yield return new WaitForSeconds(0.5f);
        PlaySound(puzzleSolvedClip);
        yield return new WaitForSeconds(0.6f);

        onPuzzleSolved?.Invoke();
        Debug.Log("[SwitchSequencePuzzle] Puzzle solved!");
    }

    IEnumerator ResetSequence()
    {
        _isResetting = true;

        yield return new WaitForSeconds(resetDelay);

        // Deactivate all switches
        foreach (var sw in correctSequence)
            if (sw != null) sw.SetActivated(false);

        _currentStep = 0;
        _isResetting = false;

        Debug.Log("[SwitchSequencePuzzle] Wrong order — resetting.");
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
        _isSolved = false;
        _currentStep = 0;
        foreach (var sw in correctSequence)
            if (sw != null) sw.SetActivated(false);
        Debug.Log("[SwitchSequencePuzzle] Progress reset.");
    }
}
