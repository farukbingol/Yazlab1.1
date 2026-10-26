using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Oyuncu sağlığı: hasar alır, canı HPText'e yazar, ölünce kontrol kilitlenir ve
// birkaç saniye sonra başlangıç noktasında tam canla yeniden doğar.
public class PlayerHealth : MonoBehaviour
{
    public int maxCan = 100;
    public int can = 100;
    public float yenidenDogusSuresi = 3f;

    static readonly Vector3 BaslangicNoktasi = new Vector3(0f, 0.05f, 2f);

    PlayerController hareket;
    Animator animator;
    Text hpText;

    void Start()
    {
        can = maxCan;
        hareket = GetComponent<PlayerController>();
        animator = GetComponentInChildren<Animator>();
        var t = GameObject.Find("HPText");
        if (t != null) hpText = t.GetComponent<Text>();
        Guncelle();
    }

    public void HasarAl(int miktar)
    {
        if (can <= 0) return;
        can = Mathf.Max(0, can - miktar);
        Debug.Log($"[Oyuncu] hasar -{miktar} → can {can}");
        Guncelle();
        if (can == 0) StartCoroutine(OlVeYenidenDog());
    }

    IEnumerator OlVeYenidenDog()
    {
        if (hareket != null) hareket.enabled = false;
        Tetikle("Die"); // Death state'i oynar; animator açık kalır
        yield return new WaitForSeconds(yenidenDogusSuresi);

        transform.position = BaslangicNoktasi;
        can = maxCan;
        Guncelle();
        if (hareket != null) hareket.enabled = true;
        Tetikle("Revive");
    }

    // Controller'da parametre yoksa sessizce geç (Death/Die kurulmadan da çalışsın)
    void Tetikle(string ad)
    {
        if (animator == null) return;
        foreach (var p in animator.parameters)
            if (p.name == ad) { animator.SetTrigger(ad); return; }
    }

    void Guncelle()
    {
        if (hpText != null) hpText.text = "HP: " + can;
    }
}
