using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider))]
public class TriggerZone : MonoBehaviour
{
    [SerializeField] private string targetTag = "Player";

    public UnityEvent onEnter = new UnityEvent();
    public UnityEvent onExit = new UnityEvent();

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(targetTag))
            onEnter.Invoke();
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(targetTag))
            onExit.Invoke();
    }
}
