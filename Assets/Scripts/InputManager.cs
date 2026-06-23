using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; protected set; }
    public bool IsMouseLocked {
        get {
            return Cursor.lockState == CursorLockMode.Locked;
        }
        set {
            Cursor.lockState = value ? CursorLockMode.Locked : CursorLockMode.None;
        }
    }
    void Awake()
    {
        if (Instance != null && Instance != this) {
            Destroy(this.gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(this.gameObject);
    }

    public void OnMovement(InputAction.CallbackContext value)
    {
        Vector2 inputMovement = value.ReadValue<Vector2>();
        //rawInputMovement = new Vector3(inputMovement.x, 0, inputMovement.y);
    }

    //This is called from PlayerInput, when a button has been pushed, that corresponds with the 'Attack' action
    public void OnAttack(InputAction.CallbackContext value)
    {
        if (value.started)
        {
            //playerAnimationBehaviour.PlayAttackAnimation();
        }
    }

    //This is called from Player Input, when a button has been pushed, that corresponds with the 'TogglePause' action
    public void OnTogglePause(InputAction.CallbackContext value)
    {
        if (value.started)
        {
            //GameManager.Instance.TogglePauseState(this);
        }
    }
}   