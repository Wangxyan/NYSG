using UnityEngine;

public class DestroyAfterTime : MonoBehaviour
{
    public float lifetime = 1.0f; // Ĭ��1��

    void Start()
    {
        Destroy(gameObject, lifetime);
    }
}