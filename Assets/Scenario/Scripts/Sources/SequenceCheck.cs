using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class SequenceCheck : MonoBehaviour
{
    [SerializeField] private List<string> order = new List<string> { "A", "B" };

    public UnityEvent onCompleted = new UnityEvent();
    public UnityEvent onWrongOrder = new UnityEvent();

    private int next;

    public void Push(string key)
    {
        var index = order.IndexOf(key);
        if (index < next)
            return;

        if (index > next)
        {
            next = 0;
            onWrongOrder.Invoke();
            return;
        }

        next++;
        if (next < order.Count)
            return;

        next = 0;
        onCompleted.Invoke();
    }

    public void ResetProgress()
    {
        next = 0;
    }
}
