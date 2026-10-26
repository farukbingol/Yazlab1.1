using UnityEngine;

// WASD ile dünya eksenlerinde hareket; Shift = koşu. Animator'daki "Speed" float'ını besler.
// Karakter 2.2x ölçekli, animasyon 1.8x tempo — hızlar buna göre kalibre (doğal adım temposu).
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    public float walkSpeed = 4.5f;
    public float runSpeed = 12f;
    public float turnSpeed = 12f;
    public float gravity = -20f;

    CharacterController controller;
    Animator animator;
    float verticalVelocity;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponentInChildren<Animator>();
    }

    void Update()
    {
        Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        Vector3 move = new Vector3(input.x, 0f, input.y);
        bool moving = move.sqrMagnitude > 0.01f;
        bool running = moving && Input.GetKey(KeyCode.LeftShift);
        float targetSpeed = running ? runSpeed : walkSpeed;

        if (moving)
        {
            move.Normalize();
            Quaternion targetRot = Quaternion.LookRotation(move);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, turnSpeed * Time.deltaTime);
        }

        // Basit yerçekimi: yerdeyken küçük aşağı basınç, havadayken birikir
        if (controller.isGrounded) verticalVelocity = -1f;
        else verticalVelocity += gravity * Time.deltaTime;

        Vector3 velocity = move * (moving ? targetSpeed : 0f);
        velocity.y = verticalVelocity;
        controller.Move(velocity * Time.deltaTime);

        if (animator)
            animator.SetFloat("Speed", new Vector2(velocity.x, velocity.z).magnitude);
    }
}
