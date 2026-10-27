using System.Threading;
using UnityEngine;

namespace Yazlab.LLM
{
    /// <summary>
    /// Yerel LLM sunucusu istemcisi sözleşmesi. Uygulamalar ana thread'i bloklamaz ve istisna fırlatmaz;
    /// her durum <see cref="LlmResult"/> ile döner.
    /// </summary>
    public interface ILlmClient
    {
        /// <summary>Sohbet isteği gönderir; başarıda <see cref="LlmResult.RawContent"/> modelin ürettiği metindir.</summary>
        Awaitable<LlmResult> SendChatAsync(LlmRequest request, CancellationToken cancellationToken);

        /// <summary>Sunucunun açık ve yapılandırılmış modelin yüklü olduğunu denetler.</summary>
        Awaitable<LlmResult> CheckHealthAsync(CancellationToken cancellationToken);
    }
}
