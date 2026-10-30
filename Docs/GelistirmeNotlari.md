# Geliştirme Notları
Proje boyunca yapılan işlerin kısa kaydı — her satır ilgili değişikliğin özeti.

- Ch05 dokuları düzeltildi: kafa/gövde/zırh için diffuse ve normal eşleşmeleri doğru dosyalarla değiştirildi.

- Bolum3'teki Dusman_Reis, Brute.fbx ile değiştirildi; EnemyController değerleri (hasar, drop, ölüm mesajı) aynen korundu.

- Brute için FBX slot adlarına göre Mat_Brute_* materyalleri kuruldu (gövde, saç, pantolon, ayakkabı, bıyık, balta).

- Brute'un Humanoid avatarı bağlandı; DusmanAnimator klipleri retarget ile oturdu.

- BossHealthBar._boss referansı yeni EnemyController bileşenine yeniden bağlandı.

- Bolum2 duvarları Rampart_Wall parçalarıyla kale duvarına çevrildi; görünmez collider kutularına ve NavMesh'e dokunulmadı.

- Duvar parçaları bindirme yerine milimetrik esnetmeyle uç uca dizildi (mazgal çift çizmesin diye).

- Mat_Kale_Duvar, palet dokusuyla kalıcı materyal olarak Assets/Kale altına eklendi.

- EnvironmentSetup tekrar çalıştırılsa bile Bolum2 duvarlarının rampart kalması sağlandı (CliffWallBuilder yalnız Bolum3'te kullanılıyor).

- Oyuncu (SurviverMale), Dreyar.fbx ile değiştirildi; ölçülen boy (≈4.17 dünya birimi) yeni modele ölçeklendi.

- Dreyar FBX'i Humanoid rig'e çevrildi; PlayerAnimator.controller klipleri retarget ile oturdu.

- AxePickup'ın aradığı "RightHand" kemiği, Mixamo rig'inde "mixamorig:RightHand" olarak bulundu; balta bağlantısı çalışıyor.

- Mat_Dreyar materyali, FBX'ten çıkarılan diffuse + normal dokularla Assets/Oyuncu altına eklendi.

- CharacterController, yeni kök ölçeğinden yeniden türetildi (4 boy / 0.66 yarıçap / 2 merkez — CC transform ölçeğini takip etmez).

- PlayerController/PlayerCombat/PlayerHealth/PlayerInputReader/CompanionCommander bileşenleri serileştirilmiş alan kopyasıyla yeni modele taşındı.

- Oyuncu değişimi üç bölüm sahnesini de kapsayacak şekilde genişletildi (Bolum2_Gecit ve Bolum3_Koy'da da Dreyar kullanılıyor).

- Zaten Dreyar'a çevrilmiş sahneler atlanıyor; kurulum tekrar çalıştırılabilir (idempotent).

- Sahnedeki tüm serialized oyuncu referansları (kamera hedefi, HUD, düşmanların _playerTransform'ları) otomatik yeniden bağlandı.

- Kurulum araçları tek menü altında toplandı: Yazlab/Kurulum (Sef, Reis, Oyuncu, Kale Duvarı).

- Model import'larında Unity 6 uyumu: Humanoid animation type sabit cast ile ayarlanıyor (enum üyesi derlenmiyor).

- Sahne geçişlerinde oyuncu boyu/bileşenleri korunduğu için geçiş sonrası kontroller ve dengeler değişmedi.

