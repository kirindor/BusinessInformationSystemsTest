using UnityEngine;
using UnityEngine.Events;

public class StateCheck : MonoBehaviour
{
    [SerializeField] private ObjectState target;
    [SerializeField] private bool requiredOn = true;

    public UnityEvent onPassed = new UnityEvent();
    public UnityEvent onFailed = new UnityEvent();

    public void Check()
    {
        if (target.IsOn == requiredOn)
            onPassed.Invoke();
        else
            onFailed.Invoke();
    }
}
