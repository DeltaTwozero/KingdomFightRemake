using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(NetworkObject))]
public class NetworkPlayerController : NetworkBehaviour
{
    // ─── Inspector ─────────────────────────────────────────────────────────────

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float jumpHeight = 1.5f;
    [SerializeField] private float gravityMultiplier = 2f;

    [Header("Camera")]
    [SerializeField] private float mouseSensitivity = 0.1f;
    [SerializeField] private float verticalLookClamp = 85f;

    [Header("Interaction")]
    [SerializeField] private float interactRange = 3f;
    [SerializeField] private LayerMask interactableLayer = ~0;

    [Header("Audio")]
    [SerializeField] private AudioClip[] footstepClips;
    [SerializeField] private float footstepInterval = 0.45f;

    [Header("References")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Transform interactionOrigin;

    // ─── Private state ─────────────────────────────────────────────────────────

    private PlayerInputActions _input;
    private CharacterController _cc;
    private AudioSource _audioSource;

    private Vector3 _velocity;
    private float _cameraPitch;
    private float _footstepTimer;

    // ─── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        _cc = GetComponent<CharacterController>();
        _audioSource = GetComponentInChildren<AudioSource>();
        _input = new PlayerInputActions();
    }

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            if (playerCamera != null)
            {
                playerCamera.enabled = false;
                var listener = playerCamera.GetComponent<AudioListener>();
                if (listener != null) listener.enabled = false;
            }
            enabled = false;
            return;
        }

        if (playerCamera != null)
        {
            playerCamera.enabled = true;
            var listener = playerCamera.GetComponent<AudioListener>();
            if (listener != null) listener.enabled = true;
        }

        if (interactionOrigin == null && playerCamera != null)
            interactionOrigin = playerCamera.transform;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        _input.Player.Enable();
    }

    public override void OnNetworkDespawn()
    {
        if (!IsOwner) return;

        _input.Player.Disable();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Update()
    {
        if (!IsOwner) return;

        HandleLook();
        HandleMovement();
        HandleInteraction();
    }

    // ─── Look ──────────────────────────────────────────────────────────────────

    private void HandleLook()
    {
        Vector2 look = _input.Player.Look.ReadValue<Vector2>() * mouseSensitivity;

        transform.Rotate(Vector3.up * look.x);

        _cameraPitch -= look.y;
        _cameraPitch = Mathf.Clamp(_cameraPitch, -verticalLookClamp, verticalLookClamp);

        if (playerCamera != null)
            playerCamera.transform.localRotation = Quaternion.Euler(_cameraPitch, 0f, 0f);
    }

    // ─── Movement ──────────────────────────────────────────────────────────────

    private void HandleMovement()
    {
        bool grounded = _cc.isGrounded;

        if (grounded && _velocity.y < 0f)
            _velocity.y = -2f;

        Vector2 input = _input.Player.Move.ReadValue<Vector2>();
        Vector3 move  = transform.right * input.x + transform.forward * input.y;

        if (_input.Player.Jump.WasPressedThisFrame() && grounded)
            _velocity.y = Mathf.Sqrt(jumpHeight * 2f * Mathf.Abs(Physics.gravity.y));

        _velocity.y += Physics.gravity.y * gravityMultiplier * Time.deltaTime;

        _cc.Move((move * moveSpeed + _velocity) * Time.deltaTime);

        if (grounded && move.sqrMagnitude > 0.01f)
        {
            _footstepTimer -= Time.deltaTime;
            if (_footstepTimer <= 0f)
            {
                PlayFootstep();
                _footstepTimer = footstepInterval;
            }
        }
        else
        {
            _footstepTimer = 0f;
        }
    }

    // ─── Interaction ───────────────────────────────────────────────────────────

    private void HandleInteraction()
    {
        if (!_input.Player.Interact.WasPressedThisFrame()) return;
        if (interactionOrigin == null) return;

        Ray ray = new Ray(interactionOrigin.position, interactionOrigin.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, interactRange, interactableLayer))
        {
            IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();
            if (interactable == null) return;

            NetworkObject netObj = hit.collider.GetComponentInParent<NetworkObject>();
            if (netObj != null)
                InteractServerRpc(netObj.NetworkObjectId);
            else
                interactable.Interact(OwnerClientId);
        }
    }

    [ServerRpc]
    private void InteractServerRpc(ulong targetNetworkId)
    {
        if (targetNetworkId == ulong.MaxValue) return;

        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects
                .TryGetValue(targetNetworkId, out NetworkObject netObj))
        {
            IInteractable interactable = netObj.GetComponentInChildren<IInteractable>();
            interactable?.Interact(OwnerClientId);
        }
    }

    // ─── Footsteps ─────────────────────────────────────────────────────────────

    private void PlayFootstep()
    {
        if (_audioSource == null || footstepClips == null || footstepClips.Length == 0) return;
        _audioSource.PlayOneShot(footstepClips[Random.Range(0, footstepClips.Length)]);
    }

    // ─── Editor helpers ────────────────────────────────────────────────────────

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (interactionOrigin == null && playerCamera != null)
            interactionOrigin = playerCamera.transform;

        if (interactionOrigin == null) return;

        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(interactionOrigin.position, interactionOrigin.forward * interactRange);
    }
#endif
}
