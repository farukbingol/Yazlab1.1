using UnityEngine;
using Yazlab.Core;

namespace Yazlab.Companion
{
    /// <summary>
    /// Köpeğin sensör ışınlarının oyun içi görünümünü (<see cref="RaycastSensor.ShowGizmos"/>) bir tuşla açıp kapatır.
    /// Oyunda varsayılan kapalı (temiz görüntü); sunumda F3 ile Raycast sensörü canlı gösterilebilir.
    /// Sensörün ölçümü her durumda çalışır; yalnız çizim değişir.
    /// </summary>
    [DisallowMultipleComponent]
    public class SensorViewToggle : MonoBehaviour
    {
        [SerializeField] private RaycastSensor _sensor;
        [SerializeField] private KeyCode _toggleKey = KeyCode.F3;
        [SerializeField] private bool _visibleOnStart;

        private void Start()
        {
            if (_sensor != null)
            {
                _sensor.ShowGizmos = _visibleOnStart;
            }
        }

        private void Update()
        {
            if (_sensor == null || !Input.GetKeyDown(_toggleKey))
            {
                return;
            }

            _sensor.ShowGizmos = !_sensor.ShowGizmos;
            if (ServiceRegistry.TryGet(out IMessageService messages))
            {
                messages.Show(_sensor.ShowGizmos ? "Köpek sensör ışınları: açık (F3)" : "Köpek sensör ışınları: kapalı (F3)", 2f);
            }
        }
    }
}
