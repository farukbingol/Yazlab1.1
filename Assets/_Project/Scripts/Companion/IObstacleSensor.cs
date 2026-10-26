using Yazlab.World;

namespace Yazlab.Companion
{
    /// <summary>
    /// Köpek refakatçinin çevresindeki fiziksel engelleri ve <see cref="IInteractable"/> hedefleri
    /// algılayan sensör sözleşmesi.
    /// </summary>
    public interface IObstacleSensor
    {
        /// <summary>En son yapılan sensör okumasının sonucu.</summary>
        SensorReading LastReading { get; }

        /// <summary>
        /// Çevredeki engelleri ve etkileşim hedeflerini tarayarak yeni bir <see cref="SensorReading"/> üretir.
        /// </summary>
        /// <param name="desiredTarget">
        /// Varsa öncelikli olarak görüş hattı (Line of Sight) kontrol edilecek hedef;
        /// <c>null</c> ise menzildeki en yakın uygun hedef aranır.
        /// </param>
        SensorReading Sense(IInteractable desiredTarget = null);
    }
}
