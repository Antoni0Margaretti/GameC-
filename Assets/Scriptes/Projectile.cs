//using UnityEngine;

//public class Projectile : MonoBehaviour
//{
//    // ������ ����������� ��� �������� �������
//    private Vector2 direction;
//    // �������� ����� ������� (������������)
//    private float speed;

//    // ����� ������������� � �������� ������������ � ���������
//    public void Init(Vector2 dir, float projSpeed)
//    {
//        direction = dir.normalized;
//        speed = projSpeed;
//        // ��������, ���������� ������ ����� 5 ������, ����� �� �������� �����
//        Destroy(gameObject, 5f);
//    }

//    void Update()
//    {
//        // ���������� ������ � ������ ��������
//        transform.Translate(direction * speed * Time.deltaTime);
//    }

//    void OnTriggerEnter2D(Collider2D other)
//    {
//        // ���� ������ ���������� � �������
//        if (other.CompareTag("Player"))
//        {
//            // ���� ����� ��������� � ������ ������������ (��������, ��� "Invulnerable")
//            if (other.CompareTag("Invulnerable"))
//            {
//                // ������ ������ ���� � ������ �� ������
//                return;
//            }

//            // ����� �������� ������� � ������ ����� ��������� �����
//            if (playerHealth != null)
//            {
//                playerHealth.Die();
//            }
//            else
//            {
//                // ���� ��������� �� ������ � ����� ����� ���������� ������ ������.
//                Destroy(other.gameObject);
//            }
//        }
//        else if (!other.isTrigger)
//        {
//            // ���� ����������� � ���-�� ������ (��������, �� ������), ���������� ������
//            Destroy(gameObject);
//        }
//    }
//}
