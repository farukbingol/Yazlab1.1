using UnityEngine;
using UnityEngine.Serialization;
using Yazlab.Core;
using Yazlab.Player;

// WASD ile dünya eksenlerinde hareket; Shift = koşu. Animator'daki "Speed" float'ını besler.
// Karakter 2.2x ölçekli, animasyon 1.8x tempo — hızlar buna göre kalibre (doğal adım temposu).
/// <summary>
/// Oyuncu karakterinin yürüme, koşma, dönüş ve yerçekimi hareketini yöneten bileşen.
/// Girdileri <see cref="PlayerInputReader"/> üzerinden alır ve <see cref="InputLock"/> durumuna uyar.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    private static readonly int SpeedParamHash = Animator.StringToHash("Speed");

    [Header("Yapılandırma ve Bağımlılıklar")]
    [SerializeField] private PlayerConfig config;
    [SerializeField] private PlayerInputReader inputReader;

    [Header("Hareket Parametreleri (Config yoksa kullanılır)")]
    [FormerlySerializedAs("walkSpeed")]
    [SerializeField] private float _walkSpeed = 4.5f;

    [FormerlySerializedAs("runSpeed")]
    [SerializeField] private float _runSpeed = 12f;

    [FormerlySerializedAs("turnSpeed")]
    [SerializeField] private float _turnSpeed = 12f;

    [FormerlySerializedAs("gravity")]
    [SerializeField] private float _gravity = -20f;

    private CharacterController _controller;
    private Animator _animator;
    private float _verticalVelocity;

    /// <summary>
    /// Oyuncu yapılandırma varlığı (varsa hız ve yerçekimi değerleri buradan okunur).
    /// </summary>
    public PlayerConfig Config
    {
        get => config;
        set => config = value;
    }

    /// <summary>
    /// Oyuncu girdi okuyucu bileşeni.
    /// </summary>
    public PlayerInputReader InputReader
    {
        get => inputReader;
        set => inputReader = value;
    }

    /// <summary>
    /// Normal yürüme hızı (m/sn).
    /// </summary>
    public float WalkSpeed
    {
        get => config != null ? config.WalkSpeed : _walkSpeed;
        set => _walkSpeed = Mathf.Max(0f, value);
    }

    /// <summary>
    /// Koşma hızı (m/sn).
    /// </summary>
    public float RunSpeed
    {
        get => config != null ? config.RunSpeed : _runSpeed;
        set => _runSpeed = Mathf.Max(0f, value);
    }

    /// <summary>
    /// Hareket yönüne dönüş yumuşatma hızı.
    /// </summary>
    public float TurnSpeed
    {
        get => config != null ? config.TurnSpeed : _turnSpeed;
        set => _turnSpeed = Mathf.Max(0f, value);
    }

    /// <summary>
    /// Yerçekimi ivmesi (m/sn²).
    /// </summary>
    public float Gravity
    {
        get => config != null ? config.Gravity : _gravity;
        set => _gravity = value;
    }

    // Eski Editor kurulum scriptleriyle (FinishSetup) geriye dönük uyumluluk özellikleri
    public float walkSpeed
    {
        get => WalkSpeed;
        set => WalkSpeed = value;
    }

    public float runSpeed
    {
        get => RunSpeed;
        set => RunSpeed = value;
    }

    public float turnSpeed
    {
        get => TurnSpeed;
        set => TurnSpeed = value;
    }

    public float gravity
    {
        get => Gravity;
        set => Gravity = value;
    }

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
        _animator = GetComponentInChildren<Animator>();

        if (inputReader == null && !TryGetComponent(out inputReader))
        {
            inputReader = gameObject.AddComponent<PlayerInputReader>();
        }
    }

    private void OnDisable()
    {
        if (_animator != null && _animator.isActiveAndEnabled && _animator.runtimeAnimatorController != null)
        {
            _animator.SetFloat(SpeedParamHash, 0f);
        }
    }

    private void Update()
    {
        if (_controller == null || !_controller.enabled)
        {
            return;
        }

        Vector2 input = Vector2.zero;
        bool runHeld = false;

        if (!InputLock.IsLocked && inputReader != null)
        {
            input = inputReader.Move;
            runHeld = inputReader.IsRunHeld;
        }

        Vector3 move = new Vector3(input.x, 0f, input.y);
        bool moving = move.sqrMagnitude > 0.01f;
        bool running = moving && runHeld;
        float targetSpeed = running ? RunSpeed : WalkSpeed;

        if (moving)
        {
            move.Normalize();
            Quaternion targetRot = Quaternion.LookRotation(move);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, TurnSpeed * Time.deltaTime);
        }

        // Basit yerçekimi: yerdeyken küçük aşağı basınç, havadayken birikir
        if (_controller.isGrounded)
        {
            _verticalVelocity = -1f;
        }
        else
        {
            _verticalVelocity += Gravity * Time.deltaTime;
        }

        Vector3 velocity = move * (moving ? targetSpeed : 0f);
        velocity.y = _verticalVelocity;
        _controller.Move(velocity * Time.deltaTime);

        if (_animator != null && _animator.isActiveAndEnabled && _animator.runtimeAnimatorController != null)
        {
            float horizontalSpeed = new Vector2(velocity.x, velocity.z).magnitude;
            _animator.SetFloat(SpeedParamHash, horizontalSpeed);
        }
    }
}
