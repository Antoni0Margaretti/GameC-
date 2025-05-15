//using UnityEngine;
//using System.Collections;

//public class RangedEnemyAI : MonoBehaviour
//{
//    // Состояния противника
//    private enum State { Pursuing, Aiming, Shooting, Reloading, Retreating }
//    private State currentState = State.Pursuing;

//    [Header("Detection & Visual Contact")]
//    [Tooltip("Максимальное расстояние обнаружения игрока.")]
//    public float detectionRadius = 8f;
//    [Tooltip("Точка «глаз» противника для проверки визуального контакта.")]
//    public Transform designatedPoint;
//    [Tooltip("Маска слоёв, которая учитывается при проверке препятствий (например, стены).")]
//    public LayerMask obstacleMask;

//    [Header("Movement Settings")]
//    [Tooltip("Скорость преследования (движение по оси X).")]
//    public float moveSpeed = 2f;
//    [Tooltip("Скорость отступления (движение по оси X).")]
//    public float retreatSpeed = 1.5f;
//    [Tooltip("Если горизонтальное расстояние до игрока меньше этого значения, начинается отступление.")]
//    public float retreatDistance = 2f;

//    [Header("Attack & Aiming Settings")]
//    [Tooltip("Время прицеливания перед началом стрельбы (в секундах).")]
//    public float aimTime = 1f;
//    [Tooltip("Интервал между выстрелами (в секундах).")]
//    public float fireRate = 0.5f;
//    [Tooltip("Количество выстрелов (патронов) в одном цикле.")]
//    public int magazineSize = 5;
//    [Tooltip("Время перезарядки (в секундах) после опустошения магазина или превышения времени без контакта.")]
//    public float reloadTime = 2f;
//    [Tooltip("Если визуальный контакт с игроком отсутствует дольше этого времени, начинается перезарядка.")]
//    public float lostSightReloadDelay = 1.5f;

//    [Header("Projectiles")]
//    [Tooltip("Префаб снаряда, который будет создаваться при стрельбе.")]
//    public GameObject projectilePrefab;
//    [Tooltip("Точка, откуда вылетают снаряды.")]
//    public Transform firePoint;
//    [Tooltip("Скорость полёта снаряда")]
//    public float projectileSpeed = 10f;  // Регулируемая скорость полёта снаряда

//    [Header("Player Reference")]
//    [Tooltip("Ссылка на игрока (если не задана, ищется объект с тегом 'Player').")]
//    public Transform player;

//    // Внутренние переменные
//    private int currentAmmo;
//    private float lastShotTime;
//    private float lastSightTime; // время последнего установления визуального контакта

//    void Start()
//    {
//        currentAmmo = magazineSize;
//        currentState = State.Pursuing;
//        if (player == null)
//        {
//            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
//            if (playerObj != null)
//                player = playerObj.transform;
//        }
//    }

//    void Update()
//    {
//        if (player == null)
//            return;

//        // Рассчитываем горизонтальное расстояние (по оси X) между противником и игроком
//        float horizontalDistance = Mathf.Abs(transform.position.x - player.position.x);

//        // Если игрок находится слишком близко, переходим в состояние отступления
//        if (horizontalDistance < retreatDistance)
//        {
//            currentState = State.Retreating;
//        }
//        else
//        {
//            // Если противник ранее отступал, но теперь игрок находится вне опасной зоны, переходим к преследованию
//            if (currentState == State.Retreating && horizontalDistance >= (retreatDistance + 0.5f))
//            {
//                currentState = State.Pursuing;
//            }
//        }

//        // Если игрок находится вне зоны обнаружения (по всей дистанции), можно оставить противника в состоянии покоя
//        if (horizontalDistance > detectionRadius)
//        {
//            // Можно выставлять idle-анимацию здесь, при желании:
//            return;
//        }

//        // Проверяем визуальный контакт (линия обзора) с игроком
//        bool hasVisualContact = CheckVisualContact();
//        if (hasVisualContact)
//        {
//            lastSightTime = Time.time;
//        }

//        // Обрабатываем поведение в зависимости от текущего состояния
//        switch (currentState)
//        {
//            case State.Pursuing:
//                ChasePlayer();
//                if (hasVisualContact)
//                {
//                    // При установлении контакта начинаем прицеливание
//                    StartCoroutine(BeginAiming());
//                }
//                // Если визуальный контакт отсутствует дольше указанного времени и магазин частично пуст,
//                // переходим в режим перезарядки
//                if (!hasVisualContact && Time.time - lastSightTime > lostSightReloadDelay && currentAmmo < magazineSize)
//                {
//                    currentState = State.Reloading;
//                    StartCoroutine(Reload());
//                }
//                break;

//            case State.Aiming:
//                // Состояние прицеливания обрабатывается в корутине BeginAiming() – здесь ничего не делается
//                break;

//            case State.Shooting:
//                if (hasVisualContact)
//                {
//                    if (Time.time - lastShotTime >= fireRate && currentAmmo > 0)
//                    {
//                        Shoot();
//                        lastShotTime = Time.time;
//                        currentAmmo--;
//                        if (currentAmmo <= 0)
//                        {
//                            currentState = State.Reloading;
//                            StartCoroutine(Reload());
//                        }
//                    }
//                }
//                else
//                {
//                    if (Time.time - lastSightTime > lostSightReloadDelay)
//                    {
//                        currentState = State.Reloading;
//                        StartCoroutine(Reload());
//                    }
//                }
//                break;

