using UnityEngine;

/// <summary>
/// Small additive camera shake that plays nicely with other camera movement.
/// Put this on the camera (or a camera pivot) you want to shake.
///
/// In LateUpdate it removes last frame's shake offset, then applies a new one,
/// so normal camera movement can continue underneath it.
/// </summary>
public class CameraShakeImpulse : MonoBehaviour
{
    [Header("Shake Shape")]
    [SerializeField] private float positionMultiplier = 1f;
    [SerializeField] private float rotationMultiplier = 1.6f;

    private float remainingTime;
    private float totalDuration;
    private float strength;

    private Vector3 previousPositionOffset;
    private Quaternion previousRotationOffset = Quaternion.identity;

    public void Kick(float newStrength, float duration)
    {
        if (newStrength <= 0f || duration <= 0f)
            return;

        // Repeated impacts can refresh/strengthen the current shake.
        strength = Mathf.Max(strength, newStrength);
        remainingTime = Mathf.Max(remainingTime, duration);
        totalDuration = Mathf.Max(totalDuration, duration);
    }

    private void LateUpdate()
    {
        // Remove the offset we added last frame.
        transform.localPosition -= previousPositionOffset;
        transform.localRotation *= Quaternion.Inverse(previousRotationOffset);

        previousPositionOffset = Vector3.zero;
        previousRotationOffset = Quaternion.identity;

        if (remainingTime <= 0f)
        {
            strength = 0f;
            totalDuration = 0f;
            return;
        }

        remainingTime -= Time.deltaTime;

        float normalized =
            totalDuration <= 0f
                ? 0f
                : Mathf.Clamp01(remainingTime / totalDuration);

        // Strong initial hit, very fast falloff.
        float envelope = normalized * normalized;
        float currentStrength = strength * envelope;

        Vector3 randomDirection = Random.insideUnitSphere;

        previousPositionOffset =
            randomDirection *
            currentStrength *
            positionMultiplier;

        Vector3 rotationEuler =
            Random.insideUnitSphere *
            currentStrength *
            35f *
            rotationMultiplier;

        previousRotationOffset = Quaternion.Euler(rotationEuler);

        transform.localPosition += previousPositionOffset;
        transform.localRotation *= previousRotationOffset;

        if (remainingTime <= 0f)
        {
            strength = 0f;
            totalDuration = 0f;
        }
    }

    private void OnDisable()
    {
        // Clean up any offset if this component/camera gets disabled.
        transform.localPosition -= previousPositionOffset;
        transform.localRotation *= Quaternion.Inverse(previousRotationOffset);

        previousPositionOffset = Vector3.zero;
        previousRotationOffset = Quaternion.identity;
        remainingTime = 0f;
        totalDuration = 0f;
        strength = 0f;
    }
}
