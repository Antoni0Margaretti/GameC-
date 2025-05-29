using UnityEngine;

public class AutoDestroyVFX : MonoBehaviour
{
    private ParticleSystem ps;

    void Awake()
    {
        ps = GetComponent<ParticleSystem>();
    }

    void Start()
    {
        if (ps == null)
            ps = GetComponent<ParticleSystem>();
        if (ps != null)
            Destroy(gameObject, ps.main.duration);
        else
            Destroy(gameObject, 1f);
    }

    public void PlayAndAutoDestroy()
    {
        if (ps == null)
            ps = GetComponent<ParticleSystem>();
        if (ps != null)
        {
            ps.Play();
            Destroy(gameObject, ps.main.duration);
        }
        else
        {
            Destroy(gameObject, 1f);
        }
    }
}