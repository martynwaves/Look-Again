// Tells belt slots how to turn an item that keeps its shape when stowed, such as the camera turned so its lens faces out from the body.

using UnityEngine;

public class StowedFacing : MonoBehaviour, IStowedShape
{
    [Tooltip("How the item is turned in a belt slot, in degrees, relative to the slot, which faces in toward the body.")]
    [SerializeField] Vector3 turn = new(0f, 180f, 0f);

    [Tooltip("The item's middle, in its own space; a slot holds it there.")]
    [SerializeField] Vector3 middle;

    public Vector3 StowedMiddle => middle;
    public Quaternion StowedTurn => Quaternion.Euler(turn);
    public bool ShowsStowed(Transform part) => part.TryGetComponent(out Renderer look) && look.enabled;
    public Matrix4x4 StowedPose(Transform part) => transform.worldToLocalMatrix * part.localToWorldMatrix;
}
