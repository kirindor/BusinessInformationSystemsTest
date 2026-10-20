using UnityEngine;
using UnityEngine.Events;

public class StateCheck : MonoBehaviour
{
    [SerializeField] private ObjectState _target;
    [SerializeField] private bool _requiredOn = true;

    public UnityEvent onPassed = new UnityEvent();
    public UnityEvent onFailed = new UnityEvent();

    public ObjectState Target => _target;
    public bool RequiredOn => _requiredOn;

    public void Check()
    {
        if (_target.IsOn == _requiredOn)
            onPassed.Invoke();
        else
            onFailed.Invoke();
    }
}
