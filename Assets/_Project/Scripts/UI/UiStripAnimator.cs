using UnityEngine;
using UnityEngine.UI;

namespace Yazlab.UI
{
    /// <summary>
    /// Yan yana dizilmiş karelerden oluşan sprite şeridini (ör. SHADOW yükleme ikonları) <see cref="RawImage.uvRect"/>
    /// kaydırarak oynatır. Gerçek zamanla çalışır; oyun duraklatılmışken (başlangıç ekranı) de döner.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RawImage))]
    public class UiStripAnimator : MonoBehaviour
    {
        [SerializeField] private int _frameCount = 8;
        [Tooltip("Bir karenin piksel genişliği.")]
        [SerializeField] private int _frameWidth = 14;
        [Tooltip("Ardışık karelerin başlangıçları arasındaki piksel mesafesi (kare + boşluk).")]
        [SerializeField] private int _frameStride = 18;
        [SerializeField] private float _framesPerSecond = 10f;

        private RawImage _image;

        private void Awake()
        {
            _image = GetComponent<RawImage>();
        }

        private void OnEnable()
        {
            ShowFrame(0);
        }

        private void Update()
        {
            if (_frameCount > 1)
            {
                ShowFrame(Mathf.FloorToInt(Time.unscaledTime * _framesPerSecond) % _frameCount);
            }
        }

        private void ShowFrame(int index)
        {
            Texture texture = _image != null ? _image.texture : null;
            if (texture == null || texture.width <= 0)
            {
                return;
            }

            float w = texture.width;
            _image.uvRect = new Rect(index * _frameStride / w, 0f, _frameWidth / w, 1f);
        }
    }
}
