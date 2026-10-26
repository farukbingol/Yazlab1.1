namespace Yazlab.Core
{
    /// <summary>
    /// Oyuncuya ekran üzerinde bilgilendirme veya durum mesajı gösteren servis sözleşmesi.
    /// </summary>
    public interface IMessageService
    {
        /// <summary>
        /// Ekranda belirtilen süre boyunca Türkçe bilgilendirme mesajı gösterir.
        /// </summary>
        /// <param name="text">Gösterilecek mesaj metni.</param>
        /// <param name="duration">Mesajın ekranda kalma süresi (gerçek saniye).</param>
        void Show(string text, float duration = 4f);
    }
}
