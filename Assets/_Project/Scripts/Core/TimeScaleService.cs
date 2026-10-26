using System.Collections;
using UnityEngine;

namespace Yazlab.Core
{
    /// <summary>
    /// Oyun içi zaman ölçeği (<see cref="Time.timeScale"/>) yönetim sözleşmesi.
    /// </summary>
    public interface ITimeScaleService
    {
        /// <summary>
        /// Oyunun duraklatılmış (pause) olup olmadığını belirtir.
        /// </summary>
        bool IsPaused { get; }

        /// <summary>
        /// Oyunu duraklatır veya devam ettirir.
        /// </summary>
        /// <param name="paused">Duraklatma durumu.</param>
        void SetPaused(bool paused);

        /// <summary>
        /// Vuruş hissi için kısa süreli zaman yavaşlatması (HitStop) uygular.
        /// <see cref="WaitForSecondsRealtime"/> kullanır ve pause durumuyla çakışmaz.
        /// </summary>
        /// <param name="durationRealtime">Gerçek saniye cinsinden süre.</param>
        /// <param name="scale">HitStop sırasındaki zaman ölçeği (0 ile 1 arası).</param>
        void TriggerHitStop(float durationRealtime, float scale = 0.05f);

        /// <summary>
        /// Zaman ölçeğini varsayılan (1.0) değerine sıfırlar.
        /// </summary>
        void ResetTimeScale();
    }

    /// <summary>
    /// HitStop ve Pause durumlarının birbirini ezmesini önleyen merkezi zaman ölçeği bileşeni.
    /// <see cref="ServiceRegistry"/> üzerinden <see cref="ITimeScaleService"/> olarak erişilebilir.
    /// </summary>
    [DisallowMultipleComponent]
    public class TimeScaleService : MonoBehaviour, ITimeScaleService
    {
        private bool _isPaused;
        private bool _isHitStopActive;
        private float _hitStopScale = 1f;
        private float _hitStopEndUnscaledTime;
        private Coroutine _hitStopCoroutine;

        /// <inheritdoc />
        public bool IsPaused => _isPaused;

        private void Awake()
        {
            ServiceRegistry.Register<ITimeScaleService>(this);
            ServiceRegistry.Register(this);
            ApplyEffectiveTimeScale();
        }

        private void OnDisable()
        {
            ResetTimeScale();
        }

        private void OnDestroy()
        {
            if (Time.timeScale != 1f)
            {
                Time.timeScale = 1f;
            }

            ServiceRegistry.Unregister<ITimeScaleService>(this);
            ServiceRegistry.Unregister(this);
        }

        /// <inheritdoc />
        public void SetPaused(bool paused)
        {
            _isPaused = paused;
            ApplyEffectiveTimeScale();
        }

        /// <inheritdoc />
        public void TriggerHitStop(float durationRealtime, float scale = 0.05f)
        {
            if (durationRealtime <= 0f || !isActiveAndEnabled)
            {
                return;
            }

            float clampedScale = Mathf.Clamp(scale, 0f, 1f);
            float targetEndTime = Time.unscaledTime + durationRealtime;

            if (_isHitStopActive)
            {
                _hitStopEndUnscaledTime = Mathf.Max(_hitStopEndUnscaledTime, targetEndTime);
                _hitStopScale = Mathf.Min(_hitStopScale, clampedScale);
                ApplyEffectiveTimeScale();
                return;
            }

            _hitStopScale = clampedScale;
            _hitStopEndUnscaledTime = targetEndTime;

            if (_hitStopCoroutine != null)
            {
                StopCoroutine(_hitStopCoroutine);
            }

            _hitStopCoroutine = StartCoroutine(HitStopRoutine());
        }

        /// <inheritdoc />
        public void ResetTimeScale()
        {
            if (_hitStopCoroutine != null)
            {
                StopCoroutine(_hitStopCoroutine);
                _hitStopCoroutine = null;
            }

            _isPaused = false;
            _isHitStopActive = false;
            _hitStopScale = 1f;
            Time.timeScale = 1f;
        }

        private IEnumerator HitStopRoutine()
        {
            _isHitStopActive = true;
            ApplyEffectiveTimeScale();

            while (Time.unscaledTime < _hitStopEndUnscaledTime)
            {
                float remaining = _hitStopEndUnscaledTime - Time.unscaledTime;
                yield return new WaitForSecondsRealtime(Mathf.Max(0.005f, remaining));
            }

            _isHitStopActive = false;
            _hitStopScale = 1f;
            _hitStopCoroutine = null;
            ApplyEffectiveTimeScale();
        }

        private void ApplyEffectiveTimeScale()
        {
            if (_isPaused)
            {
                Time.timeScale = 0f;
            }
            else if (_isHitStopActive)
            {
                Time.timeScale = _hitStopScale;
            }
            else
            {
                Time.timeScale = 1f;
            }
        }
    }
}
