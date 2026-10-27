using System;

namespace Yazlab.LLM
{
    /// <summary>LLM bağlantısının oyuncuya gösterilen durumu.</summary>
    public enum LlmHealthState
    {
        /// <summary>Henüz kontrol edilmedi / bağlanılıyor.</summary>
        Connecting,

        /// <summary>Sunucu ve model var; model belleğe yükleniyor (ısınma isteği).</summary>
        WarmingUp,

        /// <summary>Yanıt vermeye hazır.</summary>
        Ready,

        /// <summary>Sunucu kapalı, model yok, zaman aşımı veya ayar geçersiz.</summary>
        Unavailable
    }

    /// <summary>
    /// Oyun boyunca tek LLM bağlantısı: çalışma anı config kopyası, istemci, sağlık durumu ve yeniden yapılandırma.
    /// <see cref="Yazlab.Core.ServiceRegistry"/> üzerinden erişilir.
    /// </summary>
    public interface ILlmRuntime
    {
        /// <summary>Çalışma anı config kopyası (asset değil).</summary>
        LlmConfig Config { get; }

        /// <summary>Aktif istemci (yeniden yapılandırmada değişir).</summary>
        ILlmClient Client { get; }

        /// <summary>Son bilinen durum.</summary>
        LlmHealthState State { get; }

        /// <summary>Son durumun kısa Türkçe açıklaması (rozet metni).</summary>
        string StatusText { get; }

        /// <summary>Son sağlık kontrolü sonucu (yoksa <c>null</c>).</summary>
        LlmResult LastHealth { get; }

        /// <summary>Durum veya açıklama değiştiğinde tetiklenir.</summary>
        event Action<LlmHealthState, string> StatusChanged;

        /// <summary>Sunucu adresi/model adını değiştirir; geçersizse (yerel/LAN değil) uygulanmaz ve <paramref name="error"/> döner.</summary>
        bool TryReconfigure(string baseUrl, string modelName, out string error);

        /// <summary>Hemen sağlık kontrolü (+ gerekirse ısınma) başlatır.</summary>
        void RequestHealthCheck();

        /// <summary>Bir sohbet isteğinin sonucunu bildirir (bağlantı hatası durumu hemen kırmızıya çeker).</summary>
        void Report(LlmResult result);
    }
}
