using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Attach to the same GameObject as PuzzleSwitch on the two left lever switches.
/// Call StartMorse() when lever reaches middle, StopMorse() when it leaves.
/// </summary>
public class MorseFlicker : MonoBehaviour
{
    [Header("Morse Message")]
    [Tooltip("The morse code string. Use . for dot, - for dash, / for word gap, space for letter gap")]
    [TextArea]
    public string morseCode = ".. / .- -- / ... - .. .-.. .-.. / .... . .-. .";

    [Tooltip("Name shown in debug — e.g. LIRA or URIEL")]
    public string senderName = "LIRA";

    [Header("Light")]
    public Light morseLight;
    public Color morseColor     = new Color(1f, 0.6f, 0.1f);
    public float onIntensity    = 2.5f;
    public float offIntensity   = 0f;

    [Header("Timing (seconds)")]
    public float dotDuration    = 0.15f;   // length of a dot flash
    public float dashDuration   = 0.45f;   // length of a dash flash
    public float symbolGap      = 0.1f;    // gap between dots/dashes in same letter
    public float letterGap      = 0.25f;   // gap between letters
    public float wordGap        = 0.55f;   // gap between words
    public float endPause       = 1.8f;    // pause after BZZT before looping
    public float loopPause      = 0.6f;    // pause before starting again

    [Header("Audio")]
    public AudioSource audioSource;
    [Tooltip("Short high beep for dots")]
    public AudioClip dotClip;
    [Tooltip("Long low beep for dashes")]
    public AudioClip dashClip;
    [Tooltip("Electric BZZT at end of message")]
    public AudioClip bzztClip;

    [Range(0f, 1f)] public float dotVolume  = 0.5f;
    [Range(0f, 1f)] public float dashVolume = 0.5f;
    [Range(0f, 1f)] public float bzztVolume = 0.8f;

    private Coroutine _morseCoroutine;
    private bool _running = false;

    // ── Public Interface ──────────────────────────────────────────────

    public void StartMorse()
    {
        if (_running) return;
        _running = true;
        _morseCoroutine = StartCoroutine(MorseLoop());
        Debug.Log($"[MorseFlicker] Starting morse from {senderName}");
    }

    public void StopMorse()
    {
        _running = false;
        if (_morseCoroutine != null)
        {
            StopCoroutine(_morseCoroutine);
            _morseCoroutine = null;
        }
        SetLight(false);
    }

    // ── Morse Loop ────────────────────────────────────────────────────

    IEnumerator MorseLoop()
    {
        while (_running)
        {
            yield return StartCoroutine(PlayMessage());

            if (!_running) yield break;

            // BZZT end-of-message signal
            PlaySound(bzztClip, bzztVolume);

            // Rapid stutter flash for the BZZT
            for (int i = 0; i < 3; i++)
            {
                SetLight(true);
                yield return new WaitForSeconds(0.05f);
                SetLight(false);
                yield return new WaitForSeconds(0.04f);
            }

            yield return new WaitForSeconds(endPause);

            if (!_running) yield break;
            yield return new WaitForSeconds(loopPause);
        }
    }

    IEnumerator PlayMessage()
    {
        // Parse morse string symbol by symbol
        string morse = morseCode.Trim();
        int i = 0;

        while (i < morse.Length && _running)
        {
            char c = morse[i];

            if (c == '.')
            {
                // Dot
                SetLight(true);
                PlaySound(dotClip, dotVolume);
                yield return new WaitForSeconds(dotDuration);
                SetLight(false);
                yield return new WaitForSeconds(symbolGap);
            }
            else if (c == '-')
            {
                // Dash
                SetLight(true);
                PlaySound(dashClip, dashVolume);
                yield return new WaitForSeconds(dashDuration);
                SetLight(false);
                yield return new WaitForSeconds(symbolGap);
            }
            else if (c == ' ')
            {
                // Letter gap (space between letters in same word)
                // Check if next char is '/' for word gap
                if (i + 1 < morse.Length && morse[i + 1] == '/')
                {
                    // word gap handled by '/'
                }
                else
                {
                    yield return new WaitForSeconds(letterGap);
                }
            }
            else if (c == '/')
            {
                // Word gap
                yield return new WaitForSeconds(wordGap);
                // Skip the spaces around the slash
                if (i + 1 < morse.Length && morse[i + 1] == ' ')
                    i++;
            }

            i++;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────

    void SetLight(bool on)
    {
        if (morseLight == null) return;
        morseLight.color     = morseColor;
        morseLight.intensity = on ? onIntensity : offIntensity;
    }

    void PlaySound(AudioClip clip, float volume)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip, volume);
    }

    void OnDisable()
    {
        StopMorse();
    }
}
