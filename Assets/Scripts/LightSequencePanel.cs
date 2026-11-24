using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class LightSequencePanel : MonoBehaviour
{
    [Serializable]
    public sealed class Rocker
    {
        public Transform paddle;
        public Collider target;
        public Transform axisReference;
        [NonSerialized] private Vector3 restPosition;
        [NonSerialized] private Quaternion restRotation;
        [NonSerialized] private Vector3 hingeOffset;
        [NonSerialized] private Vector3 axis;

        public void Initialize()
        {
            if (paddle == null) return;
            restPosition = paddle.localPosition;
            restRotation = paddle.localRotation;
            var mesh = paddle.GetComponent<MeshFilter>();
            var pivot = mesh != null && mesh.sharedMesh != null
                ? paddle.TransformPoint(mesh.sharedMesh.bounds.center)
                : paddle.position;
            var direction = axisReference != null ? axisReference.right : paddle.right;
            if (paddle.parent != null)
            {
                pivot = paddle.parent.InverseTransformPoint(pivot);
                direction = paddle.parent.InverseTransformDirection(direction);
            }
            hingeOffset = restPosition - pivot;
            axis = direction.normalized;
        }

        public void Pose(float angle)
        {
            if (paddle == null) return;
            var turn = Quaternion.AngleAxis(angle, axis);
            paddle.localRotation = turn * restRotation;
            paddle.localPosition = restPosition + turn * hingeOffset - hingeOffset;
        }
    }

    [Serializable]
    public sealed class Fixture
    {
        public Rocker power = new Rocker();
        public Rocker selector = new Rocker();
        public Light roomLight;
        public Color primaryColor = Color.white;
        public Color secondaryColor = Color.white;
        [Min(0.01f)] public float intensity = 1.5f;
        [NonSerialized] public bool powered;
        [NonSerialized] public bool secondary;

        public void Refresh()
        {
            if (roomLight == null) return;
            roomLight.color = secondary ? secondaryColor : primaryColor;
            roomLight.intensity = intensity;
            roomLight.enabled = powered;
        }
    }

    [Serializable]
    public struct Step
    {
        [Tooltip("Fixture number, starting at 1.")]
        public int fixtureNumber;
        public bool secondary;
    }

    public Camera playerCamera;
    public FirstPersonController playerController;
    public float interactionDistance = 2.5f;
    public float angle = 9f;
    public Fixture[] fixtures = Array.Empty<Fixture>();
    public Step[] sequence = Array.Empty<Step>();
    public UnityEvent onWrongInput;
    public UnityEvent onSequenceSolved;

    [Header("Sequence Feedback")]
    public AudioSource feedbackSource;
    public AudioClip powerRockerClick;
    public AudioClip colorSelectorClick;
    public AudioClip correctStepClip;
    public AudioClip wrongInputClip;
    public AudioClip solvedClip;

    public int Progress { get; private set; }
    public bool IsSolved { get; private set; }

    private void Start()
    {
        ScreenReticle.EnsureExists();
        foreach (var fixture in fixtures)
        {
            fixture.power.Initialize();
            fixture.selector.Initialize();
            fixture.power.Pose(angle);
            fixture.selector.Pose(angle);
            fixture.Refresh();
        }
    }

    private void LateUpdate()
    {
        if (playerCamera == null || !FirstPersonController.AllowsWorldPuzzleInput(playerController)) return;
        var ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f));
        if (!Physics.Raycast(ray, out var hit, interactionDistance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return;

        for (int i = 0; i < fixtures.Length; i++)
        {
            var fixture = fixtures[i];
            bool power = hit.collider == fixture.power.target;
            if (!power && hit.collider != fixture.selector.target) continue;

            ScreenReticle.Instance?.ShowInteractionHint();
            if (Mouse.current?.leftButton.wasPressedThisFrame != true) return;

            if (power)
            {
                PlayClip(powerRockerClick);
                fixture.powered = !fixture.powered;
                fixture.power.Pose(fixture.powered ? -angle : angle);
                fixture.Refresh();
                if (fixture.powered) SubmitActivation(i + 1, fixture.secondary);
            }
            else
            {
                PlayClip(colorSelectorClick);
                fixture.secondary = !fixture.secondary;
                fixture.selector.Pose(fixture.secondary ? -angle : angle);
                fixture.Refresh();
            }
            return;
        }
    }

    private void PlayFeedback(AudioClip clip, Color tint, float duration)
    {
        ScreenReticle.Instance?.Pulse(tint, duration);
        PlayClip(clip);
    }

    private void PlayClip(AudioClip clip)
    {
        if (clip != null && feedbackSource != null) feedbackSource.PlayOneShot(clip);
    }

    private void SubmitActivation(int fixtureNumber, bool secondary)
    {
        if (IsSolved || sequence.Length == 0) return;
        var expected = sequence[Progress];
        if (expected.fixtureNumber != fixtureNumber || expected.secondary != secondary)
        {
            Progress = 0;
            PlayFeedback(wrongInputClip, new Color(1f, 0.47f, 0.38f), 0.24f);
            onWrongInput?.Invoke();
            Debug.Log("[LightSequencePanel] Wrong light activation. Sequence reset.", this);
            return;
        }

        Progress++;
        Debug.Log($"[LightSequencePanel] {Progress}/{sequence.Length} correct.", this);
        if (Progress != sequence.Length)
        {
            PlayFeedback(correctStepClip, new Color(0.77f, 0.90f, 0.73f), 0.18f);
            return;
        }

        IsSolved = true;
        PlayFeedback(solvedClip, new Color(1f, 0.83f, 0.48f), 0.55f);
        Debug.Log("[LightSequencePanel] Sequence solved.", this);
        onSequenceSolved?.Invoke();
    }
}
