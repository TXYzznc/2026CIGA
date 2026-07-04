using System;
using UnityEngine;

/// <summary>
/// Candidate option for a three-choice step.
/// UI can show id/title/description/icon; gameplay can identify by id or payload.
/// </summary>
[Serializable]
public class ChoiceOption
{
    [SerializeField] private string id;
    [SerializeField] private string title;
    [TextArea, SerializeField] private string description;
    [SerializeField] private Sprite icon;
    [Min(0f), SerializeField] private float weight = 1f;

    public string Id => id;
    public string Title => title;
    public string Description => description;
    public Sprite Icon => icon;
    public float Weight => weight;
    public object Payload { get; private set; }

    public ChoiceOption() { }

    public ChoiceOption(string id, string title, string description = "", float weight = 1f, Sprite icon = null, object payload = null)
    {
        this.id = id;
        this.title = title;
        this.description = description;
        this.weight = Mathf.Max(0f, weight);
        this.icon = icon;
        Payload = payload;
    }

    public ChoiceOption WithPayload(object payload)
    {
        Payload = payload;
        return this;
    }
}
