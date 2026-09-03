using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Holographic/foil "edition" shader effect — currently built for UI Image
/// components (2D card sprites). Not currently attached to anything in the
/// 3D card setup.
///
/// TODO before reusing this for card/relic rarity effects on the 3D
/// meshes: swap `Image` for `MeshRenderer` (image.material -> 
/// GetComponent<MeshRenderer>().material), since real 3D cards don't have
/// a UI Image component. The keyword/edition logic and the rotation-driven
/// _Rotation vector shader param should still work the same way once
/// that swap is made — this file just needs the component type updated.
/// </summary>
public class ShaderCode : MonoBehaviour
{
    Image image;
    Material m;

    void Start()
    {
        image = GetComponent<Image>();
        m = new Material(image.material);
        image.material = m;

        string[] editions = { "REGULAR", "POLYCHROME", "REGULAR", "NEGATIVE" };
        for (int i = 0; i < image.material.enabledKeywords.Length; i++)
            m.DisableKeyword(image.material.enabledKeywords[i]);
        m.EnableKeyword("_EDITION_" + editions[Random.Range(0, editions.Length)]);
    }

    void Update()
    {
        Quaternion currentRotation = transform.parent.localRotation;
        Vector3 eulerAngles = currentRotation.eulerAngles;
        float xAngle = ClampAngle(eulerAngles.x, -90f, 90f);
        float yAngle = ClampAngle(eulerAngles.y, -90f, 90f);
        m.SetVector("_Rotation", new Vector2(
            ExtensionMethods.Remap(xAngle, -20, 20, -.5f, .5f),
            ExtensionMethods.Remap(yAngle, -20, 20, -.5f, .5f)));
    }

    float ClampAngle(float angle, float min, float max)
    {
        if (angle < -180f) angle += 360f;
        if (angle > 180f) angle -= 360f;
        return Mathf.Clamp(angle, min, max);
    }
}




























