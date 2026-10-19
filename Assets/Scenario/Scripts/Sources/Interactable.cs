using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Collider))]
public class Interactable : MonoBehaviour
{
    [SerializeField] private string targetTag = "Player";
    [SerializeField] private Key key = Key.E;

    public UnityEvent onInteract = new UnityEvent();

    private bool inRange;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(targetTag))
            inRange = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(targetTag))
            inRange = false;
    }

    private void Update()
    {
        if (inRange && Keyboard.current[key].wasPressedThisFrame)
            onInteract.Invoke();
    }
}
