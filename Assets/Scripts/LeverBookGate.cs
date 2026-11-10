// Keeps the lever book asleep until the painting has fallen: until then it can't be pulled and its spectral glow is hidden.
// A moment after the painting drops the book wakes, so the player has to go back into the stacks to find it.
// Sits next to LeverBook and only turns its grab on and off, so LeverBook itself works exactly as before.

using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(LeverBook), typeof(XRSimpleInteractable))]
public class LeverBookGate : MonoBehaviour
{
    [Tooltip("The painting that has to fall before the book can be pulled. Leave empty and the book is never held back.")]
    [SerializeField] FallingPainting painting;

    [Tooltip("Seconds between the painting coming loose and the book waking.")]
    [SerializeField] float wakeDelay = 3f;

    [Tooltip("The book's spectral glow (halo, dust, aura), hidden until it wakes.")]
    [SerializeField] GameObject[] glow;

    [Tooltip("Optional sound played as the book wakes, such as a whisper from the stacks.")]
    [SerializeField] AudioSource wakeSound;

    public bool Woken { get; private set; }

    XRSimpleInteractable interactable;
    float sinceFall;

    void Awake()
    {
        interactable = GetComponent<XRSimpleInteractable>();
        if (painting == null)
        {
            Woken = true;
            enabled = false;
            return;
        }
        interactable.enabled = false;
        SetGlow(false);
    }

    void Update()
    {
        if (!painting.Fallen) return;
        sinceFall += Time.deltaTime;
        if (sinceFall >= wakeDelay) Wake();
    }

    // Lets the book be pulled and shows its glow, for good
    public void Wake()
    {
        if (Woken) return;
        Woken = true;
        interactable.enabled = true;
        SetGlow(true);
        if (wakeSound != null) wakeSound.Play();
        enabled = false;
    }

    void SetGlow(bool shown)
    {
        if (glow == null) return;
        foreach (GameObject part in glow)
            if (part != null) part.SetActive(shown);
    }
}
