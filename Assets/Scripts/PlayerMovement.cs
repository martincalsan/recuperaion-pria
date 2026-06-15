using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(NetworkTransform))]
public class PlayerMovement : NetworkBehaviour
{
    const float Speed   = 5f;
    const float GroundY = 0.5f;
    const float Bound   = 4.5f;
    const float Deadzone = 0.15f;

    [Tooltip("Asignar InputSystem_Actions")]
    public InputActionAsset InputActions;

    InputAction _moveAction;

    public NetworkVariable<byte> ColorIndex = new NetworkVariable<byte>(0);

    Renderer _renderer;

    void Awake()
    {
        if (InputActions == null)
            InputActions = Resources.Load<InputActionAsset>("InputSystem_Actions");

        if (InputActions != null)
        {
            var map = InputActions.FindActionMap("Player", true);
            if (map != null)
                _moveAction = map.FindAction("Move", true);
        }
    }

    void OnEnable()
    {
        InputActions?.Enable();
    }

    void OnDisable()
    {
        InputActions?.Disable();
    }

    public override void OnNetworkSpawn()
    {
        _renderer = GetComponentInChildren<Renderer>();

        if (_renderer != null)
            _renderer.material = Object.Instantiate(_renderer.material);

        ColorIndex.OnValueChanged += OnColorChanged;

        ApplyColor(ColorIndex.Value);

        if (IsServer)
        {
            transform.position = new Vector3(Random.Range(-3f, 3f), GroundY, Random.Range(-3f, 3f));
            ColorIndex.Value = 0;
            GameManager.Instance?.RegisterPlayer(this);
        }
    }

    public override void OnNetworkDespawn()
    {
        ColorIndex.OnValueChanged -= OnColorChanged;
        if (IsServer)
            GameManager.Instance?.UnregisterPlayer(this);
    }

    void OnColorChanged(byte previous, byte current) => ApplyColor(current);

    void ApplyColor(byte idx)
    {
        if (_renderer == null) _renderer = GetComponentInChildren<Renderer>();
        if (_renderer == null) return;

        switch (idx)
        {
            default:
            case 0: _renderer.material.color = Color.white; break;
            case 1: _renderer.material.color = Color.red; break;
            case 2: _renderer.material.color = Color.green; break;
            case 3: _renderer.material.color = Color.blue; break;
            case 4: _renderer.material.color = Color.yellow; break;
        }
    }

    void Update()
    {
        if (!IsOwner) return;

        if (_moveAction != null)
        {
            Vector2 input = _moveAction.ReadValue<Vector2>();
            if (input.sqrMagnitude > 0.0001f)
                MoveServerRpc(input);
        }
    }

    [ServerRpc]
    void MoveServerRpc(Vector2 input) => ServerHandleMove(input);

    void ServerHandleMove(Vector2 input)
    {
        if (!IsServer) return;

        Vector2 inNorm = input;
        if (Mathf.Abs(inNorm.x) < Deadzone) inNorm.x = 0f;
        if (Mathf.Abs(inNorm.y) < Deadzone) inNorm.y = 0f;
        if (inNorm.sqrMagnitude < 0.0001f) return;

        byte desiredColor = GetColorFromInput(inNorm);
        byte currentColor = ColorIndex.Value;

        if (desiredColor != 0 && desiredColor != currentColor && !GameManager.Instance.CanAcquireColor(desiredColor))
        {
            if (Mathf.Abs(input.x) > Mathf.Abs(input.y))
                input.x = 0f;
            else
                input.y = 0f;

            if (Mathf.Abs(input.x) < Deadzone && Mathf.Abs(input.y) < Deadzone)
                return;

            desiredColor = GetColorFromInput(input);
            if (desiredColor != 0 && desiredColor != currentColor && !GameManager.Instance.CanAcquireColor(desiredColor))
                return;
        }

        if (desiredColor != currentColor)
        {
            if (desiredColor == 0 || GameManager.Instance.TryAcquireColor(this, desiredColor))
            {
                ColorIndex.Value = desiredColor;
            }
            else
            {
                return;
            }
        }

        var pos = transform.position + new Vector3(input.x * Speed, 0f, input.y * Speed) * Time.deltaTime;
        pos.y = GroundY;
        pos.x = Mathf.Clamp(pos.x, -Bound, Bound);
        pos.z = Mathf.Clamp(pos.z, -Bound, Bound);
        transform.position = pos;
    }

    byte GetColorFromInput(Vector2 input)
    {
        if (Mathf.Abs(input.x) > Mathf.Abs(input.y))
        {
            if (input.x < 0f) return 1;
            if (input.x > 0f) return 3;
        }
        else
        {
            if (input.y < 0f) return 2;
            if (input.y > 0f) return 4;
        }
        return 0;
    }

    public void SetColorServer(byte newColor)
    {
        if (!IsServer) return;
        byte old = ColorIndex.Value;
        if (old == newColor) return;
        GameManager.Instance?.ForceChangeColor(this, old, newColor);
        ColorIndex.Value = newColor;
    }
}