using UnityEngine;
using System.Collections;

public class RangedEnemyAI : MonoBehaviour
{
    private enum State { Pursuing, Aiming, Shooting, Reloading, Retreating }
    private State currentState = State.Pursuing;

    [Header("Detection & Visual Contact")]
    public float detectionRadius = 8f;
    public Transform designatedPoint;
    public LayerMask obstacleMask;

    [Header("Movement Settings")]
    public float moveSpeed = 2f;
    public float retreatSpeed = 1.5f;
    public float retreatDistance = 2f;

    [Header("Attack & Aiming Settings")]
    public float aimTime = 1f;
    public float fireRate = 0.5f;
    public int magazineSize = 5;
    public float reloadTime = 2f;
    public float lostSightReloadDelay = 1.5f;

    [Header("Projectiles")]
    public GameObject projectilePrefab;
    public Transform firePoint;
    public float projectileSpeed = 10f;

    [Header("Player Reference")]
    public Transform player;

    private int currentAmmo;
    private float lastShotTime;
    private float lastSightTime;

    void Start()
    {
        currentAmmo = magazineSize;
        currentState = State.Pursuing;
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
                player = playerObj.transform;
        }
    }

    void Update()
    {
        if (player == null)
            return;

        float horizontalDistance = Mathf.Abs(transform.position.x - player.position.x);

        if (horizontalDistance < retreatDistance)
            currentState = State.Retreating;
        else if (currentState == State.Retreating && horizontalDistance >= (retreatDistance + 0.5f))
            currentState = State.Pursuing;

        if (horizontalDistance > detectionRadius)
            return;

        bool hasVisualContact = CheckVisualContact();
        if (hasVisualContact)
            lastSightTime = Time.time;

        switch (currentState)
        {
            case State.Pursuing:
                ChasePlayer();
                if (hasVisualContact)
                    StartCoroutine(BeginAiming());
                if (!hasVisualContact && Time.time - lastSightTime > lostSightReloadDelay && currentAmmo < magazineSize)
                {
                    currentState = State.Reloading;
                    StartCoroutine(Reload());
                }
                break;

            case State.Aiming:
                break;

            case State.Shooting:
                if (hasVisualContact)
                {
                    if (Time.time - lastShotTime >= fireRate && currentAmmo > 0)
                    {
                        Shoot();
                        lastShotTime = Time.time;
                        currentAmmo--;
                        if (currentAmmo <= 0)
                        {
                            currentState = State.Reloading;
                            StartCoroutine(Reload());
                        }
                    }
                }
                else if (Time.time - lastSightTime > lostSightReloadDelay)
                {
                    currentState = State.Reloading;
                    StartCoroutine(Reload());
                }
                break;

            case State.Reloading:
                break;

            case State.Retreating:
                RetreatFromPlayer();
                if (hasVisualContact)
                {
                    if (Time.time - lastShotTime >= fireRate && currentAmmo > 0)
                    {
                        Shoot();
                        lastShotTime = Time.time;
                        currentAmmo--;
                        if (currentAmmo <= 0)
                        {
                            currentState = State.Reloading;
                            StartCoroutine(Reload());
                        }
                    }
                    else if (Time.time - lastSightTime > lostSightReloadDelay)
                    {
                        currentState = State.Reloading;
                        StartCoroutine(Reload());
                    }
                }
                break;
        }
    }

    void ChasePlayer()
    {
        Vector2 targetPos = new Vector2(player.position.x, transform.position.y);
        Vector2 newPosition = Vector2.MoveTowards(transform.position, targetPos, moveSpeed * Time.deltaTime);
        transform.position = newPosition;
    }

    void RetreatFromPlayer()
    {
        float direction = Mathf.Sign(transform.position.x - player.position.x);
        float newX = Mathf.MoveTowards(transform.position.x, transform.position.x + direction, retreatSpeed * Time.deltaTime);
        transform.position = new Vector2(newX, transform.position.y);
    }

    bool CheckVisualContact()
    {
        if (designatedPoint == null)
            designatedPoint = transform;
        Vector2 direction = (player.position - designatedPoint.position).normalized;
        float distance = Vector2.Distance(designatedPoint.position, player.position);
        RaycastHit2D hit = Physics2D.Raycast(designatedPoint.position, direction, distance, obstacleMask);
        return (hit.collider == null);
    }

    IEnumerator BeginAiming()
    {
        if (currentState == State.Aiming || currentState == State.Shooting)
            yield break;

        currentState = State.Aiming;
        yield return new WaitForSeconds(aimTime);
        if (CheckVisualContact())
            currentState = State.Shooting;
        else
            currentState = State.Pursuing;
    }

    void Shoot()
    {
        if (projectilePrefab != null && firePoint != null)
        {
            GameObject proj = Instantiate(projectilePrefab, firePoint.position, Quaternion.identity);
            Projectile projectileScript = proj.GetComponent<Projectile>();
            if (projectileScript != null)
            {
                Vector2 shootDirection = (player.position - firePoint.position).normalized;
                projectileScript.Init(shootDirection, projectileSpeed);
            }
        }
    }

    IEnumerator Reload()
    {
        yield return new WaitForSeconds(reloadTime);
        currentAmmo = magazineSize;
        float horizontalDistance = Mathf.Abs(transform.position.x - player.position.x);
        currentState = (horizontalDistance < retreatDistance) ? State.Retreating : State.Pursuing;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, retreatDistance);
    }
}