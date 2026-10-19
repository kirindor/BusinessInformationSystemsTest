using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider))]
public class TriggerZone : MonoBehaviour
{
    [SerializeField] private string _targetTag = "Player";

    public UnityEvent onEnter = new UnityEvent();
    public UnityEvent onExit = new UnityEvent();

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(_targetTag))
            onEnter.Invoke();
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(_targetTag))
            onExit.Invoke();
    }
}
