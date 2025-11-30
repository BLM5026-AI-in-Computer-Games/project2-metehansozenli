# Perception System - Troubleshooting Guide

## ?? Görüþ Konisi Görünmüyor?

### Problem Tanýmý
Yeni `VisionSensor` componentine geçtikten sonra Scene View'da sarý görüþ konisi görünmüyor.

---

## ? Çözüm Adýmlarý

### 1?? **Editor Tool ile Otomatik Migrate**

Unity Editor'da:
```
Tools ? Facility Protocol ? Migrate Perception Systems
```

**Bu tool:**
- ? Tüm Enemy GameObject'leri bulur
- ? Yeni componentleri ekler (VisionSensor, NoiseDetector, etc.)
- ? Referanslarý otomatik baðlar
- ? Scene durumunu raporlar

---

### 2?? **Manuel Component Kontrolü**

Enemy GameObject'inizde þu componentler olmalý:

```
Enemy GameObject:
??? Transform
??? Rigidbody2D
??? SpriteRenderer (optional)
??? EnemyPatrol2D (AI movement)
?
??? [PERCEPTION SENSORS]
??? VisionSensor              ? Görüþ konisi bu componentten!
??? NoiseDetector
??? HeatmapSensor
?
??? [INTELLIGENCE]
??? ThreatPerceptron
??? ThreatEvaluator
?
??? [SYSTEMS]
    ??? HeatmapCollector
```

---

### 3?? **VisionSensor Inspector Ayarlarý**

**Zorunlu Ayarlar:**
```
Target: Player Transform          (boþ býrakmayýn!)
Sight Range: 10                    (görüþ mesafesi)
Field Of View: 90                  (açý)
Ray Count: 36                      (koni detayý)
Obstruction Mask: Walls            (duvar layer'ý)

? Draw Gizmos: TRUE               ? ÖNEMLÝ!
? Draw Range Circle: TRUE
```

**Draw Gizmos kapalýysa koni görünmez!**

---

### 4?? **Scene View Gizmos Açýk mý?**

Scene View penceresinde sað üstte:
```
Gizmos ? (açýk olmalý)
```

Eðer kapalýysa veya VisionSensor gizmos'u disable edilmiþse görünmez.

---

### 5?? **Player Tag Kontrolü**

Player GameObject'in **tag'i "Player"** olmalý:

```
Inspector ? Tag ? Player
```

VisionSensor otomatik olarak Player tag'li GameObject'i arar.

---

### 6?? **Runtime Test**

Play mode'a girin ve kontrol edin:

? **Görüþ konisi oynarken görünüyor mu?**
- Evet ? Scene View Gizmos sorunu
- Hayýr ? Component veya Rigidbody2D eksik

? **Console'da hata var mý?**
```
[ThreatEvaluator] VisionSensor not found!  ? Component eksik
```

---

## ?? Sýk Karþýlaþýlan Hatalar

### **Hata 1: "NullReferenceException: VisionSensor.target"**
**Çözüm:**
```csharp
// VisionSensor Inspector:
Target = Player Transform  // Bu boþ olamaz!
```

---

### **Hata 2: "Koni çiziliyor ama oyuncuyu görmüyor"**
**Çözüm:**
```csharp
// VisionSensor Inspector:
Obstruction Mask = Walls  // Doðru layer seçili mi?
```

Raycast yanlýþ layer'a çarpýyor olabilir.

---

### **Hata 3: "Koni hiç hareket etmiyor"**
**Çözüm:**
```csharp
// VisionSensor Inspector:
Use Transform Up = true   // veya
Use Transform Up = false (hareket yönüne göre)
```

Enemy hareket etmiyorsa `transform.up` kullanýn.

---

## ?? Debug Checklist

```
[ ] VisionSensor component ekli mi?
[ ] Draw Gizmos = true mi?
[ ] Target = Player mi?
[ ] Scene View Gizmos açýk mý?
[ ] Player tag'i "Player" mi?
[ ] Rigidbody2D var mý?
[ ] ThreatEvaluator referanslarý baðlý mý?
[ ] Console'da hata yok mu?
```

---

## ?? Test Scene Kurulumu

Minimal bir test scene oluþturun:

```
Scene Hierarchy:
??? Camera
??? HeatmapSystem (empty GameObject + HeatmapSystem component)
??? Player (tag: Player)
?   ??? PlayerMovement2D
?   ??? PlayerNoiseEmitter
?   ??? SpriteRenderer
?
??? Enemy (tag: Enemy)
    ??? VisionSensor (draw gizmos ON)
    ??? NoiseDetector
    ??? HeatmapSensor
    ??? ThreatPerceptron
    ??? ThreatEvaluator
    ??? HeatmapCollector
    ??? EnemyPatrol2D
```

---

## ?? Quick Fix Script

Scene View'da GameObject seçili iken:

```csharp
// Inspector ? VisionSensor ? Right Click ? Reset
// veya
Tools ? Facility Protocol ? Migrate Perception Systems
```

---

## ?? Hala Çözülmedi mi?

1. **Console Log Kontrolü:**
```
Window ? General ? Console
```

2. **Component Inspector'ý Açýk Býrakýn:**
```
Play mode'da runtime deðerlerini izleyin
```

3. **Gizmos Debug:**
```csharp
VisionSensor.OnDrawGizmos() çaðrýlýyor mu?
// Debug.Log ekleyin
```

---

**Son Çare:** Scene'i sýfýrdan kurun ve migration tool'u kullanýn.
