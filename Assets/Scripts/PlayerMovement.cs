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


    [Header("Raycast Settings")]
    [SerializeField] private LayerMask groundLayer;

    private CharacterController controller;
    private Camera mainCamera;

    private Vector2 wasdInput;
    private Vector2 pointerScreenPos;
    private Vector3 targetWorldPosition;
    private bool hasClickTarget;
    private float verticalVelocity;

    private void Awake()
    {
        // Cacheamos referencias en Awake (equivalente a BeginPlay temprano / _ready)
        controller = GetComponent<CharacterController>();
        // Cacheamos camara en Awake
        mainCamera = Camera.main;
        targetWorldPosition = transform.position;
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
            Debug.Log("Hola");
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

    private void ApplyMovement(Vector3 moveDirection)
    {
        Vector3 finalVelocity = (moveDirection * moveSpeed) + (Vector3.up * verticalVelocity);
        controller.Move(finalVelocity * Time.deltaTime);

        if(moveDirection.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
    }
}