using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [SerializeField]
    protected Transform followTarget;
    
    [SerializeField]
    protected float speed;

    [SerializeField]
    protected Bounds trackingBounds;// Would like to calculate this based on aspect ratio

    protected Vector3 offset;
    
    protected void Awake()
    {
        offset = transform.position - followTarget.position;
    }

    protected Vector3 unprojectedPos = new Vector3();
    protected void Update()
    {
        unprojectedPos.x = followTarget.position.x < trackingBounds.min.x ? trackingBounds.min.x :
                            (followTarget.position.x > trackingBounds.max.x ? trackingBounds.max.x : followTarget.position.x);

        unprojectedPos.y = followTarget.position.y;

        unprojectedPos.z = followTarget.position.z < trackingBounds.min.z ? trackingBounds.min.z :
                            (followTarget.position.z > trackingBounds.max.z ? trackingBounds.max.z : followTarget.position.z);

        var projectedPos = unprojectedPos + offset;
        var delta = projectedPos - transform.position;

        transform.position += delta * speed * Time.deltaTime;
    }
}