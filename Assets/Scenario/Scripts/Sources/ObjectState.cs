using UnityEngine;
using UnityEngine.Events;

public class ObjectState : MonoBehaviour
{
    [SerializeField] private bool isOn;

    public UnityEvent onTurnedOn = new UnityEvent();
    public UnityEvent onTurnedOff = new UnityEvent();

    private bool startState;

    public bool IsOn => isOn;

    private void Awake()
    {
        startState = isOn;
    }

    public void TurnOn() => SetState(true);
    public void TurnOff() => SetState(false);
    public void Toggle() => SetState(!isOn);
    public void ResetState() => SetState(startState);

    public void SetState(bool value)
    {
        if (isOn == value)
            return;

        isOn = value;
        if (isOn)
            onTurnedOn.Invoke();
        else
            onTurnedOff.Invoke();
    }
}
