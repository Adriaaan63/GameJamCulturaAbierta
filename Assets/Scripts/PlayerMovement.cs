using UnityEngine;
using UnityEngine.InputSystem; // Imprescindible para el nuevo Input System

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    public enum MovementMode
    {
        WASD,
        FollowMouseContinuous,
        ClickToMove
    }

    [Header("Mode Selection")]
    [SerializeField] private MovementMode currentMode = MovementMode.WASD;


    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 7f;
    [SerializeField] private float rotationSpeed = 15f;
    [SerializeField] private float stoppingDistance = 0.2f;
    [SerializeField] private float gravity = -9.81f;
    [SerializeField] private float maxStamina = 10f;
    [SerializeField] private float sprintMultiplier= 2f;


    [Header("Raycast Settings")]
    [SerializeField] private LayerMask groundLayer;

    // NUEVO ------------------------------------------------------------------------
    [Header("Flashlight (WASD mode)")]
    [Tooltip("Child object that carries the Flashlight component. In WASD mode it aims at the mouse, independent of where the player is facing.")]
    [SerializeField] private Transform flashlightPivot;
    [Tooltip("Max degrees per second the flashlight can turn. 0 = instant. A cap avoids absurdly violent sweeps on a mouse flick.")]
    [SerializeField] private float aimTurnSpeed = 720f;
    private bool warnedNoPivot;

    [Tooltip("Prints once per second what the flashlight aiming is doing, and draws a red line along the flashlight's forward. Turn off when it works.")]
    [SerializeField] private bool debugAim = true;
    private float nextDebugTime;
    // ------------------------------------------------------------------------------

    private CharacterController controller;
    private Camera mainCamera;

    private Vector2 wasdInput;
    private Vector2 pointerScreenPos;
    private Vector3 targetWorldPosition;
    private bool hasClickTarget;
    private float verticalVelocity;
    [SerializeField] private Flashlight flashlight;

    private bool sprinting = false;
    private float stamina = 10f;

    private void Awake()
    {
        // Cacheamos referencias en Awake (equivalente a BeginPlay temprano / _ready)
        controller = GetComponent<CharacterController>();
        // Cacheamos camara en Awake
        mainCamera = Camera.main;
        targetWorldPosition = transform.position;
        stamina = maxStamina;
    }

    // Este método lo dispara automáticamente el componente "Player Input" 
    // cuando la acción "Move" cambia de estado.
    public void OnMove(InputValue value)
    {
        wasdInput = value.Get<Vector2>();
    }

    public void OnPointerPosition(InputValue value)
    {
        pointerScreenPos = value.Get<Vector2>();
    }

    public void OnClickToMove(InputValue value)
    {
        if(value.isPressed && currentMode == MovementMode.ClickToMove)
        {
            if(TryGetMouseWorldPosition(out Vector3 hitPoint))
            {
                targetWorldPosition = hitPoint;
                hasClickTarget = true;
            }
        }
    }

    private void Update()
    {
        HandleGravity();
        Vector3 moveDirection = currentMode switch
        {
            MovementMode.WASD => GetWASDDirection(),
            MovementMode.FollowMouseContinuous => GetFollowMouseDirection(),
            MovementMode.ClickToMove => GetClickToMoveDirection(),
            _ => Vector3.zero
        };

        ApplyMovement(moveDirection);

        // NUEVO
        UpdateFlashlightAim();
    }

    // -- Calcular Direccion --

    private Vector3 GetWASDDirection()
    {
        return new Vector3(wasdInput.x, 0f, wasdInput.y).normalized;
    }

    private Vector3 GetFollowMouseDirection()
    {
        
        if (TryGetMouseWorldPosition(out Vector3 mouseWorldPos))
        {
            return GetDirectionTowards(mouseWorldPos);
        }
        return Vector3.zero;
    }

    private Vector3 GetClickToMoveDirection()
    {
        if (!hasClickTarget) return Vector3.zero;

        Vector3 direction = GetDirectionTowards(targetWorldPosition);
        if(direction == Vector3.zero)
        {
            hasClickTarget = false; //Llegamos al destino
        }
        return direction;
    }

    private bool TryGetMouseWorldPosition(out Vector3 worldPos)
    {
        Ray ray = mainCamera.ScreenPointToRay(pointerScreenPos);

            
        if(Physics.Raycast(ray, out RaycastHit hit, 100f,groundLayer))
        {
            worldPos = hit.point;
            return true;
        }

        worldPos = Vector3.zero;
        return false;
    }

    private Vector3 GetDirectionTowards(Vector3 destination)
    {
        Vector3 toTarget = destination - transform.position;
        toTarget.y = 0f; //Ignoramos diferencia de altura

        if(toTarget.magnitude <= stoppingDistance)
        {
            return Vector3.zero;
        }

        return toTarget.normalized;
    }

    private void HandleGravity()
    {
        // CharacterController no aplica gravedad por sí solo
        if (controller.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -2f; // Pequeña fuerza hacia abajo para mantenerlo pegado al suelo
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }
    }

    void OnSprint(InputValue value)
    {
        sprinting = value.isPressed;
    }

    private float CalculateSpeed(bool isMoving)
    {
        float finalSpeed = moveSpeed;
        float staminaDrainRate = 3f;
        float staminaRegenRate = 3f;

        // Solo esprinta si mantiene el botón, tiene stamina y se está moviendo
        if (sprinting && stamina > 0f && isMoving)
        {
            finalSpeed *= sprintMultiplier;
            stamina = Mathf.Clamp(stamina - staminaDrainRate * Time.deltaTime, 0f, maxStamina);
        }
        else
        {
            stamina = Mathf.Clamp(stamina + staminaRegenRate * Time.deltaTime, 0f, maxStamina);
        }

        if(stamina <= 0f)
        { 
            sprinting = false;
        }

        return finalSpeed;
    }

    private void ApplyMovement(Vector3 moveDirection)
    {
        bool isMoving = moveDirection.sqrMagnitude > 0.01f;
        float finalSpeed = CalculateSpeed(isMoving);

        Vector3 finalVelocity = (moveDirection * finalSpeed) + (Vector3.up * verticalVelocity);
        controller.Move(finalVelocity * Time.deltaTime);

        if (isMoving)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
    }

    // -- NUEVO: apuntado de la linterna --

    // Punto del mundo bajo el cursor, a la altura del pivote de la linterna.
    // No depende de groundLayer (usa un plano horizontal) ni de que la accion "PointerPosition"
    // este conectada (lee el raton directamente si existe).
    private bool TryGetAimPoint(out Vector3 point)
    {
        point = Vector3.zero;
        if (mainCamera == null) return false;

        Vector2 screenPos = Mouse.current != null
            ? Mouse.current.position.ReadValue()
            : pointerScreenPos;

        Ray ray = mainCamera.ScreenPointToRay(screenPos);
        Plane plane = new Plane(Vector3.up, flashlightPivot.position);

        if (plane.Raycast(ray, out float enter))
        {
            point = ray.GetPoint(enter);
            return true;
        }
        return false;
    }

    private void UpdateFlashlightAim()
    {
        if (flashlightPivot == null)
        {
            if (!warnedNoPivot)
            {
                Debug.LogWarning("PlayerMovement: 'Flashlight Pivot' is not assigned, the flashlight will not aim at the mouse.", this);
                warnedNoPivot = true;
            }
            return;
        }

        if (currentMode != MovementMode.WASD)
        {
            // Otros modos: la linterna simplemente mira hacia donde mira el jugador.
            flashlightPivot.localRotation = Quaternion.identity;
            DebugAim("Movement mode is " + currentMode + ", not WASD, so the flashlight just follows the player.");
            return;
        }

        // WASD: la linterna apunta al raton, el jugador sigue mirando hacia donde camina.
        if (!TryGetAimPoint(out Vector3 aimPoint))
        {
            DebugAim("Could not get a point under the cursor. mainCamera is " + (mainCamera == null ? "NULL (tag your camera as MainCamera)" : "ok") +
                     ", Mouse.current is " + (Mouse.current == null ? "NULL" : "ok") + ".");
            return;
        }

        Vector3 toMouse = aimPoint - flashlightPivot.position;
        toMouse.y = 0f;
        if (toMouse.sqrMagnitude < 0.01f)
        {
            DebugAim("Cursor is right on top of the pivot, keeping the current direction.");
            return; // raton encima del jugador: mantener direccion
        }

        Quaternion targetRotation = Quaternion.LookRotation(toMouse.normalized, Vector3.up);

        flashlightPivot.rotation = aimTurnSpeed <= 0f
            ? targetRotation
            : Quaternion.RotateTowards(flashlightPivot.rotation, targetRotation, aimTurnSpeed * Time.deltaTime);

        if (debugAim)
        {
            Debug.DrawRay(flashlightPivot.position + Vector3.up * 0.5f, flashlightPivot.forward * 6f, Color.red);
        }
        DebugAim("OK. Cursor point = " + aimPoint + ", pivot '" + flashlightPivot.name + "' yaw = " + flashlightPivot.eulerAngles.y.ToString("F0") + " deg.");
    }

    void OnToggleFlashLight(InputValue value)
    {
        if (value.isPressed && flashlight != null)
        {
            flashlight.active = !flashlight.active;
        }
    }
    
    private void DebugAim(string message)
    {
        if (!debugAim || Time.time < nextDebugTime) return;
        nextDebugTime = Time.time + 1f;
        Debug.Log("[Flashlight aim] " + message, this);
    }
}