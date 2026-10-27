using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

public class Notebook : MonoBehaviour
{
    static Notebook instance;

    [System.Serializable]
    public struct Objective
    {
        public string objectiveDescription;
        [TextArea] public string hintDescription;
    }

    [Header("Pages")]
    [Tooltip("Page text for the current objective's hint.")]
    [SerializeField, FormerlySerializedAs("leftPageHintText")] TMP_Text hintText;
    [Tooltip("Page text for the objectives list.")]
    [SerializeField, FormerlySerializedAs("rightPageObjectivesText")] TMP_Text objectivesText;
    [SerializeField] List<Objective> objectives = new();

    int currentObjectiveIndex;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        UpdatePages();
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