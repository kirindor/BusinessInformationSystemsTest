using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider))]
public class CollisionSensor : MonoBehaviour
{
    [SerializeField] private string _targetTag = "Player";

    public UnityEvent onHit = new UnityEvent();

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag(_targetTag))
            onHit.Invoke();
    }
}
