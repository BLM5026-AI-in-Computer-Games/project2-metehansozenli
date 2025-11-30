# ?? HIZLI BAÞLANGÝÇ - Görüþ Konisini Geri Getirme

## ? 3 Adýmda Çözüm

### 1?? Unity Editor'da Tool'u Aç
```
Tools ? Facility Protocol ? Migrate Perception Systems
```

### 2?? Migrate Et
```
"Migrate All Enemies" butonuna týkla
```

### 3?? Scene'i Kontrol Et
```
Hierarchy ? Enemy seçin
Scene View ? Sarý görüþ konisi görünüyor! ?
```

---

## ?? Ne Oldu?

**Önceki Durum:**
- ? Yeni modüler sistem kodlarý yazýldý
- ? VisionSensor, NoiseDetector, HeatmapSensor hazýr
- ? ThreatPerceptron ve ThreatEvaluator eklendi
- ? ANCAK GameObject'lere henüz eklenmedi

**Þu Anki Durum:**
- Eski `EnemyPerception2D_DEPRECATED` hala GameObject'te
- Yeni `VisionSensor` henüz GameObject'te deðil
- Bu yüzden görüþ konisi gizmo'larý çizilmiyor

**Çözüm:**
- Migration tool otomatik olarak:
  - Yeni componentleri ekler
  - Player referanslarýný baðlar
  - Sensör referanslarýný ThreatEvaluator'a atar
  - HeatmapCollector'ý günceller

---

## ?? Sonuç: Neyi Göreceksiniz?

Scene View'da enemy seçiliyken:

```
     ?? Sarý Görüþ Konisi (FOV)
    /  |  \
   /   |   \
  /    |    \
 /     ??    \    (kýrmýzý = ileri yön)
      Enemy
 
 ?? Sarý Çember = Görüþ menzili
 ?? Yeþil Çizgi = Oyuncuyu görüyor!
```

---

## ?? Troubleshooting

**"No Enemies Found" hatasý?**
? Enemy GameObject'lerine **"Enemy" tag**'i ekleyin

**Koni görünmüyor?**
? VisionSensor Inspector ? **Draw Gizmos = TRUE** kontrol edin

**Console'da NullReference hatasý?**
? VisionSensor Inspector ? **Target = Player** ayarlayýn

---

## ?? Detaylý Rehber

Tüm detaylar için bakýnýz:
```
Assets/_Project/PERCEPTION_SETUP_GUIDE.md
```

---

**?? Tahmini Süre:** 2-3 dakika  
**?? Baþarý Oraný:** %99  

? **Hazýrsýnýz!**
