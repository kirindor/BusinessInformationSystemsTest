using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Collider))]
public class Interactable : MonoBehaviour
{
    [SerializeField] private string _targetTag = "Player";
    [SerializeField] private Key _key = Key.E;

    public UnityEvent onInteract = new UnityEvent();

    private bool _inRange;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(_targetTag))
            _inRange = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(_targetTag))
            _inRange = false;
    }

    private void Update()
    {
        if (_inRange && Keyboard.current[_key].wasPressedThisFrame)
            onInteract.Invoke();
    }
}
