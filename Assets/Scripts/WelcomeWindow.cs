// The welcome window: closing it hides it and completes the first objective.

using UnityEngine;

public class WelcomeWindow : MonoBehaviour
{
    [Tooltip("The panel to hide when closed. Leave empty to hide this object.")]
    [SerializeField] GameObject windowPanel;

    // Called by the window's close buttons
    public void CloseWindow()
    {
        (windowPanel != null ? windowPanel : gameObject).SetActive(false);
        Notebook.CompleteCurrentObjective();
    }
}
