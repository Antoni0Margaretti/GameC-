//using UnityEngine;

//public class Projectile : MonoBehaviour
//{
//    // Вектор направления для движения снаряда
//    private Vector2 direction;
//    // Скорость полёта снаряда (регулируемая)
//    private float speed;

//    // Метод инициализации с заданным направлением и скоростью
//    public void Init(Vector2 dir, float projSpeed)
//    {
//        direction = dir.normalized;
//        speed = projSpeed;
//        // Например, уничтожаем снаряд через 5 секунд, чтобы не засорять сцену
//        Destroy(gameObject, 5f);
//    }

//    void Update()
//    {
//        // Перемещаем снаряд с учетом скорости
//        transform.Translate(direction * speed * Time.deltaTime);
//    }

//    void OnTriggerEnter2D(Collider2D other)
//    {
//        // Если снаряд столкнулся с игроком
//        if (other.CompareTag("Player"))
//        {
//            // Если игрок находится в режиме неуязвимости (например, тег "Invulnerable")
//            if (other.CompareTag("Invulnerable"))
//            {
//                // Прошел сквозь него – ничего не делаем
//                return;
//            }

//            // Иначе пытаемся вызвать у игрока метод получения урона
//            if (playerHealth != null)
//            {
//                playerHealth.Die();
//            }
//            else
//            {
//                // Если компонент не найден – можно сразу уничтожить объект игрока.
//                Destroy(other.gameObject);
//            }
//        }
//        else if (!other.isTrigger)
//        {
//            // Если столкнулись с чем-то другим (например, со стеной), уничтожаем снаряд
//            Destroy(gameObject);
//        }
//    }
//}
