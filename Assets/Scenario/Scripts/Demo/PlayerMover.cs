using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMover : MonoBehaviour
{
    [SerializeField] private float speed = 5f;

    private Rigidbody body;
    private Vector3 startPosition;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        startPosition = transform.position;
    }

    private void FixedUpdate()
    {
        var keyboard = Keyboard.current;
        var input = Vector3.zero;
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) input.z += 1f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) input.z -= 1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) input.x += 1f;
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) input.x -= 1f;

        var velocity = input.normalized * speed;
        velocity.y = body.linearVelocity.y;
        body.linearVelocity = velocity;
    }

    public void ResetPosition()
    {
        body.position = startPosition;
        transform.position = startPosition;
        body.linearVelocity = Vector3.zero;
    }
}
