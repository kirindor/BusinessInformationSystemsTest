using System;
using UnityEngine;
using UnityEngine.Events;

public class ScenarioStep : MonoBehaviour
{
    [SerializeField] private string id;
    [SerializeField, TextArea] private string description;

    public UnityEvent onActivated = new UnityEvent();
    public UnityEvent onDeactivated = new UnityEvent();

    public event Action<ScenarioStep, bool, string> Reported;

    public string Id => id;
    public string Description => description;

    public void Complete()
    {
        Reported?.Invoke(this, true, null);
    }

    public void Violate(string reason)
    {
        Reported?.Invoke(this, false, reason);
    }

    private void Reset()
    {
        id = name;
    }
}
