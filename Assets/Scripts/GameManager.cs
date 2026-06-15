using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance { get; private set; }

    readonly List<PlayerMovement> _players = new List<PlayerMovement>();
    readonly int[] _counts = new int[5];

    void Awake() => Instance = this;

    public void RegisterPlayer(PlayerMovement p)
    {
        if (!IsServer) return;
        if (!_players.Contains(p)) _players.Add(p);
        byte c = p.ColorIndex.Value;
        if (c >= 0 && c < _counts.Length) _counts[c]++;
    }

    public void UnregisterPlayer(PlayerMovement p)
    {
        if (!IsServer) return;
        if (_players.Remove(p))
        {
            byte c = p.ColorIndex.Value;
            if (c >= 0 && c < _counts.Length) _counts[c] = Mathf.Max(0, _counts[c] - 1);
        }
    }

    public bool CanAcquireColor(byte color)
    {
        if (!IsServer) return false;
        if (color == 0) return true;
        return _counts[color] < 2;
    }

    public bool TryAcquireColor(PlayerMovement p, byte newColor)
    {
        if (!IsServer) return false;
        byte old = p.ColorIndex.Value;
        if (newColor == old) return true;
        if (newColor != 0 && _counts[newColor] >= 2) return false;
        if (old >= 0 && old < _counts.Length) _counts[old] = Mathf.Max(0, _counts[old] - 1);
        if (newColor >= 0 && newColor < _counts.Length) _counts[newColor]++;
        return true;
    }

    public void ForceChangeColor(PlayerMovement p, byte oldColor, byte newColor)
    {
        if (!IsServer) return;
        if (oldColor >= 0 && oldColor < _counts.Length) _counts[oldColor] = Mathf.Max(0, _counts[oldColor] - 1);
        if (newColor >= 0 && newColor < _counts.Length) _counts[newColor]++;
    }

    public void OnResetColorButton()
    {
        if (NetworkManager.Singleton == null) return;
        if (NetworkManager.Singleton.IsServer && !NetworkManager.Singleton.IsHost)
        {
            foreach (var p in _players)
            {
                byte old = p.ColorIndex.Value;
                if (old >= 0 && old < _counts.Length) _counts[old] = Mathf.Max(0, _counts[old] - 1);
                p.ColorIndex.Value = 0;
            }
            _counts[0] = _players.Count;
        }
    }

    void OnGUI()
    {
        if (NetworkManager.Singleton == null) return;

        if (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
        {
            if (GUILayout.Button("Host"))   NetworkManager.Singleton.StartHost();
            if (GUILayout.Button("Server")) NetworkManager.Singleton.StartServer();
            if (GUILayout.Button("Client")) NetworkManager.Singleton.StartClient();
            return;
        }

        if (NetworkManager.Singleton.IsServer && !NetworkManager.Singleton.IsHost)
        {
            if (GUILayout.Button("Cor de inicio"))
            {
                OnResetColorButton();
            }
        }
    }
}
