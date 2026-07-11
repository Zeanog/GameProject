using System;
using System.Collections;
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

    [SerializeField, Tooltip("Temp hack to determine the conversion factor from world units to UV units.")]
    protected float worldToUVScale = 0.75f;

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
            conveyorAnimator.TextureSpeed = worldToUVScale * -ConveyorDirection * worldSpeed;
        }
    }

    public void ApplyForces(Collision collision)
    {
        //var rigidBody = collision.rigidbody;
        //if (rigidBody == null)
        //{
        //    return;
        //}

        for (int ix = 0; ix < 1/*collision.contactCount*/; ++ix)
        {
            var contactPoint = collision.GetContact(ix);
            var collider = contactPoint.otherCollider;
            var rigidBody = collider.attachedRigidbody;
            
            var targetVelocity = transform.forward * ConveyorDirection * Speed;
            
            StartCoroutine(ApplyForce(rigidBody, targetVelocity, contactPoint));
        }
    }

    public IEnumerator ApplyForce(Rigidbody rigidBody, Vector3 targetVelocity, ContactPoint contactPoint)
    {
        if(!rigidBody)
        {
            yield break;
        }

        //rigidBody.isKinematic = true;

        //yield return new WaitForEndOfFrame();

        //Vector3 contactPointVelocity = rigidBody.GetPointVelocity(contactPoint.point);
        //var contactPointDeltaVelocity = targetVelocity - contactPointVelocity;//Vector3.Dot(rigidBody.linearVelocity, transform.forward) * transform.forward;
        var deltaVelocity = targetVelocity - Vector3.Dot(rigidBody.linearVelocity, transform.forward) * transform.forward;

        if (deltaVelocity.sqrMagnitude < 0.0001f)
        {
            rigidBody.MovePosition( rigidBody.position + targetVelocity * Time.deltaTime);
        }
        else
        {
            rigidBody.AddForceAtPosition(deltaVelocity, contactPoint.point, ForceMode.VelocityChange);
        }

        //rigidBody.isKinematic = false;
    }
}