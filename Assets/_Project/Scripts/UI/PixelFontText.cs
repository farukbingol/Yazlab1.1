using UnityEngine;
using UnityEngine.UI;

namespace Yazlab.UI
{
    /// <summary>
    /// Metne oyun açılırken piksel fontu (Quadlet, Türkçe harfler eklenmiş) uygular. Font lisansı açık repoya konmasına
    /// izin vermediği için dosya yalnız yerel kopyada (git dışı <c>LocalOnly/Resources</c>) ve build'de bulunur; font
    /// yoksa sahnede kayıtlı yedek font (Montserrat) ve boyutu olduğu gibi kalır.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Text))]
    public class PixelFontText : MonoBehaviour
    {
        /// <summary>Piksel fontun <c>Resources</c> altındaki yolu (uzantısız).</summary>
        public const string DefaultFontPath = "PixelFonts/Quadlet-TR";

        [SerializeField] private string _fontPath = DefaultFontPath;
        [Tooltip("Piksel font yüklendiğinde kullanılacak boyut. Keskin görünüm için 11'in katı (22, 33, 44...).")]
        [SerializeField] private int _pixelFontSize = 33;
        [SerializeField] private float _pixelLineSpacing = 1.2f;

        private void Awake()
        {
            Font font = Resources.Load<Font>(_fontPath);
            if (font == null)
            {
                return;
            }

            Text text = GetComponent<Text>();
            text.font = font;
            text.fontSize = _pixelFontSize;
            text.fontStyle = FontStyle.Normal;
            text.lineSpacing = _pixelLineSpacing;
            text.resizeTextForBestFit = false; // ara boyutlar pikselleri bulanıklaştırır
        }
    }
}
