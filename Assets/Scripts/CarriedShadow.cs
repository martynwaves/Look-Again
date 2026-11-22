// Hides an item's shadow while it's carried, in a hand or on the belt, like the player's own tools, which a nearby lantern would otherwise throw about.
// Its shadow comes back once it's let go.

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(XRGrabInteractable))]
public class CarriedShadow : MonoBehaviour
{
    XRGrabInteractable item;
    Renderer[] parts;
    ShadowCastingMode[] shadows;

    void Awake()
    {
        item = GetComponent<XRGrabInteractable>();
        parts = GetComponentsInChildren<Renderer>();
        shadows = System.Array.ConvertAll(parts, part => part.shadowCastingMode);
    }

    void OnEnable()
    {
        item.selectEntered.AddListener(Carried);
        item.selectExited.AddListener(LetGo);
    }

    void OnDisable()
    {
        item.selectEntered.RemoveListener(Carried);
        item.selectExited.RemoveListener(LetGo);
    }

    void Carried(SelectEnterEventArgs args) => Cast(false);

    // Passed from the belt to a hand it's let go of for a moment, so only once nothing holds it
    void LetGo(SelectExitEventArgs args)
    {
        if (!item.isSelected) Cast(true);
    }

    void Cast(bool shadow)
    {
        for (int i = 0; i < parts.Length; i++)
            if (parts[i] != null) parts[i].shadowCastingMode = shadow ? shadows[i] : ShadowCastingMode.Off;
    }
}
