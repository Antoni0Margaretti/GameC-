using System.Collections.Generic;
using UnityEngine;

public class Projectile : MonoBehaviour
{
    private Vector2 direction;
    private float speed;
    public bool isReflected { get; private set; } = false;
    public bool isEnemyProjectile { get; private set; } = true; // true Ч враг, false Ч игрок
    private static LayerMask obstacleMask = 1 << 6; // Ќастройте под ваш проект

    public void Init(Vector2 dir, float projSpeed)
    {
        direction = dir.normalized;
        speed = projSpeed;
        isReflected = false;
        isEnemyProjectile = true;
        Destroy(gameObject, 5f);
    }

    /// <summary>
    /// ќтражение снар€да игроком или врагом.
    /// </summary>
    /// <param name="reflectOrigin">“очка отражени€ (позици€ парировани€)</param>
    /// <param name="toEnemy">true Ч летит во врага, false Ч летит в игрока</param>
    public void Reflect(Vector2 reflectOrigin, bool toEnemy)
    {
        isReflected = true;
        isEnemyProjectile = !toEnemy; // если toEnemy Ч игрок отразил, если !toEnemy Ч враг отразил обратно

        string targetTag = toEnemy ? "Enemy" : "Player";
        GameObject[] targets = GameObject.FindGameObjectsWithTag(targetTag);
        Vector2? bestTarget = null;
        float minDist = float.MaxValue;

        foreach (var obj in targets)
        {
            if (obj == null || obj == this.gameObject) continue;
            Collider2D col = obj.GetComponent<Collider2D>();
            if (col == null) continue;

            List<Vector2> points = new List<Vector2>();
            var bounds = col.bounds;
            points.Add(bounds.center);
            points.Add(new Vector2(bounds.min.x, bounds.min.y));
            points.Add(new Vector2(bounds.min.x, bounds.max.y));
            points.Add(new Vector2(bounds.max.x, bounds.min.y));
            points.Add(new Vector2(bounds.max.x, bounds.max.y));

            var poly = col as PolygonCollider2D;
            if (poly != null)
            {
                for (int p = 0; p < poly.pathCount; p++)
                {
                    var path = poly.GetPath(p);
                    foreach (var pt in path)
                        points.Add(col.transform.TransformPoint(pt));
                }
            }

            foreach (var pt in points)
            {
                Vector2 toPt = pt - reflectOrigin;
                float dist = toPt.magnitude;
                RaycastHit2D hit = Physics2D.Raycast(reflectOrigin, toPt.normalized, dist, obstacleMask);
                if (hit.collider == null)
                {
                    if (dist < minDist)
                    {
                        minDist = dist;
                        bestTarget = pt;
                    }
                }
            }
        }

        if (bestTarget.HasValue)
            direction = (bestTarget.Value - reflectOrigin).normalized;
        else
            direction = -direction;

        // ѕеремещаем снар€д в точку отражени€ (важно дл€ визуальной корректности)
        transform.position = reflectOrigin;
    }

    void Update()
    {
        transform.Translate(direction * speed * Time.deltaTime);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // ¬ражеский снар€д
        if (isEnemyProjectile)
        {
            if (other.CompareTag("Player"))
            {
                var playerController = other.GetComponent<PlayerController>();
                if (playerController != null && playerController.isInvulnerable)
                    return; // »гнорируем попадание, если игрок неу€звим
                PlayerDeath playerDeath = other.GetComponent<PlayerDeath>();
                if (playerDeath != null)
                    playerDeath.Die();
                else
                    Destroy(other.gameObject);

                // ќткрепить trail-эффект, если он есть, чтобы он доиграл анимацию
                foreach (Transform child in transform)
                {
                    var vfx = child.GetComponent<AutoDestroyVFX>();
                    if (vfx != null)
                    {
                        child.SetParent(null);
                        vfx.PlayAndAutoDestroy();
                    }
                }

                Destroy(gameObject);
            }
            // ѕарирование игроком
            else if (other.CompareTag("ParryHitbox"))
            {
                var parry = other.GetComponent<ParryHitbox>();
                if (parry != null && parry.IsParrying)
                {
                    Reflect(transform.position, true); // true Ч летит во врага
                }
            }
            else if (!other.isTrigger && !other.CompareTag("Enemy"))
            {
                // ќткрепить trail-эффект, если он есть, чтобы он доиграл анимацию
                foreach (Transform child in transform)
                {
                    var vfx = child.GetComponent<AutoDestroyVFX>();
                    if (vfx != null)
                    {
                        child.SetParent(null);
                        vfx.PlayAndAutoDestroy();
                    }
                }

                Destroy(gameObject);
            }
        }
        // ќтражЄнный игроком снар€д
        else
        {
            if (other.CompareTag("Enemy"))
            {
                var ai = other.GetComponent<MeleeEnemyAI>();
                if (ai != null)
                    ai.TakeDamage();

                // ќткрепить trail-эффект, если он есть, чтобы он доиграл анимацию
                foreach (Transform child in transform)
                {
                    var vfx = child.GetComponent<AutoDestroyVFX>();
                    if (vfx != null)
                    {
                        child.SetParent(null);
                        vfx.PlayAndAutoDestroy();
                    }
                }

                Destroy(gameObject);
            }
            // ѕарирование врагом ближнего бо€
            else if (other.CompareTag("EnemyParryHitbox"))
            {
                var parry = other.GetComponent<EnemyParryHitbox>();
                if (parry != null && parry.CanParryProjectile)
                {
                    Reflect(transform.position, false); // false Ч летит в игрока
                }
            }
            else if (!other.isTrigger && !other.CompareTag("Player"))
            {
                // ќткрепить trail-эффект, если он есть, чтобы он доиграл анимацию
                foreach (Transform child in transform)
                {
                    var vfx = child.GetComponent<AutoDestroyVFX>();
                    if (vfx != null)
                    {
                        child.SetParent(null);
                        vfx.PlayAndAutoDestroy();
                    }
                }

                Destroy(gameObject);
            }
        }
    }

    public void PlayAndAutoDestroy()
    {
        var ps = GetComponent<ParticleSystem>();
        if (ps != null) ps.Play();
        Destroy(gameObject, ps != null ? ps.main.duration : 1f);
    }
}

