using UnityEngine;

[DefaultExecutionOrder(-10)]
[DisallowMultipleComponent]
public sealed class TwoRockerLightCircuit : MonoBehaviour
{
    public PuzzleSwitch colorRocker;
    public PuzzleSwitch powerRocker;
    public Light roomLight;

    private void LateUpdate()
    {
        if (roomLight == null || colorRocker == null || powerRocker == null) return;

        int colorState = colorRocker.GetBinaryState();
        roomLight.enabled = powerRocker.IsOn && colorState != 0;
        roomLight.color = colorState > 0 ? colorRocker.binaryUpColor : colorRocker.binaryDownColor;
    }

    private void OnDisable()
    {
        if (roomLight != null) roomLight.enabled = false;
    }
}
