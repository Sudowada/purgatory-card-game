using UnityEngine;

[RequireComponent(typeof(FirstPersonController))]
public sealed class LightPuzzlePlaytestControls : MonoBehaviour
{
    private void Start()
    {
        ScreenReticle.EnsureExists();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}
