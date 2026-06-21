using UnityEngine;

public class DebugOnly : MonoBehaviour
{
    [SerializeField]
    protected bool allowInDevBuilds = true;

    protected void Awake()
    {
#if !UNITY_EDITOR
        if(!Debug.isDebugBuild || !allowInDevBuilds)
        {
            Destroy(gameObject);
        }
#endif
    }
}