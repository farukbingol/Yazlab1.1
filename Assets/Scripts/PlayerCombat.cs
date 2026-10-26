using System.Collections;
using UnityEngine;

// Sol tık / F ile yumruk. Hasar, yumruk animasyonunun vuruş anına denk gelir
// (vurulmaGecikmesi); isabet alınca kısa zaman donması (hit-stop) vuruş hissi verir.
// debugKayit: ilk birkaç yumruğu Console'a yazar (zincirin nerede kırıldığını görmek için).
public class PlayerCombat : MonoBehaviour
{
    public float menzil = 3.2f;      // karakterler dev (4.2 birim) — görsel temasa uygun geniş
    public float aciDerece = 90f;
    public float vurusBeklemesi = 0.55f;
    [Tooltip("Yumruktan hasara kadar geçen süre — animasyonun vuruş anına denk gelmeli")]
    public float vurulmaGecikmesi = 0.22f;
    [Tooltip("İsabet alınca oyundaki kısa yavaşlatma (0 = kapalı)")]
    public float hitStopSuresi = 0.06f;
    public int hasar = 1; // kurt 2 canlı → 2 yumruk
    [Tooltip("Baltasız temel hasar — her oyun başında hasar buna döner")]
    public int tabanHasar = 1;
    [Tooltip("Balta kuşanınca hasarın katlanacağı çarpan")]
    public int baltaHasarCarpani = 2;
    public bool debugKayit = true;

    Animator animator;
    PlayerHealth can;
    float sonrakiVurus;
    int yumrukSayisi;
    bool vuruyor; // yumruğun vuruş anına kadar yeni yumruk başlamasın

    void Start()
    {
        animator = GetComponentInChildren<Animator>();
        can = GetComponent<PlayerHealth>();
        hasar = tabanHasar; // sahneye kaydedilmiş eski değeri değil, temel hasarı kullan
    }

    // AxePickup çağırır: balta eldeyken hasar çarpana düşer (idempotent)
    public void BaltaKusandi()
    {
        hasar = tabanHasar * baltaHasarCarpani;
        Debug.Log($"[Saldiri] balta kuşanıldı — hasar {hasar}");
    }

    void Update()
    {
        if (can != null && can.can <= 0) return; // ölüyken yumruk yok
        bool basti = Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.F);
        if (!basti || Time.time < sonrakiVurus || vuruyor) return;

        sonrakiVurus = Time.time + vurusBeklemesi;
        if (animator) animator.SetTrigger("Attack");
        vuruyor = true;
        StartCoroutine(Vur());
    }

    IEnumerator Vur()
    {
        yield return new WaitForSeconds(vurulmaGecikmesi); // yumruk hedefe varana kadar bekle
        vuruyor = false;

        int vurulan = 0;
        float enYakin = float.MaxValue;
        foreach (var dusman in Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
        {
            Vector3 d = dusman.transform.position - transform.position;
            d.y = 0f;
            float mesafe = d.magnitude;
            if (mesafe < enYakin) enYakin = mesafe;
            if (mesafe > menzil) continue;
            if (Vector3.Angle(transform.forward, d) > aciDerece) continue;
            dusman.HasarAl(d.normalized); // geri tepme yönü: oyuncudan düşmana
            vurulan++;
        }

        if (vurulan > 0 && hitStopSuresi > 0f) StartCoroutine(HitStop());

        if (debugKayit && yumrukSayisi < 5)
        {
            yumrukSayisi++;
            Debug.Log($"[Saldiri] yumruk #{yumrukSayisi} → {vurulan} düşman vuruldu (en yakın {enYakin:0.0} m, menzil {menzil:0.0} m, açı {aciDerece:0}°)");
        }
    }

    // Dünyayı bir anlığına dondurur — darbe hissi. Gerçek-zamanlı sayaçla
    // çalışır ki timescale 0.05'teyken kurtulması mümkün olsun.
    IEnumerator HitStop()
    {
        Time.timeScale = 0.05f;
        yield return new WaitForSecondsRealtime(hitStopSuresi);
        Time.timeScale = 1f;
    }

    // Menzil dairesini Scene görünümünde göster (oyuncuyu seçince)
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, menzil);
    }
}
