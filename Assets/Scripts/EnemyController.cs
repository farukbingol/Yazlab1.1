using UnityEngine;


[RequireComponent(typeof(CharacterController))]
public class EnemyController : MonoBehaviour
{
    public float algilamaMesafesi = 14f;
    public float hiz = 3.2f;              // oyuncu (yürüme 4.5) her zaman kaçabilir
    public float saldiriMesafesi = 2.4f;  // dev karakterlerde görsel temasa yakın
    public float saldiriBeklemesi = 1.6f; // yumruk arası süre
    public int hasar = 10;
    public int can = 3;                   // oyuncunun kaç yumruğu
    public float donusHizi = 8f;
    public bool debugKayit = true;
    [Tooltip("Hasar alınca geri itilme hızı")]
    public float tepetmeGucu = 6f;
    [Tooltip("Hasar alınca hareketin kısa donma süresi (darbe hissi)")]
    public float donmaSuresi = 0.12f;
    [Tooltip("Ölünce yere düşecek eşya (prefab) — örn. balta")]
    public GameObject olumDrop;
    [Tooltip("Ölünce üst ortada görünecek mesaj")]
    public string olumMesaji;

    const float YuvayaDonmeUzakligi = 40f; // oyuncu yuva+bu kadar uzaklaşınca takibi bırakır

    Transform oyuncu;
    PlayerHealth oyuncuCan;
    CharacterController controller;
    Animator animator;
    Vector3 yuva;
    Vector3 tepetmeHizi;
    float sonrakiSaldiri;
    float sonrakiArama;
    float donmaBitis;
    float dikeyHiz;
    bool olu;
    bool kovaliyordu;
    int vurusSayisi;

    void Start()
    {
        yuva = transform.position;
        controller = GetComponent<CharacterController>();
        animator = GetComponentInChildren<Animator>();
        OyuncuBul();
    }

    void OyuncuBul()
    {
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p == null) p = GameObject.Find("SurviverMale");
        if (p == null)
        {
            var ph = Object.FindAnyObjectByType<PlayerHealth>(FindObjectsInactive.Exclude);
            if (ph != null) p = ph.gameObject;
        }
        if (p != null)
        {
            oyuncu = p.transform;
            oyuncuCan = p.GetComponent<PlayerHealth>();
        }
    }

    void Update()
    {
        if (olu) return;

        if (oyuncu == null)
        {
           
            if (Time.time >= sonrakiArama)
            {
                sonrakiArama = Time.time + 1f;
                OyuncuBul();
            }
            return;
        }

        float mesafe = Vector3.Distance(transform.position, oyuncu.position);
        float yuvadanUzak = Vector3.Distance(transform.position, yuva);
        bool takipte = mesafe <= algilamaMesafesi && yuvadanUzak <= YuvayaDonmeUzakligi;

        Vector3 hareket = Vector3.zero;

        if (Time.time < donmaBitis)
        {
            hareket = tepetmeHizi; // darbe anı: sadece geri tepme işler
        }
        else if (mesafe <= saldiriMesafesi)
        {
            if (kovaliyordu && debugKayit && vurusSayisi < 3)
                Debug.Log($"[{name}] oyuncuya yetişti ({mesafe:0.0} m) — yumrukluyor");
            kovaliyordu = false;

            Yonel(oyuncu.position);
            if (Time.time >= sonrakiSaldiri)
            {
                sonrakiSaldiri = Time.time + saldiriBeklemesi;
                if (oyuncuCan == null) oyuncuCan = oyuncu.GetComponent<PlayerHealth>();
                if (animator) animator.SetTrigger("Attack");
                if (oyuncuCan != null)
                {
                    oyuncuCan.HasarAl(hasar);
                    if (debugKayit && vurusSayisi < 3) { vurusSayisi++; Debug.Log($"[{name}] VURDU (-{hasar})"); }
                }
            }
        }
        else if (takipte)
        {
            if (!kovaliyordu && debugKayit && vurusSayisi < 3)
                Debug.Log($"[{name}] kovalamaya başladı ({mesafe:0.0} m)");
            kovaliyordu = true;

            Yonel(oyuncu.position);
            hareket = (oyuncu.position - transform.position).normalized * hiz;
        }
        else if (yuvadanUzak > 0.5f)
        {
            kovaliyordu = false;
            Yonel(yuva);
            hareket = (yuva - transform.position).normalized * hiz;
        }

        
        if (controller.isGrounded) dikeyHiz = -1f;
        else dikeyHiz += -20f * Time.deltaTime;

        controller.Move((hareket + Vector3.up * dikeyHiz) * Time.deltaTime);
        tepetmeHizi = Vector3.Lerp(tepetmeHizi, Vector3.zero, 12f * Time.deltaTime);

        if (animator) animator.SetFloat("Speed",
            (Time.time < donmaBitis || hareket.magnitude <= 0.01f) ? 0f : hareket.magnitude);
    }

    
    public void HasarAl()
    {
        if (olu) return;
        can--;
        if (debugKayit) Debug.Log($"[{name}] can: {can}");
        if (can <= 0) Olu();
    }

    // Oyuncunun yumruğu isabet aldı: can + geri tepme + kısa donma + sarsılma animasyonu
    public void HasarAl(Vector3 vuranYon)
    {
        if (olu) return;
        HasarAl();
        if (can <= 0) return; // öldüyse ölüm animasyonunu bozmayalım

        vuranYon.y = 0f;
        if (vuranYon.sqrMagnitude > 0.001f) tepetmeHizi = vuranYon.normalized * tepetmeGucu;
        donmaBitis = Time.time + donmaSuresi;
        if (animator && ParamVar("Hit")) animator.SetTrigger("Hit");
    }

    bool ParamVar(string ad)
    {
        if (animator == null) return false;
        foreach (var p in animator.parameters)
            if (p.name == ad) return true;
        return false;
    }

    void Olu()
    {
        olu = true;
        Debug.Log($"[{name}] öldü");
        if (animator) animator.SetTrigger("Die");
        if (olumDrop != null)
        {
            var esya = Instantiate(olumDrop, transform.position + Vector3.up * 0.2f,
                Quaternion.Euler(0f, Random.value * 360f, 0f));
            if (esya.GetComponent<AxePickup>() == null) esya.AddComponent<AxePickup>(); // çift bileşen olmasın
        }
        if (!string.IsNullOrEmpty(olumMesaji)) MesajPaneli.Goster(olumMesaji);
        var cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        StartCoroutine(OlduktenSonraGomul());
    }

   
    System.Collections.IEnumerator OlduktenSonraGomul()
    {
        yield return new WaitForSeconds(3f);
        float t = 0f;
        Vector3 bas = transform.position;
        while (t < 1.5f)
        {
            t += Time.deltaTime;
            transform.position = bas + Vector3.down * (t * 1.2f);
            yield return null;
        }
        Destroy(gameObject);
    }

    void Yonel(Vector3 hedef)
    {
        Vector3 yon = hedef - transform.position;
        yon.y = 0f;
        if (yon.sqrMagnitude < 0.001f) return;
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(yon), donusHizi * Time.deltaTime);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, algilamaMesafesi);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, saldiriMesafesi);
    }
}
