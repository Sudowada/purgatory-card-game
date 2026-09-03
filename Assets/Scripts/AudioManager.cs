using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Procedural audio — generates all sound effects synthetically using AudioSource.
/// No audio files needed. Attach to GameManager GameObject (auto-added).
/// Call AudioManager.Instance.Play("sound_name") from anywhere.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Volume")]
    [Range(0f,1f)] [SerializeField] public float masterVolume = 0.7f;
    [Range(0f,1f)] [SerializeField] public float sfxVolume    = 1.0f;

    // Pool of AudioSources for overlapping sounds
    private List<AudioSource> sourcePool = new List<AudioSource>();
    private const int POOL_SIZE = 12;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(this); return; }

        for (int i = 0; i < POOL_SIZE; i++)
        {
            var src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            sourcePool.Add(src);
        }
    }

    // ── PUBLIC API ────────────────────────────────────────────────────────────

    public void Play(string soundName)
    {
        switch (soundName)
        {
            case "card_draw":       StartCoroutine(SynthCardDraw());     break;
            case "card_play":       StartCoroutine(SynthCardPlay());     break;
            case "card_discard":    StartCoroutine(SynthCardDiscard());  break;
            case "card_select":     StartCoroutine(SynthCardSelect());   break;
            case "card_hover":      StartCoroutine(SynthCardHover());    break;
            case "combo_small":     StartCoroutine(SynthComboSmall());   break;
            case "combo_big":       StartCoroutine(SynthComboBig());     break;
            case "combo_massive":   StartCoroutine(SynthComboMassive()); break;
            case "round_clear":     StartCoroutine(SynthRoundClear());   break;
            case "game_over":       StartCoroutine(SynthGameOver());     break;
            case "ui_click":        StartCoroutine(SynthUIClick());      break;
            case "gold_earn":       StartCoroutine(SynthGoldEarn());     break;
            case "shop_open":       StartCoroutine(SynthShopOpen());     break;
            case "decay_tick":      StartCoroutine(SynthDecayTick());    break;
            case "decay_full":      StartCoroutine(SynthDecayFull());    break;
            default: Debug.LogWarning($"AudioManager: unknown sound '{soundName}'"); break;
        }
    }

    // ── SOUND SYNTHS ─────────────────────────────────────────────────────────

    // Soft papery whoosh — card sliding out of deck
    IEnumerator SynthCardDraw()
    {
        yield return PlayTone(
            startFreq:  800f,  endFreq:   400f,
            duration:   0.12f, volume:    0.18f,
            waveType:   WaveType.Noise,
            envelope:   new Vector3(0.01f, 0.08f, 0.03f));
    }

    // Solid thwack — playing a card
    IEnumerator SynthCardPlay()
    {
        // Low thud
        StartCoroutine(PlayTone(220f, 80f, 0.08f, 0.28f, WaveType.Sine,
            new Vector3(0.005f, 0.04f, 0.035f)));
        yield return new WaitForSeconds(0.02f);
        // High click
        yield return PlayTone(2400f, 1200f, 0.05f, 0.12f, WaveType.Noise,
            new Vector3(0.002f, 0.02f, 0.028f));
    }

    // Light papery flutter — card to discard pile
    IEnumerator SynthCardDiscard()
    {
        yield return PlayTone(
            startFreq:  600f, endFreq:   200f,
            duration:   0.16f, volume:   0.15f,
            waveType:   WaveType.Noise,
            envelope:   new Vector3(0.01f, 0.10f, 0.05f));
    }

    // Brief light click — selecting a card
    IEnumerator SynthCardSelect()
    {
        yield return PlayTone(1800f, 1600f, 0.06f, 0.14f, WaveType.Sine,
            new Vector3(0.003f, 0.04f, 0.017f));
    }

    // Very subtle tick — hovering
    IEnumerator SynthCardHover()
    {
        yield return PlayTone(2200f, 2000f, 0.04f, 0.05f, WaveType.Sine,
            new Vector3(0.002f, 0.02f, 0.018f));
    }

    // Rising arpeggio — small combo
    IEnumerator SynthComboSmall()
    {
        float[] notes = { 440f, 550f, 660f };
        foreach (float n in notes)
        {
            StartCoroutine(PlayTone(n, n*1.05f, 0.12f, 0.22f, WaveType.Sine,
                new Vector3(0.01f, 0.08f, 0.03f)));
            yield return new WaitForSeconds(0.08f);
        }
    }

    // Big fanfare chord — big combo
    IEnumerator SynthComboBig()
    {
        float[] chord = { 330f, 415f, 495f, 660f };
        foreach (float n in chord)
            StartCoroutine(PlayTone(n, n*0.98f, 0.4f, 0.20f, WaveType.Sine,
                new Vector3(0.02f, 0.28f, 0.10f)));
        yield return new WaitForSeconds(0.1f);
        // Impact noise burst
        yield return PlayTone(200f, 60f, 0.08f, 0.35f, WaveType.Noise,
            new Vector3(0.005f, 0.04f, 0.035f));
    }

    // Massive power chord — huge combo
    IEnumerator SynthComboMassive()
    {
        // Low rumble
        StartCoroutine(PlayTone(80f, 40f, 0.6f, 0.4f, WaveType.Sine,
            new Vector3(0.02f, 0.4f, 0.18f)));
        yield return new WaitForSeconds(0.04f);
        // Bright harmonics
        float[] harm = { 220f, 330f, 440f, 550f, 660f, 880f };
        for (int i = 0; i < harm.Length; i++)
        {
            StartCoroutine(PlayTone(harm[i], harm[i]*0.96f, 0.5f,
                0.22f - i*0.02f, WaveType.Sine,
                new Vector3(0.01f, 0.35f, 0.14f)));
            yield return new WaitForSeconds(0.03f);
        }
        // Noise slam
        StartCoroutine(PlayTone(800f, 100f, 0.12f, 0.45f, WaveType.Noise,
            new Vector3(0.005f, 0.06f, 0.055f)));
        yield return null;
    }

    // Ascending victorious phrase — round cleared
    IEnumerator SynthRoundClear()
    {
        float[] melody = { 392f, 523f, 659f, 784f, 1047f };
        foreach (float n in melody)
        {
            StartCoroutine(PlayTone(n, n, 0.2f, 0.25f, WaveType.Sine,
                new Vector3(0.01f, 0.14f, 0.05f)));
            yield return new WaitForSeconds(0.12f);
        }
    }

    // Low descending toll — game over
    IEnumerator SynthGameOver()
    {
        float[] toll = { 220f, 165f, 110f, 82f };
        foreach (float n in toll)
        {
            StartCoroutine(PlayTone(n, n*0.85f, 0.8f, 0.35f, WaveType.Sine,
                new Vector3(0.04f, 0.5f, 0.26f)));
            // dissonant harmonic
            StartCoroutine(PlayTone(n*1.12f, n*0.9f, 0.8f, 0.12f, WaveType.Sine,
                new Vector3(0.04f, 0.5f, 0.26f)));
            yield return new WaitForSeconds(0.6f);
        }
    }

    // Clean click — UI interaction
    IEnumerator SynthUIClick()
    {
        yield return PlayTone(1400f, 1200f, 0.05f, 0.12f, WaveType.Sine,
            new Vector3(0.003f, 0.03f, 0.017f));
    }

    // Coins clinking — earning gold
    IEnumerator SynthGoldEarn()
    {
        float[] pitches = { 1760f, 2093f, 1760f };
        foreach (float p in pitches)
        {
            StartCoroutine(PlayTone(p, p*0.97f, 0.09f, 0.16f, WaveType.Sine,
                new Vector3(0.003f, 0.06f, 0.027f)));
            yield return new WaitForSeconds(0.07f);
        }
    }

    // Mysterious low drone — shop opens
    IEnumerator SynthShopOpen()
    {
        StartCoroutine(PlayTone(110f, 100f, 0.6f, 0.22f, WaveType.Sine,
            new Vector3(0.1f, 0.4f, 0.1f)));
        yield return new WaitForSeconds(0.08f);
        yield return PlayTone(165f, 155f, 0.5f, 0.15f, WaveType.Sine,
            new Vector3(0.08f, 0.32f, 0.1f));
    }

    // Creaky crumble — decay applied
    IEnumerator SynthDecayTick()
    {
        yield return PlayTone(300f, 80f, 0.18f, 0.18f, WaveType.Noise,
            new Vector3(0.005f, 0.12f, 0.063f));
    }

    // Horrible crunch — fully decayed
    IEnumerator SynthDecayFull()
    {
        StartCoroutine(PlayTone(150f, 40f, 0.3f, 0.35f, WaveType.Noise,
            new Vector3(0.005f, 0.2f, 0.095f)));
        yield return new WaitForSeconds(0.05f);
        yield return PlayTone(2000f, 200f, 0.15f, 0.22f, WaveType.Noise,
            new Vector3(0.005f, 0.10f, 0.045f));
    }

    // ── SYNTH ENGINE ──────────────────────────────────────────────────────────

    enum WaveType { Sine, Square, Noise }

    /// <summary>
    /// envelope = (attack, sustain, release) in seconds.
    /// </summary>
    IEnumerator PlayTone(float startFreq, float endFreq, float duration,
        float volume, WaveType waveType, Vector3 envelope)
    {
        AudioSource src = GetFreeSource();
        if (src == null) yield break;

        float attack  = envelope.x;
        float sustain = envelope.y;
        float release = envelope.z;
        float totalDuration = attack + sustain + release;

        int sampleRate = AudioSettings.outputSampleRate;
        int sampleCount = Mathf.CeilToInt(totalDuration * sampleRate);
        float[] samples = new float[sampleCount];

        float phase = 0f;
        for (int i = 0; i < sampleCount; i++)
        {
            float t    = (float)i / sampleRate;
            float tNorm = Mathf.Clamp01(t / totalDuration);

            // Frequency sweep
            float freq = Mathf.Lerp(startFreq, endFreq, tNorm);

            // Waveform
            float sample = 0f;
            switch (waveType)
            {
                case WaveType.Sine:
                    sample = Mathf.Sin(phase * 2f * Mathf.PI);
                    break;
                case WaveType.Square:
                    sample = Mathf.Sin(phase * 2f * Mathf.PI) >= 0f ? 1f : -1f;
                    break;
                case WaveType.Noise:
                    sample = Random.Range(-1f, 1f);
                    // Shape noise with sine carrier for pitched noise
                    sample *= (0.5f + 0.5f * Mathf.Sin(phase * 2f * Mathf.PI));
                    break;
            }

            // Amplitude envelope (ADSR-lite)
            float amp;
            if (t < attack)
                amp = t / attack;
            else if (t < attack + sustain)
                amp = 1f;
            else
                amp = 1f - (t - attack - sustain) / Mathf.Max(release, 0.001f);

            samples[i] = sample * Mathf.Clamp01(amp) * volume * masterVolume * sfxVolume;

            phase += freq / sampleRate;
            if (phase > 1f) phase -= 1f;
        }

        AudioClip clip = AudioClip.Create("synth", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);

        src.clip   = clip;
        src.volume = 1f;
        src.Play();

        yield return new WaitForSeconds(totalDuration);

        if (src.clip == clip)
        {
            src.Stop();
            src.clip = null;
        }
    }

    AudioSource GetFreeSource()
    {
        foreach (var s in sourcePool)
            if (!s.isPlaying) return s;

        // All busy — find the oldest (shortest remaining time)
        AudioSource oldest = sourcePool[0];
        foreach (var s in sourcePool)
            if (s.time > oldest.time) oldest = s;
        oldest.Stop();
        return oldest;
    }
}