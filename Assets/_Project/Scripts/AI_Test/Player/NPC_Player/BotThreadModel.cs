using UnityEngine;

public class BotThreatModel : MonoBehaviour
{
    public Transform enemy;
    public LayerMask obstacleMask;

    [Header("Distances (meters)")]
    public float highThreatLOSRange = 7f;
    public float midThreatRange = 5f;

    [Header("Smoothing")]
    public float threatDecaySeconds = 2.0f;

    public enum ThreatLevel { Low, Mid, High }

    public ThreatLevel CurrentThreat { get; private set; }
    public bool EnemyLOS { get; private set; }
    public float EnemyDistance { get; private set; }

    float lastHighT;

    public void Tick(float dt)
    {
        if (!enemy)
        {
            CurrentThreat = ThreatLevel.Low;
            EnemyLOS = false;
            EnemyDistance = float.PositiveInfinity;
            return;
        }

        Vector2 p = transform.position;
        Vector2 e = enemy.position;
        EnemyDistance = Vector2.Distance(p, e);

        EnemyLOS = HasLOS(p, e);

        bool high = EnemyLOS && EnemyDistance <= highThreatLOSRange;
        bool mid = EnemyDistance <= midThreatRange;

        if (high) lastHighT = Time.time;

        // decay: if recently high, keep it high for a short time
        if (Time.time - lastHighT <= threatDecaySeconds && EnemyDistance < highThreatLOSRange + 1f)
        {
            CurrentThreat = ThreatLevel.High;
        }
        else if (mid)
        {
            CurrentThreat = ThreatLevel.Mid;
        }
        else
        {
            CurrentThreat = ThreatLevel.Low;
        }
    }

    bool HasLOS(Vector2 from, Vector2 to)
    {
        Vector2 dir = (to - from);
        float dist = dir.magnitude;
        if (dist < 1e-4f) return true;
        dir /= dist;

        var hit = Physics2D.Raycast(from, dir, dist, obstacleMask);
        return hit.collider == null;
    }
}
