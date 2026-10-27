// The notebook: raising it shows the objectives and the current objective's hint.

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

public class Notebook : HeldTool
{
    static readonly int OpenId = Shader.PropertyToID("_Open");

    static Notebook instance;

    [System.Serializable]
    public struct Objective
    {
        public string objectiveDescription;
        [TextArea] public string hintDescription;
    }

    [Header("Notebook")]
    [SerializeField] Transform notebookTransform;
    [SerializeField] MeshRenderer notebookMesh;
    [SerializeField] float raiseDrop = 0.25f;
    [Tooltip("How wide the notebook looks in a headset, in degrees. Narrower than the view, so both eyes see its edges.")]
    [SerializeField] float headsetWidth = 70f;

    [Header("Pages")]
    [Tooltip("Page text for the current objective's hint.")]
    [SerializeField, FormerlySerializedAs("leftPageHintText")] TMP_Text hintText;
    [Tooltip("Page text for the objectives list.")]
    [SerializeField, FormerlySerializedAs("rightPageObjectivesText")] TMP_Text objectivesText;
    [SerializeField] List<Objective> objectives = new();

    MaterialPropertyBlock notebookBlock;
    float bookWidth;
    int currentObjectiveIndex;

    public Notebook() : base("Notebook", "<XRController>{RightHand}/primaryButton", "A", "<Keyboard>/g") { }

    protected override void Awake()
    {
        base.Awake();
        instance = this;
        if (notebookTransform == null) notebookTransform = transform;
        notebookBlock = new MaterialPropertyBlock();
        if (notebookMesh != null) bookWidth = notebookMesh.transform.localScale.x * notebookMesh.GetComponent<MeshFilter>().sharedMesh.bounds.size.x;
    }

    // The page texts are children of the notebook, so they rise, fall and hide with it
    protected override void LayOut(float eased)
    {
        bool shown = eased > 0f;
        if (notebookTransform.gameObject.activeSelf != shown) notebookTransform.gameObject.SetActive(shown);
        if (!shown) return;

        float scale = Headset && bookWidth > 0f ? 2f * distance * Mathf.Tan(headsetWidth * 0.5f * Mathf.Deg2Rad) / bookWidth : 1f;
        notebookTransform.localScale = Vector3.one * scale;
        notebookTransform.localPosition = new Vector3(0f, -(1f - eased) * raiseDrop * scale, distance);
        if (notebookMesh != null)
        {
            notebookMesh.GetPropertyBlock(notebookBlock);
            notebookBlock.SetFloat(OpenId, eased);
            notebookMesh.SetPropertyBlock(notebookBlock);
        }
        UpdatePages(eased);
    }

    void UpdatePages(float alpha)
    {
        bool done = currentObjectiveIndex >= objectives.Count;

        if (hintText != null)
        {
            hintText.text = done ? "<b>HINTS</b>\n\nNo more objectives." : $"<b>HINT</b>\n\n{objectives[currentObjectiveIndex].hintDescription}";
            hintText.alpha = alpha;
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
            objectivesText.alpha = alpha;
        }
    }

    // Marks the current objective done and moves on to the next
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
        instance.UpdatePages(instance.Raised);
    }
}