//            case State.Reloading:
//                // Перезарядка запускается через корутину Reload(), здесь ничего делать не нужно
//                break;

//            case State.Retreating:
//                RetreatFromPlayer();
//                // Даже в состоянии отступления, если визуальный контакт установлен,
//                // противник продолжает стрелять
//                if (hasVisualContact)
//                {
//                    if (Time.time - lastShotTime >= fireRate && currentAmmo > 0)
//                    {
//                        Shoot();
//                        lastShotTime = Time.time;
//                        currentAmmo--;
//                        if (currentAmmo <= 0)
//                        {
//                            currentState = State.Reloading;
//                            StartCoroutine(Reload());
//                        }
//                    }
//                    else if (Time.time - lastSightTime > lostSightReloadDelay)
//                    {
//                        currentState = State.Reloading;
//                        StartCoroutine(Reload());
//                    }
//                }
//                break;
//        }
//    }

//    /// <summary>
//    /// Движение к игроку – только по горизонтали.
//    /// Целевая позиция определяется по X-координате игрока, а Y остаётся текущим.
//    /// </summary>
//    void ChasePlayer()
//    {
//        Vector2 targetPos = new Vector2(player.position.x, transform.position.y);
//        Vector2 newPosition = Vector2.MoveTowards(transform.position, targetPos, moveSpeed * Time.deltaTime);
//        transform.position = newPosition;
//    }

//    /// <summary>
//    /// Отступление от игрока – движение происходит только по горизонтали.
//    /// Противник двигается в сторону, противоположную позиции игрока по оси X.
//    /// </summary>
//    void RetreatFromPlayer()
//    {
//        float direction = Mathf.Sign(transform.position.x - player.position.x); // +1 если враг справа от игрока, -1 если слева
//        float newX = Mathf.MoveTowards(transform.position.x, transform.position.x + direction, retreatSpeed * Time.deltaTime);
//        transform.position = new Vector2(newX, transform.position.y);
//    }

//    /// <summary>
//    /// Проверка визуального контакта. Выполняется Raycast от designatedPoint к позиции игрока.
//    /// Если на пути нет препятствий, контакт считается установленным.
//    /// </summary>
//    /// <returns>true, если препятствия не обнаружены, иначе false.</returns>
//    bool CheckVisualContact()
//    {
//        if (designatedPoint == null)
//            designatedPoint = transform; // Если точка не задана, используем позицию врага
//        Vector2 direction = (player.position - designatedPoint.position).normalized;
//        float distance = Vector2.Distance(designatedPoint.position, player.position);
//        RaycastHit2D hit = Physics2D.Raycast(designatedPoint.position, direction, distance, obstacleMask);
//        return (hit.collider == null);
//    }

//    /// <summary>
//    /// Корутина прицеливания – после задержки, если визуальный контакт всё ещё установлен,
//    /// переход в режим стрельбы. Каждый раз, когда контакт восстанавливается, процесс начинается заново.
//    /// </summary>
//    IEnumerator BeginAiming()
//    {
//        // Если уже прицеливаемся или стреляем, не запускаем повторно
//        if (currentState == State.Aiming || currentState == State.Shooting)
//            yield break;

//        currentState = State.Aiming;
//        yield return new WaitForSeconds(aimTime);
//        if (CheckVisualContact())
//            currentState = State.Shooting;
//        else
//            currentState = State.Pursuing;
//    }

//    /// <summary>
//    /// Метод стрельбы: создаёт снаряд (Projectile), устанавливает направление движения снаряда в сторону игрока.
//    /// </summary>
//    void Shoot()
//    {
//        if (projectilePrefab != null && firePoint != null)
//        {
//            GameObject proj = Instantiate(projectilePrefab, firePoint.position, Quaternion.identity);
//            Projectile projectileScript = proj.GetComponent<Projectile>();
//            if (projectileScript != null)
//            {
//                // Вычисляем направление к игроку
//                Vector2 shootDirection = (player.position - firePoint.position).normalized;
//                // Передаём направление и скорость полёта снаряда
//                projectileScript.Init(shootDirection, projectileSpeed);
//            }
//        }
//        // Здесь можно добавить звуковой эффект или анимацию стрельбы
//    }

//    /// <summary>
//    /// Корутина перезарядки. После ожидания reloadTime магазин восстанавливается.
//    /// По окончании перезарядки противник возвращается в состояние преследования или отступления,
//    /// в зависимости от текущей дистанции до игрока.
//    /// </summary>
//    IEnumerator Reload()
//    {
//        yield return new WaitForSeconds(reloadTime);
//        currentAmmo = magazineSize;
//        float horizontalDistance = Mathf.Abs(transform.position.x - player.position.x);
//        currentState = (horizontalDistance < retreatDistance) ? State.Retreating : State.Pursuing;
//    }

//    // Для удобства отладки: визуализация зон обнаружения и отступления в редакторе Unity
//    void OnDrawGizmosSelected()
//    {
//        // Зона обнаружения (синий)
//        Gizmos.color = Color.blue;
//        Gizmos.DrawWireSphere(transform.position, detectionRadius);

//        // Зона отступления (красный)
//        Gizmos.color = Color.red;
//        Gizmos.DrawWireSphere(transform.position, retreatDistance);
//    }
//}