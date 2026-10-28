// The notebook: a real book the player carries, its pages showing the current hint and the objectives. It opens while read and shuts otherwise.
// In a headset it's read by holding it up; on a monitor a key takes it up in front of the eyes.

using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class Notebook : HeldTool
{
    static Notebook instance;

    [System.Serializable]
    public struct Objective
    {
        public string objectiveDescription;
        [TextArea] public string hintDescription;
    }

    [Header("Book")]
    [Tooltip("Where the book is held while read from the keyboard, raised in front of the eyes.")]
    [SerializeField] Transform viewfinder;
    [Tooltip("How far below the eyes the book starts as it's raised, in meters.")]
    [SerializeField] float raiseDrop = 0.15f;
    [Tooltip("How the book is turned while read from the keyboard, so its pages face the eyes.")]
    [SerializeField] Vector3 readingRotation = new(-15f, 180f, 0f);
    [Tooltip("How close to the eyes a held book must be to open, in meters. It shuts again a little further out than this.")]
    [SerializeField] float readingDistance = 0.6f;

    [Header("Pages")]
    [Tooltip("Page text for the current objective's hint.")]
    [SerializeField] TMP_Text hintText;
    [Tooltip("Page text for the objectives list.")]
    [SerializeField] TMP_Text objectivesText;
    [SerializeField] List<Objective> objectives = new();

    int currentObjectiveIndex;
    BookFold book;
    bool nearEyes;

    public Notebook() : base("Notebook", "<Keyboard>/b") { }

    protected override Transform RaisedAnchor => viewfinder;
    protected override Quaternion RaisedRotation => Quaternion.Euler(readingRotation);

    protected override void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;

        base.Awake();
        if (item != null) book = item.GetComponent<BookFold>();
        UpdatePages();
    }

    protected override void LayOut(float eased)
    {
        if (viewfinder != null) viewfinder.localPosition = new Vector3(0f, -(1f - eased) * raiseDrop, distance);
        // Open while read: taken up from the keyboard, or held in a hand or ray close to the eyes, but not over a belt slot
        if (book != null) book.Open = TakenOut || InHand && !OverBelt() && NearEyes();
    }

    bool OverBelt()
    {
        foreach (var slot in item.interactorsHovering)
            if (slot is BeltSlot) return true;
        return false;
    }

    // Whether the book is close enough to the eyes to read, a little further once it's open so it doesn't flicker at the edge
    bool NearEyes()
    {
        if (View == null) return false;
        float reach = Vector3.Distance(item.transform.position, View.transform.position);
        nearEyes = reach < readingDistance * (nearEyes ? 1.25f : 1f);
        return nearEyes;
    }

    void UpdatePages()
    {
        bool done = currentObjectiveIndex >= objectives.Count;

        if (hintText != null)
        {
            hintText.text = done ? "<b>HINTS</b>\n\nNo more objectives." : $"<b>HINT</b>\n\n{objectives[currentObjectiveIndex].hintDescription}";
        }

        if (objectivesText != null)
        {
            string text = "<b>OBJECTIVES</b>\n\n";
            if (objectives.Count == 0) text += "No objectives.";
            else if (done) text += "No more objectives.";
            else for (int i = 0; i < objectives.Count; i++)
            {
                string description = objectives[i].objectiveDescription;
                if (i < currentObjectiveIndex) text += $"<s>- {description}</s>\n";
                else if (i == currentObjectiveIndex) text += $"<b>> {description}</b>\n";
                else text += $"- {description}\n";
            }
            objectivesText.text = text;
        }
    }

    public static void CompleteCurrentObjective()
    {
        if (instance == null)
        {
            Debug.LogError("There is no Notebook in the scene to complete an objective in.");
            return;
        }
        if (instance.currentObjectiveIndex >= instance.objectives.Count)
        {
            Debug.LogWarning("Tried to complete an objective, but all objectives are already finished.");
            return;
        }
        instance.currentObjectiveIndex++;
        instance.UpdatePages();
    }
}
