using UnityEngine;
using System;

public class AttackHitboxTrigger : MonoBehaviour
{
    public event Action<Collider2D> OnTriggerEnterEvent;

    private void OnTriggerEnter2D(Collider2D other)
    {
        OnTriggerEnterEvent?.Invoke(other);
    }
} 