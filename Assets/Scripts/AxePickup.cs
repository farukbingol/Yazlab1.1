using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
using Yazlab.Core;

// Yerde dönen balta: oyuncu yaklaşınca alınır, sağ ele takılır.
// Yerleştirme otomatiktir (rot ayarı yok): yumruğun biraz önü-üstü, hafif eğik dünya pozu.
// Boy, ölçümle kalibre edilir (BoyuDuzelt — 3 karede gerçek renderer boyu hedefe çekilir).
// Balta kuşanınca oyuncunun hasarı baltaHasarCarpani katına çıkar.
/// <summary>
/// Şef öldüğünde düşen baltanın (Hatchet) yerden alınmasını, oyuncunun sağ eline takılmasını
/// ve <see cref="GameEvents.AxePickedUp"/> olayının tetiklenmesini yöneten bileşen.
/// </summary>
public class AxePickup : MonoBehaviour
{
    [FormerlySerializedAs("toplamaMesafesi")]
    [SerializeField] private float _pickupRadius = 2f;

    [FormerlySerializedAs("donusHizi")]
    [SerializeField] private float _rotationSpeed = 90f;

    [FormerlySerializedAs("eleTaksin")]
    [SerializeField] private bool _attachToHand = true;

    [Header("El ayarı (otomatik; ince ayar için)")]
    [Tooltip("Balta uzunluğu = oyuncu boyu × bu oran")]
    [FormerlySerializedAs("hedefBoyOrani")]
    [SerializeField] private float _targetHeightRatio = 0.25f;

    [Tooltip("Boyut çarpanı — orana dokunmadan hızlı ince ayar")]
    [FormerlySerializedAs("olcekCarpani")]
    [SerializeField] private float _scaleMultiplier = 1f;

    [Tooltip("Ele takılınca sol üstte ayar panelini göster (yalnız Editor'de)")]
    [FormerlySerializedAs("ayarPaneli")]
    [SerializeField] private bool _showDebugPanel = false;

    [FormerlySerializedAs("debugKayit")]
    [SerializeField] private bool _debugLog = false;

    private Transform _playerTransform;
    private bool _isPickedUp;
    private float _baseScale = 1f; // otomatik kalibre edilen ölçek (_scaleMultiplier bunu çarpar)
    private bool _isCalibrated;
    private float _pickupAllowedAfterTime;

    private const float SpawnPickupGraceDelay = 0.85f;

    /// <summary>
    /// Baltanın oyuncu tarafından alınıp alınmadığını belirtir.
    /// </summary>
    public bool IsPickedUp => _isPickedUp;

    /// <summary>
    /// Baltayı düşüren sistemin oyuncu referansını doğrudan atamasını sağlar (`Find` çağrısını önler).
    /// </summary>
    /// <param name="playerTransform">Oyuncu Transform referansı.</param>
    public void Initialize(Transform playerTransform)
    {
        _playerTransform = playerTransform;
        _pickupAllowedAfterTime = Time.time + SpawnPickupGraceDelay;
    }

    /// <summary>
    /// Baltayı yerden almadan doğrudan oyuncunun eline takar (sahne geçişinde taşınan balta). Mesaj göstermez.
    /// </summary>
    public void EquipImmediately(GameObject playerObject)
    {
        if (_isPickedUp || playerObject == null)
        {
            return;
        }

        _playerTransform = playerObject.transform;
        _isPickedUp = true;
        AttachToPlayerHand(playerObject, false);
    }

    private void Start()
    {
        // Yere yarım gömülü kalmasın (elde taşınıyorsa dokunma).
        if (!_isPickedUp)
        {
            Vector3 pos = transform.position;
            pos.y += 0.3f;
            transform.position = pos;
        }

        if (_pickupAllowedAfterTime <= 0f)
        {
            _pickupAllowedAfterTime = Time.time + SpawnPickupGraceDelay;
        }

        if (_playerTransform == null)
        {
            ResolvePlayerOnce();
        }

        EnsureTriggerCollider();
    }

