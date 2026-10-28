using UnityEngine;
using UnityEngine.UI;

namespace Yazlab.UI
{
    /// <summary>
    /// Oyuncu can barı: <see cref="PlayerHealth.HealthChanged"/> olayını dinler, dolguyu anında, arkasındaki açık
    /// "hasar izi"ni kısa gecikmeyle azaltır; can düşükken dolgu nabız gibi atar. Balta kuşanılınca balta rozeti görünür.
    /// Yalnızca gösterim yapar; oyun durumunu değiştirmez.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerHealthBar : MonoBehaviour
    {
        [SerializeField] private PlayerHealth _health;
        [SerializeField] private PlayerCombat _combat;
        [SerializeField] private Image _fill;
        [SerializeField] private Image _trail;
        [SerializeField] private Text _label;
        [SerializeField] private GameObject _axeBadge;
        [SerializeField] private Color _fillColor = new Color(0.78f, 0.2f, 0.24f, 1f);
        [SerializeField] private Color _lowColor = new Color(1f, 0.36f, 0.3f, 1f);
        [SerializeField, Range(0f, 1f)] private float _lowThreshold = 0.3f;
        [SerializeField, Min(0f)] private float _trailDelay = 0.35f;
        [SerializeField, Min(0.1f)] private float _trailSpeed = 0.9f;

        private float _target = 1f;
        private float _trailHoldUntil;

        private void OnEnable()
        {
            if (_health != null)
            {
                _health.HealthChanged += OnHealthChanged;
                OnHealthChanged(_health.CurrentHealth, _health.MaxHealth);
                SnapTrail();
            }
        }

        private void OnDisable()
        {
            if (_health != null)
            {
                _health.HealthChanged -= OnHealthChanged;
            }
        }

        private void OnHealthChanged(int current, int max)
        {
            float ratio = max > 0 ? Mathf.Clamp01((float)current / max) : 0f;
            if (ratio < _target)
            {
                _trailHoldUntil = Time.unscaledTime + _trailDelay;
            }
            else if (_trail != null)
            {
                _trail.fillAmount = ratio; // iyileşmede iz beklemez
            }

            _target = ratio;
            if (_fill != null)
            {
                _fill.fillAmount = ratio;
            }

            if (_label != null)
            {
                _label.text = $"{Mathf.Max(0, current)} / {max}";
            }
        }

        private void SnapTrail()
        {
            if (_trail != null)
            {
                _trail.fillAmount = _target;
            }
        }

        private void Update()
        {
            if (_trail != null && _trail.fillAmount > _target && Time.unscaledTime >= _trailHoldUntil)
            {
                _trail.fillAmount = Mathf.MoveTowards(_trail.fillAmount, _target, _trailSpeed * Time.unscaledDeltaTime);
            }

            if (_fill != null)
            {
                bool low = _target > 0f && _target <= _lowThreshold;
                _fill.color = low
                    ? Color.Lerp(_fillColor, _lowColor, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 7f))
                    : _fillColor;
            }

            if (_axeBadge != null && _combat != null && _axeBadge.activeSelf != _combat.HasAxe)
            {
                _axeBadge.SetActive(_combat.HasAxe);
            }
        }
    }
}
