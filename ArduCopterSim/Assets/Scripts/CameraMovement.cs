using UnityEngine;
using UnityEngine.InputSystem;

public class CameraMovement : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 10f;
    public float fastMultiplier = 3f;     // Hold Shift
    public float climbSpeed = 4f;         // Q/E

    [Header("Look")]
    public float mouseSensitivity = 0.15f; // degrees per pixel
    public bool requireRightMouseToLook = false;

    private float yaw;
    private float pitch;

    void OnEnable()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        var e = transform.rotation.eulerAngles;
        pitch = e.x;
        yaw = e.y;
    }

    void Update()
    {
        if (Keyboard.current == null) return;
        if (Mouse.current == null) return;

        // ESC to toggle cursor lock
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        bool canLook = Cursor.lockState == CursorLockMode.Locked &&
                       (!requireRightMouseToLook || Mouse.current.rightButton.isPressed);


        if (canLook)
        {
            Vector2 md = Mouse.current.delta.ReadValue();
            yaw   += md.x * mouseSensitivity;
            pitch -= md.y * mouseSensitivity;
            pitch = Mathf.Clamp(pitch, -89.9f, 89.9f);
            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        // Movement input (WASD)
        Vector3 move = Vector3.zero;
        if (Keyboard.current.wKey.isPressed) move += transform.forward;
        if (Keyboard.current.sKey.isPressed) move -= transform.forward;
        if (Keyboard.current.aKey.isPressed) move -= transform.right;
        if (Keyboard.current.dKey.isPressed) move += transform.right;

        // Up/Down (Q/E)
        if (Keyboard.current.eKey.isPressed) move += Vector3.up * (climbSpeed / moveSpeed);
        if (Keyboard.current.qKey.isPressed) move -= Vector3.up * (climbSpeed / moveSpeed);

        float speed = moveSpeed * (Keyboard.current.leftShiftKey.isPressed ? fastMultiplier : 1f);

        if (move.sqrMagnitude > 1e-4f)
        {
            transform.position += move.normalized * speed * Time.unscaledDeltaTime;
        }
    }
}
