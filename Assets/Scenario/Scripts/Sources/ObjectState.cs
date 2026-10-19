using UnityEngine;
using UnityEngine.Events;

public class ObjectState : MonoBehaviour
{
    [SerializeField] private bool _isOn;

    public UnityEvent onTurnedOn = new UnityEvent();
    public UnityEvent onTurnedOff = new UnityEvent();

    private bool _startState;

    public bool IsOn => _isOn;

    private void Awake()
    {
        _startState = _isOn;
    }

    public void TurnOn() => SetState(true);
    public void TurnOff() => SetState(false);
    public void Toggle() => SetState(!_isOn);
    public void ResetState() => SetState(_startState);

    public void SetState(bool value)
    {
        if (_isOn == value)
            return;

        _isOn = value;
        if (_isOn)
            onTurnedOn.Invoke();
        else
            onTurnedOff.Invoke();
    }
}
