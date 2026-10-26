using UnityEngine;
using Yazlab.Companion;

/// <summary>
/// Eski CharacterController tabanlı köpek denetleyicisinin yerini alan ince sarmalayıcı.
/// Artık hareket mantığı yok; yalnızca <see cref="CompanionAgent"/>'ı gerektirir (eski kod ışınlanma içeriyordu, kaldırıldı).
/// Yeni kurulumlarda doğrudan <see cref="CompanionAgent"/> kullanılır.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CompanionAgent))]
public class CompanionController : MonoBehaviour
{
    private void Awake()
    {
        enabled = false;
    }
}
