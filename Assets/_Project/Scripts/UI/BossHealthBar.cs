using UnityEngine;
using UnityEngine.UI;
using Yazlab.Core;

namespace Yazlab.UI
{
    /// <summary>
    /// Boss can barı: boss saldırgan ve oyuncu <see cref="_showDistance"/> içindeyken görünür, canı dolgu oranıyla gösterir.
    /// İlk görünüşte uyarı mesajı verir. Can değeri her karede yalnızca okunur (tahsis yok).
    /// </summary>
    [DisallowMultipleComponent]
    public class BossHealthBar : MonoBehaviour
    {
        [SerializeField] private EnemyController _boss;
        [SerializeField] private GameObject _bar;
        [SerializeField] private Image _fill;
        [SerializeField] private Text _label;
        [SerializeField] private string _bossName = "Kabile Reisi";
        [SerializeField] private string _alertMessage = "Kabile Reisi seni fark etti!";
        [SerializeField, Min(1f)] private float _showDistance = 26f;

        private Transform _player;
        private bool _alerted;
        private int _shownHealth = -1;

        private void Start()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            _player = player != null ? player.transform : null;
            if (_bar != null)
            {
                _bar.SetActive(false);
            }
        }

        private void Update()
        {
            bool visible = _boss != null && _boss.IsHostile && _player != null &&
                           (_boss.transform.position - _player.position).sqrMagnitude <= _showDistance * _showDistance;
            if (_bar != null && _bar.activeSelf != visible)
            {
                _bar.SetActive(visible);
            }

            if (!visible)
            {
                return;
            }

            if (!_alerted)
            {
                _alerted = true;
                if (ServiceRegistry.TryGet(out IMessageService messages) && !string.IsNullOrEmpty(_alertMessage))
                {
                    messages.Show(_alertMessage, 3f);
                }
            }

            int health = _boss.CurrentHealth;
            if (health == _shownHealth)
            {
                return;
            }

            _shownHealth = health;
            int max = Mathf.Max(1, _boss.MaxHealth);
            if (_fill != null)
            {
                _fill.fillAmount = Mathf.Clamp01((float)health / max);
            }

            if (_label != null)
            {
                _label.text = $"{_bossName}   {health}/{max}";
            }
        }
    }
}
