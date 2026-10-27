using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Kameranın hedef objeyi (oyuncuyu) sabit bir ofset ve yumuşatmayla takip etmesini sağlayan bileşen.
/// </summary>
public class CameraFollow : MonoBehaviour
{
    [FormerlySerializedAs("target")]
    [SerializeField] private Transform _target;

    [FormerlySerializedAs("offset")]
    [SerializeField] private Vector3 _offset = new Vector3(0f, 8f, -11f);

    [FormerlySerializedAs("followSharpness")]
    [SerializeField] private float _followSharpness = 5f;
    [Tooltip("Kameranın yatay yönü (derece). Ofset bu açıyla döner; 0 = kuzeye bakar, 180 = güneye (köy sahnesi).")]
    [SerializeField] private float _yaw;
    [Tooltip("Kameranın aşağı bakış açısı (derece).")]
    [SerializeField] private float _pitch = 41f;

    /// <summary>
    /// Takip edilecek hedef Transform.
    /// </summary>
    public Transform Target
    {
        get => _target;
        set => _target = value;
    }

    /// <summary>
    /// Hedefe göre dünya uzayındaki kamera ofseti.
    /// </summary>
    public Vector3 Offset
    {
        get => _offset;
        set => _offset = value;
    }

    /// <summary>
    /// Kamera takip yumuşatma katsayısı.
    /// </summary>
    public float FollowSharpness
    {
        get => _followSharpness;
        set => _followSharpness = Mathf.Max(0.1f, value);
    }

    // Eski kurulum scriptleriyle (FinishSetup) geriye dönük uyumluluk özellikleri
    public Transform target { get => Target; set => Target = value; }
    public Vector3 offset { get => Offset; set => Offset = value; }
    public float followSharpness { get => FollowSharpness; set => FollowSharpness = value; }

    /// <summary>Kameranın yatay yönü (derece).</summary>
    public float Yaw
    {
        get => _yaw;
        set => _yaw = value;
    }

    private void Awake()
    {
        transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        if (_target != null)
        {
            // İlk karede eski sahne konumundan süzülmesin.
            transform.position = _target.position + Quaternion.Euler(0f, _yaw, 0f) * _offset;
        }
    }

    private void LateUpdate()
    {
        if (_target == null)
        {
            return;
        }

        Vector3 targetPos = _target.position + Quaternion.Euler(0f, _yaw, 0f) * _offset;
        transform.position = Vector3.Lerp(transform.position, targetPos, _followSharpness * Time.deltaTime);
    }
}
