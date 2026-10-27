using UnityEngine;
using UnityEngine.UI;
using Yazlab.Core;
using Yazlab.LLM;

namespace Yazlab.UI
{
    /// <summary>
    /// Ekran köşesindeki LLM bağlantı rozeti: yeşil "Ollama hazır (model)", sarı "bağlanılıyor / ısınıyor",
    /// kırmızı "bağlantı yok / model yok / yanıt yok". <see cref="ILlmRuntime.StatusChanged"/> olayını dinler.
    /// </summary>
    [DisallowMultipleComponent]
    public class LlmStatusIndicator : MonoBehaviour
    {
        [SerializeField] private Image _dot;
        [SerializeField] private Text _label;
        [SerializeField] private Color _readyColor = new Color(0.3f, 0.85f, 0.35f);
        [SerializeField] private Color _pendingColor = new Color(1f, 0.82f, 0.2f);
        [SerializeField] private Color _errorColor = new Color(0.95f, 0.3f, 0.28f);
        [Tooltip("Rozetin altına ayar paneli kısayolunu yaz.")]
        [SerializeField] private string _hint = "F2: LLM ayarları";

        private ILlmRuntime _runtime;

        private void Start()
        {
            if (!ServiceRegistry.TryGet(out _runtime))
            {
                Apply(LlmHealthState.Unavailable, "LLM servisi yok");
                return;
            }

            _runtime.StatusChanged += Apply;
            Apply(_runtime.State, _runtime.StatusText);
        }

        private void OnDestroy()
        {
            if (_runtime != null)
            {
                _runtime.StatusChanged -= Apply;
            }
        }

        private void Apply(LlmHealthState state, string text)
        {
            Color color = state == LlmHealthState.Ready ? _readyColor
                : state == LlmHealthState.Unavailable ? _errorColor
                : _pendingColor;

            if (_dot != null)
            {
                _dot.color = color;
            }

            if (_label != null)
            {
                _label.text = string.IsNullOrEmpty(_hint) ? text : $"{text}\n<size=11>{_hint}</size>";
            }
        }
    }
}
