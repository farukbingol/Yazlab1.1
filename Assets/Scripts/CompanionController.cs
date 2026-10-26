using UnityEngine;

// Köpek refakatçi: oyuncuyu takip eder, uzaklaşınca dörtnala gelir.
// NavMesh yok — CharacterController ile yürür; oyuncu çok uzaklaşırsa yanına ışınlanır.
// Animator Speed değeri: 0 = Idle, ~2.8 = Walk, ~6.5 = Gallop (KopekAnimator eşikleri).
public class CompanionController : MonoBehaviour
{
    public float yurumeHizi = 2.8f;
    public float kosmaHizi = 6.5f;
    public float durmaMesafesi = 3f;     // oyuncu bu kadar yakınsa durur
    public float gallopEsigi = 6.5f;     // bu kadar uzakta dörtnala geçer
    public float isinlanmaMesafesi = 40f;
    public float donusHizi = 10f;
    public bool debugKayit = true;

    Transform oyuncu;
    CharacterController controller;
    Animator animator;
    float dikeyHiz;
    bool ccOnarildi;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        if (controller == null)
        {
            controller = gameObject.AddComponent<CharacterController>();
            controller.height = 1.0f; controller.radius = 0.3f; controller.center = new Vector3(0f, 0.5f, 0f);
            controller.stepOffset = 0f; // aksi halde Unity doğrulamada CC'yi kapatır
        }
        animator = GetComponentInChildren<Animator>();
        OyuncuBul();
    }

    void OyuncuBul()
    {
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p == null) p = GameObject.Find("SurviverMale");
        if (p != null) oyuncu = p.transform;
    }

    void Update()
    {
        if (controller == null) return;

        // "Move called on inactive controller" hatasının ilacı: CC kapanmışsa her karede aç
        if (!controller.enabled)
        {
            controller.enabled = true;
            if (debugKayit && !ccOnarildi) { ccOnarildi = true; Debug.Log("[Kopek] CharacterController kapalıydı — açıldı."); }
        }

        if (oyuncu == null)
        {
            OyuncuBul();
            return;
        }

        Vector3 d = oyuncu.position - transform.position;
        d.y = 0f;
        float mesafe = d.magnitude;

        if (mesafe > isinlanmaMesafesi)
        {
            // Oyuncunun arkasına ışınla (kafa karıştırmasın diye görüş dışına değil arkasına)
            Vector3 arka = oyuncu.position - oyuncu.forward * 2f;
            arka.y = transform.position.y;
            transform.position = arka;
            return;
        }

        Vector3 hareket = Vector3.zero;
        if (mesafe > durmaMesafesi)
        {
            float hiz = mesafe > gallopEsigi ? kosmaHizi : yurumeHizi;
            hareket = d.normalized * hiz;
            Yonel(oyuncu.position);
        }

        if (controller.isGrounded) dikeyHiz = -1f;
        else dikeyHiz += -20f * Time.deltaTime;
        controller.Move((hareket + Vector3.up * dikeyHiz) * Time.deltaTime);

        if (animator) animator.SetFloat("Speed", hareket.magnitude > 0.01f ? hareket.magnitude : 0f);
    }

    void Yonel(Vector3 hedef)
    {
        Vector3 yon = hedef - transform.position;
        yon.y = 0f;
        if (yon.sqrMagnitude < 0.001f) return;
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(yon), donusHizi * Time.deltaTime);
    }
}
