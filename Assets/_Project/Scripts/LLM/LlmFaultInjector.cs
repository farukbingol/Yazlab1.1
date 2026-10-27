using UnityEngine;
using Yazlab.Core;

namespace Yazlab.LLM
{
    /// <summary>
    /// Yalnız Editor'de çalışan hata senaryosu kısayolları (rapor ekran görüntüleri ve canlı demo için):
    /// F5 sunucu kapalı (kullanılmayan port), F6 yanlış model, F7 1 sn zaman aşımı, F8 bozuk JSON (16 token sınırı),
    /// F4 config asset değerlerine dönüş. Değişiklikler <see cref="LlmRuntime"/>'ın çalışma anı kopyasına uygulanır.
    /// Build'de bileşen hiçbir şey yapmaz.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LlmRuntime))]
    public class LlmFaultInjector : MonoBehaviour
    {
        private const string ClosedServerUrl = "http://localhost:11999";
        private const string MissingModel = "yazlab-olmayan-model:1b";
        private const int ShortTimeoutSeconds = 1;
        private const int TruncatingMaxTokens = 16;

#if UNITY_EDITOR
        private LlmRuntime _runtime;

        private void Awake()
        {
            _runtime = GetComponent<LlmRuntime>();
        }

        private void Update()
        {
            if (_runtime == null || _runtime.Config == null)
            {
                return;
            }

            LlmConfig config = _runtime.Config;
            if (Input.GetKeyDown(KeyCode.F4))
            {
                _runtime.ResetToAsset();
                Notify("normal ayarlar geri yüklendi");
            }
            else if (Input.GetKeyDown(KeyCode.F5))
            {
                _runtime.TryReconfigure(ClosedServerUrl, config.ModelName, out _);
                Notify("sunucu kapalı simülasyonu (F4: normale dön)");
            }
            else if (Input.GetKeyDown(KeyCode.F6))
            {
                _runtime.TryReconfigure(config.BaseUrl, MissingModel, out _);
                Notify("yanlış model simülasyonu (F4: normale dön)");
            }
            else if (Input.GetKeyDown(KeyCode.F7))
            {
                _runtime.ApplyLimits(ShortTimeoutSeconds, config.MaxTokens);
                Notify("1 sn zaman aşımı simülasyonu (F4: normale dön)");
            }
            else if (Input.GetKeyDown(KeyCode.F8))
            {
                _runtime.ApplyLimits(config.TimeoutSeconds, TruncatingMaxTokens);
                Notify("bozuk/kesik JSON simülasyonu (F4: normale dön)");
            }
        }

        private static void Notify(string text)
        {
            Debug.Log($"[LlmFault] {text}");
            if (ServiceRegistry.TryGet(out IMessageService messages))
            {
                messages.Show($"[Test] {text}", 4f);
            }
        }
#endif
    }
}
