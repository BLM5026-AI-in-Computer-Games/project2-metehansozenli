using UnityEngine;

public interface IBotMover2D
{
    void SetDestination(Vector2 worldPos);
    void Stop();
    bool HasPath { get; }
    bool IsStuck { get; }
    float RemainingDistance { get; }
}
