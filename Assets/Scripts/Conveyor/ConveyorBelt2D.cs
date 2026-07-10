using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 2D conveyor-belt behaviour for Unity.
/// Attach to a GameObject with a 2D collider (recommended: CapsuleCollider2D configured horizontal).
/// The script applies forces to dynamic Rigidbody2D objects contacting the top of the belt.
/// Continuous: rigidbodies are tracked while in contact and pushed every FixedUpdate.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Physics2D/Conveyor Belt 2D")]
[RequireComponent(typeof(Collider2D))]
public class ConveyorBelt2D : MonoBehaviour
{
    [SerializeField, Tooltip("World units per second the belt tries to move objects.")]
    float speed = 2f;

    [SerializeField, Tooltip("Local direction of belt tangent (in local space).")]
    Vector2 localDirection = Vector2.up;

    [SerializeField, Tooltip("Strength of force applied to dynamic bodies.")]
    float forceMultiplier = 80f;

    [SerializeField, Tooltip("How \"top\" a contact normal must be to count (0..1).")]
    float contactUpDotThreshold = 0.3f;

    [SerializeField, Tooltip("Layers the belt affects (default = Everything).")]
    LayerMask affectedLayers = ~0;

    // Currently contacting rigidbodies that should be pushed each physics step.
    readonly HashSet<Rigidbody2D> _contacts = new HashSet<Rigidbody2D>();

    // Temporarily record rb's whose contact should be evaluated/added this physics step.
    readonly HashSet<Rigidbody2D> _toAdd = new HashSet<Rigidbody2D>();
    readonly HashSet<Rigidbody2D> _toRemove = new HashSet<Rigidbody2D>();

    void Awake()
    {
        if (localDirection.sqrMagnitude <= 0.0f)
            localDirection = Vector2.right;
        else
            localDirection.Normalize();
    }

    void FixedUpdate()
    {
        // Apply belt force to all tracked contacts every physics step.
        Vector2 worldDir = (Vector2)transform.TransformDirection(localDirection).normalized;

        // Apply additions/removals buffered from collision callbacks to avoid modifying collection while iterating.
        if (_toRemove.Count > 0)
        {
            foreach (var r in _toRemove) _contacts.Remove(r);
            _toRemove.Clear();
        }
        if (_toAdd.Count > 0)
        {
            foreach (var r in _toAdd) _contacts.Add(r);
            _toAdd.Clear();
        }

        if (_contacts.Count == 0)
            return;

        // Iterate a copy to be robust against external changes
        var copy = new Rigidbody2D[_contacts.Count];
        _contacts.CopyTo(copy);

        foreach (var rb in copy)
        {
            if (rb == null)
            {
                _contacts.Remove(rb);
                continue;
            }

            // safety layer check
            if (((1 << rb.gameObject.layer) & affectedLayers) == 0)
            {
                _contacts.Remove(rb);
                continue;
            }

            ApplyBeltToRigidbody(rb, worldDir);
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        HandleCollisionContacts(collision);
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        HandleCollisionContacts(collision);
    }

    void OnCollisionExit2D(Collision2D collision)
    {
        var rb = collision.rigidbody ?? collision.collider.attachedRigidbody;
        if (rb != null)
            _toRemove.Add(rb);
    }

    void HandleCollisionContacts(Collision2D collision)
    {
        if (((1 << collision.gameObject.layer) & affectedLayers) == 0)
            return;

        var rb = collision.rigidbody ?? collision.collider.attachedRigidbody;
        if (rb == null)
            return;

        // decide if there is a "top" contact by checking contact normals
        bool topContact = false;
        foreach (var contact in collision.contacts)
        {
            // contact.normal points from the other collider into this collider
            // if it's roughly aligned with belt's up vector, we treat it as top contact
            if (Vector2.Dot(contact.normal, (Vector2)transform.up) > contactUpDotThreshold)
            {
                topContact = true;
                break;
            }

            // also allow small tolerance if contact point is above belt center
            if (contact.point.y > transform.position.y - 0.01f)
            {
                topContact = true;
            }
        }

        if (topContact)
        {
            _toAdd.Add(rb);
            // ensure it's not queued for removal
            _toRemove.Remove(rb);
        }
        else
        {
            _toRemove.Add(rb);
            _toAdd.Remove(rb);
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        HandleTriggerEnterOrStay(other);
    }

    void OnTriggerStay2D(Collider2D other)
    {
        HandleTriggerEnterOrStay(other);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        var rb = other.attachedRigidbody;
        if (rb != null) _toRemove.Add(rb);
    }

    void HandleTriggerEnterOrStay(Collider2D other)
    {
        if (((1 << other.gameObject.layer) & affectedLayers) == 0)
            return;

        var rb = other.attachedRigidbody;
        if (rb == null)
            return;

        // approximate top check: object center above belt center in belt local up direction
        Vector2 toObject = (Vector2)other.bounds.center - (Vector2)transform.position;
        if (Vector2.Dot(toObject, (Vector2)transform.up) >= -0.02f)
        {
            _toAdd.Add(rb);
            _toRemove.Remove(rb);
        }
        else
        {
            _toRemove.Add(rb);
            _toAdd.Remove(rb);
        }
    }

    void ApplyBeltToRigidbody(Rigidbody2D rb, Vector2 worldDir)
    {
        if (rb.bodyType == RigidbodyType2D.Dynamic)
        {
            // Apply a force so physics carries the object along the belt.
            Vector2 push = worldDir * speed * forceMultiplier * Time.fixedDeltaTime;
            // rb.AddForce(push, ForceMode2D.Force);
            rb.linearVelocity = push;
        }
        else if (rb.bodyType == RigidbodyType2D.Kinematic)
        {
            // Set horizontal velocity component to belt speed (preserve vertical)
            rb.linearVelocity = new Vector2(worldDir.x * speed, rb.linearVelocity.y);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Vector3 origin = transform.position;
        Vector3 dir = transform.TransformDirection((Vector3)localDirection.normalized) * 0.75f;
        Gizmos.DrawLine(origin, origin + dir);
        Gizmos.DrawLine(origin + dir, origin + dir + Quaternion.Euler(0, 0, 150) * dir * 0.25f);
        Gizmos.DrawLine(origin + dir, origin + dir + Quaternion.Euler(0, 0, -150) * dir * 0.25f);

        var cap = GetComponent<CapsuleCollider2D>();
        if (cap != null)
        {
            Gizmos.color = Color.yellow;
            Vector2 size = cap.size;
            Vector2 offset = cap.offset;
            Vector3 a = transform.TransformPoint(offset + new Vector2(-size.x * 0.5f, -size.y * 0.5f));
            Vector3 b = transform.TransformPoint(offset + new Vector2(size.x * 0.5f, -size.y * 0.5f));
            Vector3 c = transform.TransformPoint(offset + new Vector2(size.x * 0.5f, size.y * 0.5f));
            Vector3 d = transform.TransformPoint(offset + new Vector2(-size.x * 0.5f, size.y * 0.5f));
            Gizmos.DrawLine(a, b);
            Gizmos.DrawLine(b, c);
            Gizmos.DrawLine(c, d);
            Gizmos.DrawLine(d, a);
        }
    }
}