# ?? Perception System Setup Guide
**Facility Protocol - Görüþ Konisi Kurulum Rehberi**

---

## ?? Durum Özeti

Yeni modüler perception sistemi hazýr ve kod derleniyor! ?

Ancak **düþman GameObject'lerine yeni bileþenler henüz eklenmedi**, bu yüzden görüþ konisi gizmo'larý görünmüyor.

Bu rehber, otomatik migration aracýný kullanarak sistemi çalýþtýrmayý anlatýr.

---

## ?? Hýzlý Kurulum (5 Dakika)

### Adým 1: Unity Editor'ý Açýn
Projeyi Unity'de açtýysanýz devam edin.

---

### Adým 2: Migration Tool'u Çalýþtýrýn

Unity Editor'da üst menüden:

```
Tools ? Facility Protocol ? Migrate Perception Systems
```

Açýlan pencerede:

![Migration Tool](https://via.placeholder.com/400x300?text=Migration+Tool+Window)

1. **"Auto-Find Enemies"** checkbox'ý iþaretli olmalý (?)
2. **"Check Scene Status"** butonuna týklayarak mevcut durumu kontrol edin
3. **"Migrate All Enemies"** butonuna týklayýn

---

### Adým 3: Scene'i Kontrol Edin

Migration tamamlandýktan sonra Console'da þu mesajý görmelisiniz:

```
[Migration] Migrating: Enemy_1
  ? VisionSensor eklendi: Enemy_1
  ? NoiseDetector eklendi: Enemy_1
  ? HeatmapSensor eklendi: Enemy_1
  ? ThreatPerceptron eklendi: Enemy_1
  ? ThreatEvaluator eklendi: Enemy_1
  ? HeatmapCollector güncellendi: Enemy_1
```

---

### Adým 4: Scene View'da Görüþ Konisini Görmek

1. **Hierarchy** penceresinden bir **Enemy GameObject** seçin
2. **Scene View** penceresine geçin
3. **Sarý görüþ konisi** artýk görünüyor olmalý! ??

---

## ?? Manuel Kontrol (Opsiyonel)

Migration tool çalýþmadýysa veya manuel kontrol yapmak istiyorsanýz:

### Enemy GameObject Inspector'da Olmasý Gerekenler:

```
Enemy GameObject
??? Transform
??? Rigidbody2D (gravityScale = 0)
??? SpriteRenderer
??? EnemyMoveAStar2D          [PATHFINDING]
?
??? VisionSensor               [PERCEPTION] ? YENÝ!
??? NoiseDetector              [PERCEPTION] ? YENÝ!
??? HeatmapSensor              [PERCEPTION] ? YENÝ!
?
??? ThreatPerceptron           [INTELLIGENCE] ? YENÝ!
??? ThreatEvaluator            [INTELLIGENCE] ? YENÝ!
?
??? HeatmapCollector           [SYSTEMS]
```

---

## ?? VisionSensor Ayarlarý

Enemy GameObject'e **VisionSensor** eklendikten sonra Inspector'da:

### Zorunlu Ayarlar:

| Parametre | Deðer | Açýklama |
|-----------|-------|----------|
| **Target** | `Player Transform` | ?? BOÞ OLAMAZ! |
| **Sight Range** | `10` | Görüþ mesafesi (metre) |
| **Field Of View** | `90` | Görüþ açýsý (derece) |
| **Ray Count** | `36` | Koni detayý |
| **Obstruction Mask** | `Walls` | Duvar layer'ý |
| **Draw Gizmos** | `? TRUE` | **ÖNEMLÝ!** |
| **Draw Range Circle** | `? TRUE` | Menzil çemberi |
| **Use Transform Up** | `? TRUE` | Ýleri yön (düþman hareket etmiyorsa) |

---

## ?? Gizmo Renkleri

Scene View'da göreceðiniz renkler:

| Renk | Anlamý |
|------|--------|
| ?? **Sarý Koni** | Görüþ alaný (FOV) |
| ?? **Sarý Çember** | Görüþ menzili |
| ?? **Kýrmýzý Çizgi** | Ýleri yön (transform.up) |
| ?? **Yeþil Çizgi** | Oyuncuyu görüyor! |
| ?? **Turuncu Çizgi** | Ses algýlama |
| ?? **Mavi-Kýrmýzý** | Heatmap sensörü |

---

## ?? Sorun Giderme

### ? "No Enemies Found" Hatasý

**Çözüm:** Scene'deki düþman GameObject'lerine **"Enemy" tag**'i ekleyin:

```
1. Hierarchy'de Enemy GameObject'i seçin
2. Inspector ? Tag ? "Enemy"
```

Veya migration tool'da **"Auto-Find Enemies" kapatýp** manuel seçim yapýn.

---

### ? Görüþ Konisi Hala Görünmüyor

**Kontrol Listesi:**

- [ ] VisionSensor component ekli mi? (Inspector'da görünüyor mu?)
- [ ] **Draw Gizmos = TRUE** mi?
- [ ] Target = Player Transform mi?
- [ ] Scene View'da **Gizmos açýk** mý? (Scene View sað üst köþe)
- [ ] Player GameObject'in tag'i **"Player"** mi?
- [ ] Rigidbody2D var mý?

---

### ? "NullReferenceException: VisionSensor.target"

**Çözüm:**

```csharp
// VisionSensor Inspector:
Target = Player Transform  // Player'ý sürükleyip býrakýn
```

Player otomatik bulunamadýysa, Hierarchy'den Player GameObject'ini **Target** alanýna sürükleyin.

---

### ? Koni Çiziliyor Ama Oyuncuyu Görmüyor

**Çözüm:**

```csharp
// VisionSensor Inspector:
Obstruction Mask = Walls
```

Duvar Layer'ýný doðru seçtiðinizden emin olun. Project Settings ? Tags and Layers'da "Walls" layer'ý var mý?

---

## ?? Test Senaryosu

Migration sonrasý test etmek için:

1. **Play Mode**'a girin
2. **Scene View**'da Enemy'yi seçili tutun
3. **Player**'ý WASD ile hareket ettirin
4. Görüþ konisi **oyuncuya doðru döner**
5. Oyuncu koni içine girdiðinde **yeþil çizgi** belirir

---

## ?? Scene Status Raporu

Migration tool'da **"Check Scene Status"** butonuna týkladýðýnýzda þunu görmelisiniz:

```
=== SCENE STATUS REPORT ===

Total Enemies Found: 1

? With VisionSensor: 1/1
? With NoiseDetector: 1/1
? With HeatmapSensor: 1/1
? With ThreatEvaluator: 1/1

Player Found: YES
PlayerNoiseEmitter: YES

HeatmapSystem: YES
```

Eðer herhangi biri **0/1** ise, o component eksik demektir!

---

## ?? Eski Componentleri Kaldýrma (Opsiyonel)

Migration tool varsayýlan olarak eski DEPRECATED componentleri **kaldýrmaz** (güvenlik için).

Kaldýrmak isterseniz:

1. `PerceptionMigrationTool.cs` dosyasýný açýn
2. `MigrateEnemy()` fonksiyonunda þu satýrý bulun:

```csharp
// 7. Eski componentleri kaldýr (opsiyonel - güvenlik için yorum satýrýnda)
// RemoveObsoleteComponents(enemy);
```

3. Yorum iþaretini kaldýrýn:

```csharp
RemoveObsoleteComponents(enemy); // ETKIN
```

4. Migration tool'u tekrar çalýþtýrýn

---

## ? Baþarý Kontrolü

Her þey doðru kurulmuþsa:

- ? Scene View'da **sarý görüþ konisi** görünüyor
- ? Console'da **hata yok**
- ? Play mode'da **yeþil çizgi** (oyuncu görüldüðünde)
- ? Inspector'da **tüm referanslar baðlý**
- ? ThreatEvaluator **Threat Score** deðeri deðiþiyor (debug)

---

## ?? Hala Çalýþmýyor mu?

1. **Console**'u temizleyin (Clear) ve tekrar test edin
2. **Scene'i kaydedin** (Ctrl+S)
3. **Unity'yi yeniden baþlatýn**
4. Migration tool'u **tekrar çalýþtýrýn**

Hala sorun varsa, Console'daki **ilk hata mesajýný** kontrol edin!

---

## ?? Teknik Detaylar

### Yeni Mimari:

```
VisionSensor (raycast FOV)
    ?
NoiseDetector (mesafe + attenuation)
    ?
HeatmapSensor (grid query)
    ?
ThreatPerceptron (MLP: 5 input ? 12 hidden ? 1 output)
    ?
ThreatEvaluator (sensor aggregator + threat score)
    ?
RL Agent (PPO: observation ? action)
```

### Eski Sistem (DEPRECATED):

```
EnemyPerception2D ? VisionSensor
EnemyThreatModel ? ThreatEvaluator
Perception ? ThreatPerceptron
```

---

## ?? Sýradaki Adýmlar

Perception sistemi çalýþtýktan sonra:

1. ? **RL Agent entegrasyonu** (PPO decision-making)
2. ? **Behavior modes** (Patrol, Investigate, Hunt, CheckHotspot)
3. ? **Reward system** (capture, proximity, hotspot rewards)
4. ? **Heatmap decay** (chapter-based reset)

---

**Not:** Migration tool **non-destructive** çalýþýr - mevcut componentleri silmez, sadece yenilerini ekler ve referanslarý baðlar.

---

Hazýrlayan: AI Assistant  
Proje: Facility Protocol  
Tarih: {DateTime.Now}

?? **Ýyi eðlenceler ve baþarýlar!**
