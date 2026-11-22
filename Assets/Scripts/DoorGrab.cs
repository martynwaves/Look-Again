// What a hand takes hold of on a door: a simple interactable whose attach point is wherever it was grabbed, such as the handle,
// so a laser holding it stays there and swings with the door instead of jumping to the hinge.

using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class DoorGrab : XRSimpleInteractable
{
    Transform grip;

    // Moves the attach point to where the door was just grabbed
    public void HoldAt(Vector3 point)
    {
        if (grip == null)
        {
            grip = new GameObject("Grip").transform;
            grip.SetParent(transform, false);
        }
        grip.position = point;
    }

    public override Transform GetAttachTransform(IXRInteractor interactor) => grip != null ? grip : base.GetAttachTransform(interactor);
}
