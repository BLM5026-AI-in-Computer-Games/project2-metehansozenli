# AI_Test Scene Setup Guide

## ?? Genel Yapý

Scene adý: **AI_Test.unity**

---

## ??? GameObject Hiyerarþisi

```
Scene: AI_Test
??? Grid
?   ??? Tilemap_Ground (walkable zemin)
?   ??? Tilemap_Walls (Collider2D + CompositeCollider2D + Rigidbody2D(Static))
?
??? Managers
?   ??? Sectorizer (Sectorizer.cs)
?   ??? NoiseBus (NoiseBus.cs)
?   ??? Logger (Logger.cs)
?
??? Player
?   ??? PlayerController.cs
?   ??? NoiseEmitter.cs
?   ??? PlayerStatsMonitor.cs
?   ??? Rigidbody2D (Dynamic, Gravity=0)
?   ??? CircleCollider2D
?
??? Enemy
?   ??? EnemyController.cs
?   ??? Pathfinder.cs
?   ??? Perception.cs
?   ??? QLearner.cs
?   ??? ActionPlanner.cs
?   ??? SectorAgent.cs
?   ??? EnemyBrain.cs
?   ??? Rigidbody2D (Dynamic, Gravity=0)
?   ??? CircleCollider2D
?
??? UI
    ??? Canvas
        ??? HUD_Text (LearningHUD.cs)
```

---

## ?? Component Kurulumu

### 1?? **Managers GameObject**

#### Sectorizer
```
- Script: Sectorizer.cs
- Sectors: 6-8 adet SectorData
  - Her sektör için:
    - id: "A", "B", "C", "D", "E", "F"
    - bounds: Rect(x, y, width, height) - dünya koordinatlarý
    - portals: Vector2[] (1-3 portal)
    - anchors: Vector2[] (1-2 anchor)
    - sweepPoints: Vector2[] (2-4 sweep nokta)
- Draw Gizmos: TRUE
- Draw Points: TRUE
```

**Örnek Sektör Yapýsý** (6x3 grid):
```
Sektör A: bounds = Rect(-15, -10, 10, 20) - Sol üst oda
Sektör B: bounds = Rect(-5, 5, 10, 15) - Orta üst koridor
Sektör C: bounds = Rect(5, 5, 10, 15) - Sað üst oda
Sektör D: bounds = Rect(-15, -10, 10, 20) - Sol alt oda
Sektör E: bounds = Rect(-5, -10, 10, 10) - Orta merkez
Sektör F: bounds = Rect(5, -10, 10, 20) - Sað alt oda
```

#### NoiseBus
```
- Script: NoiseBus.cs
- Debug Mode: TRUE
```

#### Logger
```
- Script: Logger.cs
- Format: CSV
- File Name: "ai_test_log"
- Enable Logging: TRUE
- Enemy Brain: (drag Enemy GameObject)
- Q Learner: (auto-find)
```

---

### 2?? **Player GameObject**

```
Tag: "Player"
Layer: Player

Components:
- Transform: Position (0, 0, 0)
- Rigidbody2D:
  - Body Type: Dynamic
  - Gravity Scale: 0
  - Freeze Rotation: TRUE
- CircleCollider2D:
  - Radius: 0.5
- PlayerController.cs:
  - Walk Speed: 3
  - Run Speed: 6
  - Run Key: LeftShift
- NoiseEmitter.cs:
  - Run Noise Radius: 10
  - Run Noise Interval: 1.5
  - Manual Noise Key: K
  - Manual Noise Radius: 15
- PlayerStatsMonitor.cs:
  - Update Interval: 5
  - Light Zone Mask: (Layer: LightZone)
  - Debug Mode: TRUE
- SpriteRenderer: (Yeþil kare/circle)
```

---

### 3?? **Enemy GameObject**

```
Tag: "Enemy"
Layer: Enemy

Components:
- Transform: Position (5, 5, 0)
- Rigidbody2D:
  - Body Type: Dynamic
  - Gravity Scale: 0
  - Freeze Rotation: TRUE
- CircleCollider2D:
  - Radius: 0.5
- Pathfinder.cs:
  - Cell Size: 0.5
  - Arrive Radius: 0.35
  - Wall Mask: (Layer: Wall)
  - Grid Bounds: Rect(-15, -10, 30, 20)
  - Draw Path: TRUE
  - Draw Grid: FALSE
- EnemyController.cs:
  - Speed: 3
  - Arrive Radius: 0.4
  - Stuck Timeout: 3
- Perception.cs:
  - View Distance: 10
  - View Angle: 90
  - Ray Count: 7
  - Obstruction Mask: (Layer: Wall)
  - Hearing Range: 15
  - Sound Memory Time: 5
  - Player: (drag Player GameObject)
  - Draw Gizmos: TRUE
- QLearner.cs:
  - Alpha: 0.4
  - Gamma: 0.8
  - Epsilon Start: 0.30
  - Epsilon End: 0.05
  - Epsilon Decay Seconds: 120
  - Debug Mode: TRUE
- ActionPlanner.cs:
  - Sweep Pause Duration: 0.75
  - Ambush Wait Duration: 6
  - Action Lock Duration: 1.5
  - Debug Mode: TRUE
- SectorAgent.cs:
  - Investigate Rotation Time: 2
  - Debug Mode: TRUE
- EnemyBrain.cs:
  - Decision Interval: 0.3
  - Reward Approach: 0.10
  - Reward Fresh Clue: 0.15
  - Reward Ambush Hit: 0.20
  - Penalty Idle: -0.01
  - Penalty Repeat: -0.10
  - Reward Capture: 1.0
  - Learning Enabled: TRUE
  - Debug Mode: TRUE
- SpriteRenderer: (Kýrmýzý kare/circle)
```

