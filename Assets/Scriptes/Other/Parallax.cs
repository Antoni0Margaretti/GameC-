using UnityEngine;

public class Parallax : MonoBehaviour
{
    // Коэффициент параллакса для этого слоя (0 означает неподвижный, 1 – движется вместе с камерой)
    [SerializeField]
    private float parallaxMultiplier = 0.5f;

    private Transform cam;
    private Vector3 previousCamPosition;

    void Start()
    {
        cam = Camera.main.transform;
        previousCamPosition = cam.position;
    }

    void LateUpdate()
    {
        // Определяем изменение позиции камеры с прошлого кадра
        Vector3 deltaMovement = cam.position - previousCamPosition;

        // Перемещаем слой пропорционально коэффициенту
        transform.position += new Vector3(deltaMovement.x * parallaxMultiplier, deltaMovement.y * parallaxMultiplier, 0);

        previousCamPosition = cam.position;
    }
}