# Yazlab-I Proje-1 — Devir Notu (LLM NPC tarafı için)

Oyun tarafı hazır: karakter hareketi, düşmanlar (6 kurt + 1 şef), balta, köpek refakatçi.
Bu not, LLM NPC özelliğini ekleyecek kişi için hazırlanmıştır.

## Kurulum

1. Unity Hub → **Unity 6000.0.42f1** (aynı sürüm olmalı).
2. Projeyi aç (`Library/` yoksa Unity kendisi üretir, ilk açılış uzar).
3. `Assets/Scenes/GameScene.unity` sahnesini aç → **Play**. Sahne kurulumlu gelir (6 kurt, şef, köpek, oyuncu, UI kayıtlı) — menü çalıştırmaya gerek YOK.
4. Play (WASD hareket, koşu Shift, sol tık / F yumruk).

**Onarım menüleri (normalde gerekmez):** `Yazlab/Dusmanlari Kur` ve `Yazlab/Kopegi Kur`, düşmanlar/köpek sahnedense hiç çalıştırılmaz. Şunlar için vardır: düşmanlar kaybolursa, ayar değiştirilecekse (kurt sayısı, hız, can — `DusmanKur.cs` sabitlerinden) veya sahne bozulursa. Her çalıştırmada eskileri silip taze kurar → çalıştırınca **CTRL+S**.

Not: `Blends/` klasörlerindeki .blend dosyaları kaynak; Blender kurulu değilse import uyarısı verebilir, oyun FBX'lerle çalışır, uyarıyı yok sayabilirsin.

## Bölüm sahneları (orman sonrası) — `Yazlab/Bolumleri Kur`

Ormandan sonraki iki bölge tek menüyle kurulur (GameScene'e dokunmaz; her çalıştırmada iki sahneyi baştan yazar, sahne adlarını Build Settings'e otomatik yazar):

| Sahne | Ortam | Düşmanlar |
|---|---|---|
| `Bolum2_Gecit` | Toprak patikalı kayalık koridor (görünmez duvarlı), yoğun sis + soğuk ışık, kümeli orman, sonunda anıtsal kaya kapı + ateşler | 8 kurt (3 can, 10 hasar, algı 20, hız 3.8) |
| `Bolum3_Koy` | Golden-hour ışıklı köy: toprak patika + meydan, çit halkası, kümeli orman, merkez ateş çemberi (kütük oturak + meşaleler), şef avlusu | 4 muhafız kurt + **Kabile Reisi** (12 can, 20 hasar; ölünce balta + "OYUN TAMAMLANDI!" mesajı) |

- Her iki sahnede oyuncu + köpek + UI hazır gelir; oyuncu (0, 0, 2)'de doğar, -z yönüne bakar.
- **Sahne geçişi (LoadScene) senin kısmın:** sahneler Build Settings'te hazır; mesafe/collider tetikleyici tasarımı sana kalmış. İstersen `MesajPaneli.Goster` ile "geçiş" mesajı basabilirsin.
- Sahneler bağımsız — balta ormanda alındıysa diğer sahneye taşınmaz (her sahnede taban hasarla başlar).
- Menü çalışınca aktif sahne `Bolum3_Koy` olur; ormana dönmek için `GameScene.unity` aç.

## Sistem özeti (scriptler `Assets/Scripts/` altında)

| Script | Ne yapar |
|---|---|
| `PlayerController` | WASD/Shift hareket, kamera takibi |
| `PlayerHealth` | can, HPText, ölüm → Die trigger → 3 sn sonra doğuş → Revive |
| `PlayerCombat` | yumruk: hasar vuruş anında (0.22 sn gecikme), hit-stop, balta çarpanı |
| `EnemyController` | kurt/şef AI: algı → kovala → vur; ölünce drop (şef: balta) + mesaj |
| `CompanionController` | köpek: oyuncuyu takip eder, uzakta dörtnala, 40 m'de ışınlanır |
| `AxePickup` | yerdeki balta: yaklaşınca alınır, ele takılır, hasarı 2'ye çıkarır |
| `MesajPaneli` | **üst ortadaki mesaj satırı — her yerden `MesajPaneli.Goster("...")` çağrılabilir (static)** |

## LLM NPC için önerilen bağlantı noktaları

1. **NPC konuşması:** Sahneye bir NPC koy (ör. SurviverMale klonu, `Dusman_` öneki KULLANMA — o öneki Dusmanlari Kur menüsü siliyor). Yakınlık kontrolü + `MesajPaneli.Goster(cevap)` en hızlı entegrasyon. Daha güzeli: küçük bir diyalog paneli (OyuncuUI canvas'ının altına Text + InputField).
2. **Oyun durumu LLM'e bağlamak için hazır veriler:**
   - Oyuncu canı: `FindObjectOfType<PlayerHealth>().can`
   - Balta alındı mı: oyuncudaki `PlayerCombat.hasar == 2` (balta kuşanıldı)
   - Düşman durumu: sahnede `FindObjectsByType<EnemyController>()` → `can`, `olu`
   - Mesaj gösterme: `MesajPaneli.Goster("...")`
3. **LLM çağrısı:** bir `LlmNpc.cs` yazıp coroutine ile `UnityWebRequest.Post` (JSON) kullan; ana thread'de yanıtı `MesajPaneli.Goster` ile bas. (API anahtarını kod içine gömme — Editor'de `EditorPrefs`/dış dosya daha güvenli; build öncesi sökülür.)

## Bilinen değerler / ayar yerleri

- Oyuncu: yürüme 4.5, koşu 12 (jog eşiği 7), boy 4.18 birim
- Kurt: hız 3.4, 2 can, 8 hasar · Şef: hız 3, 6 can, 15 hasar, ölünce balta + "Bölge temizlendi!"
- Köpek: yürüme 2.8, dörtnala 6.5, durma mesafesi 3 m
- `debugKayit` alanları Console log basar (gürültü istemiyorsan Inspector'dan kapat)

## Teslim öncesi (sunum) yapılacaklar

1. `Assets/Editor/` klasörünü sil (Yazlab menüsü gider, oyun etkilenmez)
2. Build Settings'e `GameScene.unity` ekle (şu an boş)
3. `ayarPaneli`/`debugKayit` işaretlerini kapat
