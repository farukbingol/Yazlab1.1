using System;
using UnityEngine;
using Yazlab.Core;

namespace Yazlab.Player
{
    /// <summary>
    /// Oyuncu klavye/fare girdilerini tek merkezden okuyan ve <see cref="InputLock"/>
    /// aktifken tüm girdileri güvenli şekilde sıfırlayan bileşen.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    [DisallowMultipleComponent]
    public class PlayerInputReader : MonoBehaviour
    {
        private Vector2 _move;
        private bool _isRunHeld;
        private bool _attackPressed;
        private bool _interactPressed;

        /// <summary>
        /// Yatay ve dikey hareket girdisi (WASD / yön tuşları).
        /// <see cref="InputLock.IsLocked"/> aktifken <see cref="Vector2.zero"/> döner.
        /// </summary>
        public Vector2 Move => InputLock.IsLocked ? Vector2.zero : _move;

        /// <summary>
        /// Koşma tuşu (Sol/Sağ Shift) basılı tutuluyor mu?
        /// <see cref="InputLock.IsLocked"/> aktifken <c>false</c> döner.
        /// </summary>
        public bool IsRunHeld => !InputLock.IsLocked && _isRunHeld;

        /// <summary>
        /// Bu karede saldırı tuşuna (Sol Tık veya F) basıldı mı?
        /// <see cref="InputLock.IsLocked"/> aktifken <c>false</c> döner.
        /// </summary>
        public bool AttackPressed => !InputLock.IsLocked && _attackPressed;

        /// <summary>
        /// Bu karede etkileşim / emir tuşuna (E) basıldı mı?
        /// <see cref="InputLock.IsLocked"/> aktifken <c>false</c> döner.
        /// </summary>
        public bool InteractPressed => !InputLock.IsLocked && _interactPressed;

        /// <summary>
        /// Etkileşim tuşuna (E) basıldığında tetiklenen olay (<see cref="InputLock"/> kilitli değilse).
        /// </summary>
        public event Action InteractTriggered;

        /// <summary>
        /// Saldırı tuşuna basıldığında tetiklenen olay (<see cref="InputLock"/> kilitli değilse).
        /// </summary>
        public event Action AttackTriggered;

        private void Update()
        {
            SampleInput();
        }

        /// <summary>
        /// Girdileri anlık olarak örnekler. <see cref="InputLock.IsLocked"/> aktifse tüm değerleri sıfırlar.
        /// </summary>
        public void SampleInput()
        {
            if (InputLock.IsLocked)
            {
                ClearInputState();
                return;
            }

            _move = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            _isRunHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            _attackPressed = Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.F);
            _interactPressed = Input.GetKeyDown(KeyCode.E);

            if (_attackPressed)
            {
                AttackTriggered?.Invoke();
            }

            if (_interactPressed)
            {
                InteractTriggered?.Invoke();
            }
        }

        private void OnEnable()
        {
            InputLock.LockStateChanged += OnInputLockStateChanged;
        }

        private void OnDisable()
        {
            InputLock.LockStateChanged -= OnInputLockStateChanged;
            ClearInputState();
        }

        private void OnInputLockStateChanged(bool isLocked)
        {
            if (isLocked)
            {
                ClearInputState();
            }
        }

        private void ClearInputState()
        {
            _move = Vector2.zero;
            _isRunHeld = false;
            _attackPressed = false;
            _interactPressed = false;
        }
    }
}
