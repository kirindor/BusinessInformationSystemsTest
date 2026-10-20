using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class SequenceCheck : MonoBehaviour
{
    [SerializeField] private List<string> _order = new List<string> { "A", "B" };

    public UnityEvent onCompleted = new UnityEvent();
    public UnityEvent onWrongOrder = new UnityEvent();

    private int _next;

    public IReadOnlyList<string> Order => _order;

    public void Push(string key)
    {
        var index = _order.IndexOf(key);
        if (index < _next)
            return;

        if (index > _next)
        {
            _next = 0;
            onWrongOrder.Invoke();
            return;
        }

        _next++;
        if (_next < _order.Count)
            return;

        _next = 0;
        onCompleted.Invoke();
    }

    public void ResetProgress()
    {
        _next = 0;
    }
}
