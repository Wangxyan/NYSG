using UnityEngine;

public class DestroyAfterTime : MonoBehaviour
{
    public float lifetime = 1.0f; // Ä¬ÈÏ1Ãë

    void Start()
    {
        Destroy(gameObject, lifetime);
    }
}