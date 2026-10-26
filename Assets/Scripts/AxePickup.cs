using System.Collections;
using UnityEngine;

// Yerde dönen balta: oyuncu yaklaşınca alınır, sağ ele takılır.
// Yerleştirme otomatiktir (rot ayarı yok): yumruğun biraz önü-üstü, hafif eğik dünya pozu.
// Boy, ölçümle kalibre edilir (BoyuDuzelt — 3 karede gerçek renderer boyu hedefe çekilir).
// Panelde kalan: ölçek/boy-oranı sliderları, tanı satırı, Yere Birak testi, Logla.
// Balta kuşanınca oyuncunun hasarı baltaHasarCarpani katına çıkar.
public class AxePickup : MonoBehaviour
{
    public float toplamaMesafesi = 2f;
    public float donusHizi = 90f;
    public bool eleTaksin = true;

    [Header("El ayarı (otomatik; ince ayar için)")]
    [Tooltip("Balta uzunluğu = oyuncu boyu × bu oran")]
    public float hedefBoyOrani = 0.25f;
    [Tooltip("Boyut çarpanı — orana dokunmadan hızlı ince ayar")]
    public float olcekCarpani = 1f;
    [Tooltip("Ele takılınca sol üstte ayar panelini göster (değerler oturdu — kapalı)")]
    public bool ayarPaneli = false;
    public bool debugKayit = true;

    Transform oyuncu;
    bool alindi;
    float temelOlec = 1f; // otomatik kalibre edilen ölçek (olcekCarpani bunu çarpar)
    bool kalibre;

    void Start()
    {
        // Yere yarım gömülü kalmasın
        var p = transform.position;
        p.y += 0.3f;
        transform.position = p;
    }

    void Update()
    {
        if (alindi) return;
        transform.Rotate(0f, donusHizi * Time.deltaTime, 0f, Space.World);

        if (oyuncu == null)
        {
            var g = GameObject.FindGameObjectWithTag("Player");
            if (g == null) g = GameObject.Find("SurviverMale");
            if (g != null) oyuncu = g.transform;
            return;
        }

        if (Vector3.Distance(transform.position, oyuncu.position) <= toplamaMesafesi)
        {
            alindi = true;
            MesajPaneli.Goster("Balta alındı!");
            if (eleTaksin) EleTak(oyuncu.gameObject);
        }
    }

