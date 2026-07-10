using System;
using System.Collections.Generic;
using UnityEngine;

public class ConveyorBelt : MonoBehaviour
{
    public enum ETopDirection
    {
        Forward = 1,
        Backward = -1
    }

    [SerializeField, Tooltip("World units per second the belt tries to move objects.")]
    protected float speed = 0f;
    public float Speed
    {
        get { return speed; }
        set
        {
            if(Mathf.Approximately(value, speed))
            {
                return;
            }
            speed = value;
            SetTextureSpeed(speed);
        }
    }

    [SerializeField, Tooltip("Local direction of belt tangent (in local space).")]
    protected ETopDirection conveyorDirection = ETopDirection.Forward;

    public float ConveyorDirection
    {
        get
        {
            return (float)conveyorDirection;
        }
    }

    [SerializeField, Tooltip("Layers the belt affects (default = Everything).")]
    LayerMask affectedLayers = ~0;

    protected ConveyorBeltAnimator conveyorAnimator;

    protected void Awake()
    {
        conveyorAnimator = GetComponentInChildren<ConveyorBeltAnimator>();
        SetTextureSpeed(speed);
    }

    protected void SetTextureSpeed(float worldSpeed)
    {
        if (conveyorAnimator != null)
        {
            //UV: 0 - 1
            //Length: meshCollider
            var length = conveyorAnimator.GetComponent<MeshCollider>().sharedMesh.bounds.extents.z * 2.0f;
            var time = length / worldSpeed;
            conveyorAnimator.TextureSpeed = -ConveyorDirection * worldSpeed;
        }
    }

    public void ApplyForces(Collision collision)
    {
        var rigidBody = collision.rigidbody;
        if (rigidBody == null)
        {
            return;
        }

        for (int ix = 0; ix < collision.contactCount; ++ix)
        {
            var contactPoint = collision.GetContact(ix);
            var collider = contactPoint.otherCollider;

            collider.attachedRigidbody?.AddForceAtPosition(transform.forward * ConveyorDirection * Speed, contactPoint.point, ForceMode.VelocityChange);
        }
    }
}