---

### 4?? **UI - Canvas**

```
Canvas:
- Render Mode: Screen Space - Overlay

HUD_Text (GameObject):
- RectTransform:
  - Anchor: Top-Left
  - Position: (10, -10, 0)
  - Width: 400
  - Height: 300
- Text (UI.Text):
  - Font Size: 14
  - Color: White
  - Alignment: Left-Top
- LearningHUD.cs:
  - Status Text: (drag Text component)
  - Update Interval: 0.5
  - Enemy Brain: (auto-find)
  - Q Learner: (auto-find)
```

---

## ?? Tilemap Kurulumu

### Grid GameObject
```
- Grid.cs (Unity built-in)
- Cell Size: (1, 1, 1)
```

### Tilemap_Ground
```
- Tilemap.cs
- Tilemap Renderer.cs
- Walkable zemin tile'larý (yeþil)
```

### Tilemap_Walls
```
- Tilemap.cs
- Tilemap Renderer.cs
- Tilemap Collider 2D:
  - Used By Composite: TRUE
- Composite Collider 2D:
  - Geometry Type: Outlines
  - Generation Type: Synchronous
- Rigidbody2D:
  - Body Type: Static
- Layer: Wall
- Duvar tile'larý (gri)
```

**Örnek Harita Yapýsý**:
```
####################
#  A   |  B  |  C  #
#      |     |     #
#------+-----+-----#
#  D   |  E  |  F  #
#      |     |     #
####################
```

---

## ?? Test Senaryolarý

### Test 1: Görüþ
1. Player'ý A sektörüne koy
2. Enemy'yi D sektörüne koy
3. Player'ý hareket ettir ? Enemy görüþ konisinde olsun
4. **Beklenen**: Enemy, GoToLastSeen ile yaklaþmalý

### Test 2: Ses
1. Player F sektöründe
2. **K tuþu**na bas (manuel noise)
3. **Beklenen**: Enemy, GoToLastHeard ile F'ye yönelmeli

### Test 3: Sweep
1. Player'ý 30 sn gizle
2. **Beklenen**: SweepNearest3 action'ý seçilmeli

### Test 4: Learning ON/OFF
1. **L tuþu** ile learning ON/OFF toggle
2. HUD'da "Learning: ON/OFF" görünmeli
3. OFF iken Q-Table güncellemeli

---

## ?? Baþarý Kriterleri

? A* path takýlmadan çalýþýyor (stuck < %5)  
? Learning ON: 60 sn içinde WrongSector/min ?%30 düþüyor  
? Learning OFF: Ýyileþme yok  
? HUD canlý güncelliyor  
? Log dosyasý oluþuyor (persistentDataPath)  

---

## ?? Layer Setup

```
Layers:
- 0: Default
- 6: Player
- 7: Enemy
- 8: Wall
- 9: LightZone (opsiyonel)

Physics 2D Collision Matrix:
- Player ? Wall: TRUE
- Enemy ? Wall: TRUE
- Player ? Enemy: FALSE (veya TRUE yakalama için)
```

---

## ?? Oyunu Baþlat

1. Scene'i aç: **AI_Test.unity**
2. **Play** butonuna bas
3. Console'u izle (debug log'lar)
4. **WASD** ile hareket et
5. **Shift** ile koþ (noise emit)
6. **K** ile manuel noise
7. **L** ile learning toggle
8. HUD'da metrikleri izle
9. `Application.persistentDataPath` altýnda log dosyasý oluþmalý

---

## ?? Log Dosyasý Konumu

**Windows**: `C:\Users\[USER]\AppData\LocalLow\[CompanyName]\[ProjectName]\`  
**Mac**: `~/Library/Application Support/[CompanyName]/[ProjectName]/`  
**Linux**: `~/.config/unity3d/[CompanyName]/[ProjectName]/`

Dosya adý: `ai_test_log_[timestamp].csv`

---

## ?? Görsel Gizmos (Scene View)

**Sectorizer**:
- Renkli sektör kutularý (A-F)
- Kýrmýzý sphere: Portal
- Yeþil sphere: Anchor
- Mavi sphere: Sweep point

**Pathfinder**:
- Cyan çizgi: A* path
- Sarý sphere: Aktif waypoint

**Perception**:
- Sarý koni: Görüþ alaný
- Koyu sarý: Duvar engeli
- Yeþil çizgi: Player görünüyor
- Kýrmýzý sphere: Son görülen pozisyon
- Sarý sphere: Son duyulan pozisyon

---

## ?? Troubleshooting

**Sorun**: "NullReferenceException: Sectorizer.Instance"  
**Çözüm**: Managers/Sectorizer GameObject var mý kontrol et

**Sorun**: "Player not found"  
**Çözüm**: Player GameObject'in Tag'i "Player" olmalý

**Sorun**: "Path not found"  
**Çözüm**: Tilemap_Walls layer mask doðru ayarlandý mý?

**Sorun**: "Q-Table güncellenmiyor"  
**Çözüm**: Learning Enabled = TRUE ve L tuþu ile kontrol et

---

Tüm dosyalar oluþturuldu! ??