    // Oyuncunun tüm renderer'larından gerçek boyu ölçer
    static float BoyOlc(GameObject go)
    {
        float min = float.MaxValue, max = float.MinValue;
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            min = Mathf.Min(min, r.bounds.min.y);
            max = Mathf.Max(max, r.bounds.max.y);
        }
        return max > min ? max - min : 0f;
    }

    float BoyOlcOz() { return BoyOlc(gameObject); }

    // Balta için hedef dünya boyu: oyuncu boyu × oran × çarpan
    float HedefDunya()
    {
        float oyuncuBoy = 4.18f;
        if (oyuncu != null)
        {
            float b = BoyOlc(oyuncu.gameObject);
            if (b > 0.01f) oyuncuBoy = b;
        }
        return oyuncuBoy * hedefBoyOrani * olcekCarpani;
    }

    void EleTak(GameObject oyuncuGo)
    {
        // Parmak kemiklerini değil avuç kemiğini yakala: "mixamorig:RightHand" gibi
        Transform el = null;
        foreach (var t in oyuncuGo.GetComponentsInChildren<Transform>())
        {
            var ad = t.name.Replace("mixamorig:", "");
            if (ad == "RightHand") { el = t; break; }
        }

        if (el == null)
        {
            Debug.LogWarning("[Balta] Sağ el kemiği YOK — balta yerde kaldı. (Hierarchy'de Hatchet diye arat.)");
            MesajPaneli.Goster("Balta alındı ama el bulunamadı — yerde kaldı!");
            return;
        }

        float oyuncuBoy = BoyOlc(oyuncuGo);
        float elKayip = Mathf.Abs((el.lossyScale.x + el.lossyScale.y + el.lossyScale.z) / 3f);
        float dogalBoy = BoyOlcOz();
        if (debugKayit)
            Debug.Log($"[Balta] el={el.name} elKayipScale={elKayip:0.0000} baltaDogalBoy={dogalBoy:0.0000} oyuncuBoy={oyuncuBoy:0.00} hedefDunya={HedefDunya():0.00}");

        // Kemiğe bağla, pozu DÜNYA uzayında elle yaz — kemik eksenlerine güvenme.
        // Hedef: yumruğun içi = bilek + (parmak yönünde yarım el) + (gövde önüne az kaydırma)
        transform.SetParent(el, true);
        Vector3 ileri = oyuncuGo.transform.forward; ileri.y = 0f;
        if (ileri.sqrMagnitude < 0.001f) ileri = Vector3.forward;
        ileri.Normalize();
        transform.position = el.position + el.up * (oyuncuBoy * 0.05f) + ileri * (oyuncuBoy * 0.025f);
        transform.rotation = Quaternion.Euler(-30f, 0f, 8f); // dik konum, hafif eğik

        // Ölçek: gerçek ölçüme göre kalibrasyon halleder
        temelOlec = 1f;
        Uygula();
        kalibre = false;
        StartCoroutine(BoyuDuzelt(HedefDunya()));

        // Balta hasarı
        var pcb = oyuncuGo.GetComponent<PlayerCombat>();
        if (pcb != null) pcb.BaltaKusandi();

        MesajPaneli.Goster("Balta kuşanıldı!");
    }

    // Birkaç kare gerçek renderer boyunu ölçüp localScale'i hedefe çeker
    IEnumerator BoyuDuzelt(float hedefDunya)
    {
        for (int i = 0; i < 3; i++)
        {
            yield return null;
            float h = BoyOlcOz();
            if (h > 0.0001f)
            {
                float f = hedefDunya / h;
                if (!Mathf.Approximately(f, 1f))
                    transform.localScale = transform.localScale * f;
            }
        }
        temelOlec = Mathf.Max(0.0001f, transform.localScale.x / Mathf.Max(0.0001f, olcekCarpani));
        kalibre = true;
        if (debugKayit)
            Debug.Log($"[Balta] kalibre edildi: gerçek boy={BoyOlcOz():0.000} (hedef {hedefDunya:0.000}) localScale={transform.localScale.x:0.00} parent={(transform.parent != null ? transform.parent.name : "YOK")}");
    }

    // Ölçek ayarını uygular (panel her karede çağırır)
    void Uygula()
    {
        transform.localScale = Vector3.one * temelOlec * olcekCarpani;
    }

    // ─── Canlı ayar paneli ───
    void OnGUI()
    {
        if (!alindi || !eleTaksin || !ayarPaneli || transform.parent == null) return;

        float oncekiOran = hedefBoyOrani;
        float oncekiCarpani = olcekCarpani;

        GUI.Box(new Rect(10f, 60f, 260f, 300f), "Balta Ayari (el)");

        GUI.Label(new Rect(20f, 88f, 240f, 20f), $"Olcuk  x{olcekCarpani:0.00}");
        olcekCarpani = GUI.HorizontalSlider(new Rect(20f, 108f, 240f, 18f), olcekCarpani, 0.2f, 3f);

        GUI.Label(new Rect(20f, 132f, 240f, 20f), $"Boy orani  {hedefBoyOrani:0.00}");
        hedefBoyOrani = GUI.HorizontalSlider(new Rect(20f, 152f, 240f, 18f), hedefBoyOrani, 0.10f, 0.40f);

        if (kalibre && (oncekiOran != hedefBoyOrani || oncekiCarpani != olcekCarpani))
            StartCoroutine(BoyuDuzelt(HedefDunya()));

        // Tanı satırı — balta görünmüyorsa buradan sebep okunur
        GUI.Label(new Rect(20f, 178f, 240f, 40f),
            $"Parent: {(transform.parent != null ? transform.parent.name : "YOK")}\n" +
            $"Gercek boy: {BoyOlcOz():0.000}  localScale: {transform.localScale.x:0.00}{(kalibre ? "" : " (kalibre...)")}");

        if (GUI.Button(new Rect(20f, 218f, 115f, 24f), "Yere Birak"))
        {
            // Test: dünyaya bırak — görünüyorsa sorun kemik bağlantısındadır.
            // Oyuncu üstüne yürüyünce tekrar alınır (alindi sıfırlanır).
            transform.SetParent(null);
            var p = transform.position; p.y = 0.35f; transform.position = p;
            alindi = false;
            temelOlec = 1f;
        }
        if (GUI.Button(new Rect(145f, 218f, 115f, 24f), "Degerleri Logla"))
            Debug.Log($"[BaltaAYAR] hedefBoyOrani={hedefBoyOrani:0.00}f olcekCarpani={olcekCarpani:0.00}f");

        GUI.Label(new Rect(20f, 250f, 240f, 60f),
            "Balta otomatik yerlesir; olcugu sliderla duzelt.\n" +
            "'Yere Birak' sonrasi ustune yuruyunce tekrar eline gecer.");
    }
}
