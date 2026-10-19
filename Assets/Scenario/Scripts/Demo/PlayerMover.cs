using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMover : MonoBehaviour
{
    [SerializeField] private float _speed = 5f;

    private Rigidbody _body;
    private Vector3 _startPosition;

    private void Awake()
    {
        _body = GetComponent<Rigidbody>();
        _startPosition = transform.position;
    }

    private void FixedUpdate()
    {
        var keyboard = Keyboard.current;
        var input = Vector3.zero;
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) input.z += 1f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) input.z -= 1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) input.x += 1f;
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) input.x -= 1f;

        var velocity = input.normalized * _speed;
        velocity.y = _body.linearVelocity.y;
        _body.linearVelocity = velocity;
    }

    public void ResetPosition()
    {
        _body.position = _startPosition;
        transform.position = _startPosition;
        _body.linearVelocity = Vector3.zero;
    }
}
