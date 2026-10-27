using UnityEngine;
using UnityEngine.UI;
using Yazlab.Core;

namespace Yazlab.UI
{
    /// <summary>
    /// Sol üstteki "Mevcut Görev" satırı. Oyun olaylarıyla ilerler (şef yenildi → geçit açıldı → son kapı);
    /// görev geri gitmez. Sahne başına görev listesi Inspector'dan verilir (köy sahnesinde tek görev).
    /// </summary>
    [DisallowMultipleComponent]
    public class ObjectiveTracker : MonoBehaviour
    {
        [SerializeField] private Text _label;
        [Tooltip("Sırasıyla görev metinleri. Olay → indeks: BossDefeated 1, GateOpened 2.")]
        [SerializeField] private string[] _objectives =
        {
            "1/4: Ormandaki kurt şefini yen ve baltayı al",
            "2/4: Kaya duvarına ilerle, köpeğe E ile emir ver ve geçidi açtır",
            "3/4: Muhafız Kael ile konuş (E) ve son kapıdan geç"
        };
        [SerializeField] private string _completedText = "Görev tamamlandı!";
        [SerializeField] private string _prefix = "Görev — ";

        private int _index;

        /// <summary>Aktif görev indeksi.</summary>
        public int CurrentIndex => _index;

        private void OnEnable()
        {
            GameEvents.BossDefeated += OnBossDefeated;
            GameEvents.GateOpened += OnGateOpened;
            GameEvents.GameWon += OnGameWon;
        }

        private void OnDisable()
        {
            GameEvents.BossDefeated -= OnBossDefeated;
            GameEvents.GateOpened -= OnGateOpened;
            GameEvents.GameWon -= OnGameWon;
        }

        private void Start()
        {
            Show(0);
        }

        private void OnBossDefeated() => Advance(1);

        private void OnGateOpened() => Advance(2);

        private void OnGameWon()
        {
            if (_label != null)
            {
                _label.text = _prefix + _completedText;
            }
        }

        /// <summary>Görevi en az <paramref name="index"/>'e ilerletir (geri gitmez, liste dışını yok sayar).</summary>
        public void Advance(int index)
        {
            if (index > _index && index < _objectives.Length)
            {
                Show(index);
            }
        }

        private void Show(int index)
        {
            _index = Mathf.Clamp(index, 0, Mathf.Max(0, _objectives.Length - 1));
            if (_label != null && _objectives.Length > 0)
            {
                _label.text = _prefix + _objectives[_index];
            }
        }
    }
}
