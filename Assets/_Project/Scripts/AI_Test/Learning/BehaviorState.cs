namespace AITest.Learning
{
    /// <summary>
    /// Yüksek seviye davranýþ durumlarý
    /// Rapordan: "Devriye, Arama, Kovalamaca"
    /// </summary>
    public enum BehaviorState
    {
        Patrol = 0,   // Devriye: Pasif keþif (heatmap-based)
        Search = 1,   // Arama: Aktif avlanma (sweep high-prob areas)
        Chase = 2     // Kovalamaca: Direkt takip (go to last seen)
    }
}
