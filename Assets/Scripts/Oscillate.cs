using UnityEngine;

public class Oscillate : MonoBehaviour
{
    [SerializeField]
    protected float speed;

    protected float time = 0;

    [SerializeField]
    protected float magnitude;

    [SerializeField]
    protected Vector3 direction = Vector3.up;

    protected virtual void Awake()
    {
    }

    protected void Update()
    {
        time += Time.deltaTime * speed;
        transform.position += direction * Mathf.Sin(time) * (magnitude / 1000f);
    }
}