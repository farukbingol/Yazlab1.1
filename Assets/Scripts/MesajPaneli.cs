using UnityEngine;
using UnityEngine.UI;

// Üst ortadaki mesaj satırı. Her yerden MesajPaneli.Goster("...") çağrılabilir;
// 4 saniye sonra kendiliğinden kaybolur. MsgText, OyuncuUI canvas'ının altında.
public class MesajPaneli : MonoBehaviour
{
    public static MesajPaneli Ornek;

    Text yazi;
    float kapanmaZamani;

    void Awake() { Ornek = this; }

    void Start()
    {
        var t = GameObject.Find("MsgText");
        if (t != null) yazi = t.GetComponent<Text>();
        if (yazi != null) yazi.text = "";
    }

    void Update()
    {
        if (yazi != null && kapanmaZamani > 0f && Time.time > kapanmaZamani)
        {
            yazi.text = "";
            kapanmaZamani = 0f;
        }
    }

    public static void Goster(string s)
    {
        if (Ornek == null) { Debug.Log("[Mesaj] " + s); return; }
        Ornek.GosterOzel(s);
    }

    void GosterOzel(string s)
    {
        if (yazi == null) { Debug.Log("[Mesaj] " + s); return; }
        yazi.text = s;
        kapanmaZamani = Time.time + 4f;
    }
}
