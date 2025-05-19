using UnityEngine;
using System.Collections;

public class EnemyTeleportController : MonoBehaviour
{
    [Header("Teleport Settings")]
    public float teleportCooldown = 5f;
    public float teleportChargeTimeFar = 0.5f;
    public float teleportChargeTimeNear = 1.5f;
    public float teleportMinDistance = 6f;
    public float teleportSafeOffset = 1.2f;
    public LayerMask groundMask = default;

    protected Transform player;
    protected Rigidbody2D rb;
    protected bool isTeleporting = false;
    protected float lastTeleportTime = -10f;

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
                player = playerObj.transform;
        }
    }

    public virtual bool CanTeleport()
    {
        if (isTeleporting) return false;
        if (Time.time - lastTeleportTime < teleportCooldown) return false;
        if (player == null) return false;
        float distanceToPlayer = Vector2.Distance(transform.position, player.position);
        return distanceToPlayer > teleportMinDistance;
    }

    public virtual void TryTeleport()
    {
        if (CanTeleport())
        {
            float distanceToPlayer = Vector2.Distance(transform.position, player.position);
            float chargeTime = Mathf.Lerp(teleportChargeTimeNear, teleportChargeTimeFar, Mathf.InverseLerp(teleportMinDistance, 20f, distanceToPlayer));
            if (!IsGrounded())
                chargeTime *= 0.3f;
            StartCoroutine(TeleportToPlayerRoutine(chargeTime));
        }
    }

    protected virtual IEnumerator TeleportToPlayerRoutine(float chargeTime)
    {
        isTeleporting = true;
        // TODO: Визуальный/звуковой эффект подготовки

        yield return new WaitForSeconds(chargeTime);

        Vector2 offset = Vector2.right * teleportSafeOffset * Mathf.Sign(transform.position.x - player.position.x);
        Vector2 targetPos = (Vector2)player.position + offset;

        RaycastHit2D groundHit = Physics2D.Raycast(targetPos, Vector2.down, 2f, groundMask);
        if (groundHit.collider != null)
            targetPos.y = groundHit.point.y + 0.5f;

        transform.position = targetPos;

        // TODO: Визуальный/звуковой эффект появления

        lastTeleportTime = Time.time;
        isTeleporting = false;
    }

    protected virtual bool IsGrounded()
    {
        RaycastHit2D hit = Physics2D.Raycast(transform.position, Vector2.down, 1.1f, groundMask);
        return hit.collider != null;
    }
}