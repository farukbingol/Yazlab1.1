using System;
using UnityEngine;

namespace Yazlab.Core
{
    /// <summary>
    /// Sahneler arası taşınan oyun ilerlemesi (static değil; iki sahne aynı asset'e referans verir).
    /// Alanlar serileştirilmez: asset dosyasına yazılmaz, her oyun başında <see cref="ResetRun"/> ile temizlenir.
    /// </summary>
    [CreateAssetMenu(fileName = "GameProgress", menuName = "Yazlab/Config/Game Progress")]
    public class GameProgress : ScriptableObject
    {
        [NonSerialized] private bool _hasAxe;
        [NonSerialized] private bool _bossDefeated;
        [NonSerialized] private bool _arrivedFromGate;

        /// <summary>Oyuncunun baltası var mı.</summary>
        public bool HasAxe => _hasAxe;

        /// <summary>İlk bölümün şefi yenildi mi.</summary>
        public bool BossDefeated => _bossDefeated;

        /// <summary>Oyuncu son kapıdan geçerek mi geldi (doğrudan sahne açılışında <c>false</c>).</summary>
        public bool ArrivedFromGate => _arrivedFromGate;

        /// <summary>Yeni oyun: tüm ilerlemeyi temizler (ilk sahne açılışında).</summary>
        public void ResetRun()
        {
            _hasAxe = false;
            _bossDefeated = false;
            _arrivedFromGate = false;
        }

        /// <summary>Sahne geçişinden önce mevcut durumu kaydeder.</summary>
        public void CaptureForTransition(GameStateSnapshot snapshot)
        {
            _hasAxe = snapshot.HasAxe;
            _bossDefeated = snapshot.BossDefeated;
            _arrivedFromGate = true;
        }
    }
}