    private void ResolvePlayerOnce()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj == null)
        {
            playerObj = GameObject.Find("SurviverMale");
        }

        if (playerObj != null)
        {
            _playerTransform = playerObj.transform;
        }
    }

    private void EnsureTriggerCollider()
    {
        if (TryGetComponent<Collider>(out _))
        {
            return;
        }

        SphereCollider trigger = gameObject.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = Mathf.Max(0.5f, _pickupRadius);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_isPickedUp || other == null || Time.time < _pickupAllowedAfterTime)
        {
            return;
        }

        if (other.CompareTag("Player") || other.GetComponent<PlayerCombat>() != null)
        {
            _playerTransform = other.transform;
            Pickup(other.gameObject);
        }
    }

    private void Update()
    {
        if (_isPickedUp)
        {
            return;
        }

        transform.Rotate(0f, _rotationSpeed * Time.deltaTime, 0f, Space.World);

        if (_playerTransform == null || Time.time < _pickupAllowedAfterTime)
        {
            return;
        }

        if (Vector3.Distance(transform.position, _playerTransform.position) <= _pickupRadius)
        {
            Pickup(_playerTransform.gameObject);
        }
    }

    private void Pickup(GameObject playerObject)
    {
        if (_isPickedUp || playerObject == null)
        {
            return;
        }

        _isPickedUp = true;

        if (_attachToHand)
        {
            AttachToPlayerHand(playerObject);
        }
        else
        {
            ShowMessage("Balta alındı!");
        }

        GameEvents.RaiseAxePickedUp();
    }

    // Oyuncunun tüm renderer'larından gerçek boyu ölçer
    private static float MeasureHeight(GameObject go)
    {
        float min = float.MaxValue;
        float max = float.MinValue;
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            min = Mathf.Min(min, r.bounds.min.y);
            max = Mathf.Max(max, r.bounds.max.y);
        }

        return max > min ? max - min : 0f;
    }

    private float MeasureSelfHeight()
    {
        return MeasureHeight(gameObject);
    }

    // Balta için hedef dünya boyu: oyuncu boyu × oran × çarpan
    private float GetTargetWorldHeight()
    {
        float playerHeight = 4.18f;
        if (_playerTransform != null)
        {
            float measured = MeasureHeight(_playerTransform.gameObject);
            if (measured > 0.01f)
            {
                playerHeight = measured;
            }
        }

        return playerHeight * _targetHeightRatio * _scaleMultiplier;
    }

    private void AttachToPlayerHand(GameObject playerObject, bool announce = true)
    {
        // Parmak kemiklerini değil avuç kemiğini yakala: "mixamorig:RightHand" gibi
        Transform rightHand = null;
        foreach (var t in playerObject.GetComponentsInChildren<Transform>())
        {
            string boneName = t.name.Replace("mixamorig:", "");
            if (boneName == "RightHand")
            {
                rightHand = t;
                break;
            }
        }

        if (rightHand == null)
        {
            Debug.LogWarning("[Balta] Sağ el kemiği YOK — balta yerde kaldı. (Hierarchy'de Hatchet diye arat.)");
            ShowMessage("Balta alındı ama el bulunamadı — yerde kaldı!");
            return;
        }

        float playerHeight = MeasureHeight(playerObject);
        float handLossyScale = Mathf.Abs((rightHand.lossyScale.x + rightHand.lossyScale.y + rightHand.lossyScale.z) / 3f);
        float naturalHeight = MeasureSelfHeight();
        if (_debugLog)
        {
            Debug.Log($"[Balta] el={rightHand.name} elKayipScale={handLossyScale:0.0000} baltaDogalBoy={naturalHeight:0.0000} oyuncuBoy={playerHeight:0.00} hedefDunya={GetTargetWorldHeight():0.00}");
        }

        // Kemiğe bağla, pozu DÜNYA uzayında elle yaz — kemik eksenlerine güvenme.
        transform.SetParent(rightHand, true);
        Vector3 forward = playerObject.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f)
        {
            forward = Vector3.forward;
        }

        forward.Normalize();
        transform.position = rightHand.position + rightHand.up * (playerHeight * 0.05f) + forward * (playerHeight * 0.025f);
        transform.rotation = Quaternion.Euler(-30f, 0f, 8f); // dik konum, hafif eğik

        // Ölçek: gerçek ölçüme göre kalibrasyon halleder
        _baseScale = 1f;
        ApplyScale();
        _isCalibrated = false;
        StartCoroutine(CalibrateScaleRoutine(GetTargetWorldHeight()));

        // Balta hasarı
        PlayerCombat combat = playerObject.GetComponent<PlayerCombat>();
        if (combat != null)
        {
            combat.EquipAxe();
        }

        if (announce)
        {
            ShowMessage("Balta kuşanıldı!");
        }
    }

    // Birkaç kare gerçek renderer boyunu ölçüp localScale'i hedefe çeker
    private IEnumerator CalibrateScaleRoutine(float targetWorldHeight)
    {
        for (int i = 0; i < 3; i++)
        {
            yield return null;
            float currentHeight = MeasureSelfHeight();
            if (currentHeight > 0.0001f)
            {
                float factor = targetWorldHeight / currentHeight;
                if (!Mathf.Approximately(factor, 1f))
                {
                    transform.localScale *= factor;
                }
            }
        }

        _baseScale = Mathf.Max(0.0001f, transform.localScale.x / Mathf.Max(0.0001f, _scaleMultiplier));
        _isCalibrated = true;
        if (_debugLog)
        {
            Debug.Log($"[Balta] kalibre edildi: gerçek boy={MeasureSelfHeight():0.000} (hedef {targetWorldHeight:0.000}) localScale={transform.localScale.x:0.00} parent={(transform.parent != null ? transform.parent.name : "YOK")}");
        }
    }

    private void ApplyScale()
    {
        transform.localScale = Vector3.one * _baseScale * _scaleMultiplier;
    }

    private static void ShowMessage(string message)
    {
        if (ServiceRegistry.TryGet<IMessageService>(out var messageService))
        {
            messageService.Show(message);
        }
        else
        {
            MesajPaneli.Goster(message);
        }
    }

