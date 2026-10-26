using System;
using UnityEngine;

namespace Yazlab.Core
{
    /// <summary>
    /// Diyalog, menü veya bitiş ekranı açıkken oyuncu hareket/saldırı girdilerini
    /// referans sayacı (reference counting) mantığıyla kilitleyen servis.
    /// Birden fazla sistem aynı anda kilit talep ettiğinde tüm kilitler kalkmadan girdi açılmaz.
    /// </summary>
    public static class InputLock
    {
        private static int _lockCount;

        /// <summary>
        /// Oyuncu girdilerinin şu anda kilitli olup olmadığını belirtir.
        /// </summary>
        public static bool IsLocked => _lockCount > 0;

        /// <summary>
        /// Aktif kilit sayısını döndürür.
        /// </summary>
        public static int LockCount => _lockCount;

        /// <summary>
        /// Kilit durumu değiştiğinde tetiklenir (<c>true</c>: kilitlendi, <c>false</c>: açıldı).
        /// </summary>
        public static event Action<bool> LockStateChanged;

        /// <summary>
        /// Yeni bir girdi kilidi ekler.
        /// </summary>
        public static void Acquire()
        {
            bool wasLocked = IsLocked;
            _lockCount++;

            if (!wasLocked && IsLocked)
            {
                LockStateChanged?.Invoke(true);
            }
        }

        /// <summary>
        /// Mevcut bir girdi kilidini kaldırır. Sayaç sıfırın altına düşmez.
        /// </summary>
        public static void Release()
        {
            if (_lockCount <= 0)
            {
                _lockCount = 0;
                return;
            }

            _lockCount--;
            if (_lockCount == 0)
            {
                LockStateChanged?.Invoke(false);
            }
        }

        /// <summary>
        /// Tüm kilitleri sıfırlar (sahne yenileme veya oyun başlangıcı için).
        /// </summary>
        public static void Reset()
        {
            bool wasLocked = IsLocked;
            _lockCount = 0;

            if (wasLocked)
            {
                LockStateChanged?.Invoke(false);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void OnSubsystemRegistration()
        {
            _lockCount = 0;
            LockStateChanged = null;
        }
    }
}