#if UNITY_EDITOR
    // ─── Canlı ayar paneli (Yalnızca Editor'de derlenir) ───
    private void OnGUI()
    {
        if (!_isPickedUp || !_attachToHand || !_showDebugPanel || transform.parent == null)
        {
            return;
        }

        float previousRatio = _targetHeightRatio;
        float previousMultiplier = _scaleMultiplier;

        GUI.Box(new Rect(10f, 60f, 260f, 300f), "Balta Ayari (el)");

        GUI.Label(new Rect(20f, 88f, 240f, 20f), $"Olcuk  x{_scaleMultiplier:0.00}");
        _scaleMultiplier = GUI.HorizontalSlider(new Rect(20f, 108f, 240f, 18f), _scaleMultiplier, 0.2f, 3f);

        GUI.Label(new Rect(20f, 132f, 240f, 20f), $"Boy orani  {_targetHeightRatio:0.00}");
        _targetHeightRatio = GUI.HorizontalSlider(new Rect(20f, 152f, 240f, 18f), _targetHeightRatio, 0.10f, 0.40f);

        if (_isCalibrated && (!Mathf.Approximately(previousRatio, _targetHeightRatio) || !Mathf.Approximately(previousMultiplier, _scaleMultiplier)))
        {
            StartCoroutine(CalibrateScaleRoutine(GetTargetWorldHeight()));
        }

        GUI.Label(
            new Rect(20f, 178f, 240f, 40f),
            $"Parent: {(transform.parent != null ? transform.parent.name : "YOK")}\n" +
            $"Gercek boy: {MeasureSelfHeight():0.000}  localScale: {transform.localScale.x:0.00}{(_isCalibrated ? "" : " (kalibre...)")}");

        if (GUI.Button(new Rect(20f, 218f, 115f, 24f), "Yere Birak"))
        {
            transform.SetParent(null);
            Vector3 p = transform.position;
            p.y = 0.35f;
            transform.position = p;
            _isPickedUp = false;
            _baseScale = 1f;
        }

        if (GUI.Button(new Rect(145f, 218f, 115f, 24f), "Degerleri Logla"))
        {
            Debug.Log($"[BaltaAYAR] hedefBoyOrani={_targetHeightRatio:0.00}f olcekCarpani={_scaleMultiplier:0.00}f");
        }

        GUI.Label(
            new Rect(20f, 250f, 240f, 60f),
            "Balta otomatik yerlesir; olcugu sliderla duzelt.\n" +
            "'Yere Birak' sonrasi ustune yuruyunce tekrar eline gecer.");
    }
#endif
}
