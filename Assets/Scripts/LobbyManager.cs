using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using static Unity.Services.Relay.Models.AllocationUtils;
using UnityEngine;

public class LobbyManager : MonoBehaviour
{
    public static LobbyManager Instance { get; private set; }

    private Lobby _hostedLobby;
    private float _lobbyHeartbeatTimer;
    private const float HeartbeatInterval = 15f;

    public event Action<string> OnError;
    public event Action<string> OnJoinCodeReady;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        HandleLobbyHeartbeat();
    }

    public async Task InitializeAsync()
    {
        if (UnityServices.State == ServicesInitializationState.Initialized) return;

        await UnityServices.InitializeAsync();
        await AuthenticationService.Instance.SignInAnonymouslyAsync();
        Debug.Log($"[LobbyManager] Signed in as {AuthenticationService.Instance.PlayerId}");
    }

    public async Task HostAsync()
    {
        try
        {
            await InitializeAsync();

            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(1);
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            var options = new CreateLobbyOptions
            {
                IsPrivate = true,
                Data = new System.Collections.Generic.Dictionary<string, DataObject>
                {
                    { "JoinCode", new DataObject(DataObject.VisibilityOptions.Member, joinCode) }
                }
            };

            _hostedLobby = await LobbyService.Instance.CreateLobbyAsync("KingdomFight", 2, options);

            if (NetworkManager.Singleton == null)
                throw new InvalidOperationException("NetworkManager.Singleton is null — no NetworkManager present in the scene.");

            // Player objects are spawned manually (see SpawnPointManager) once the
            // game scene finishes loading, not immediately on connection.
            NetworkManager.Singleton.ConnectionApprovalCallback = ApproveWithoutPlayerObject;

            var relayData = allocation.ToRelayServerData("dtls");
            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(relayData);
            NetworkManager.Singleton.StartHost();

            OnJoinCodeReady?.Invoke(joinCode);
            Debug.Log($"[LobbyManager] Hosted lobby. Join code: {joinCode}");
        }
        catch (Exception e)
        {
            OnError?.Invoke(e.Message);
            Debug.LogError($"[LobbyManager] Host failed: {e}");
        }
    }

    public void StartOffline()
    {
        if (NetworkManager.Singleton == null)
            throw new InvalidOperationException("NetworkManager.Singleton is null — no NetworkManager present in the scene.");

        // Reuses the same server-authoritative spawn flow as online mode
        // (see SpawnPointManager) so gameplay behaves identically offline.
        NetworkManager.Singleton.ConnectionApprovalCallback = ApproveWithoutPlayerObject;
        NetworkManager.Singleton.StartHost();
    }

    public async Task JoinAsync(string joinCode)
    {
        try
        {
            await InitializeAsync();

            JoinAllocation allocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

            if (NetworkManager.Singleton == null)
                throw new InvalidOperationException("NetworkManager.Singleton is null — no NetworkManager present in the scene.");

            var relayData = allocation.ToRelayServerData("dtls");
            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(relayData);
            NetworkManager.Singleton.StartClient();

            Debug.Log($"[LobbyManager] Joined via code: {joinCode}");
        }
        catch (Exception e)
        {
            OnError?.Invoke(e.Message);
            Debug.LogError($"[LobbyManager] Join failed: {e}");
        }
    }

    public async Task StopHosting()
    {
        if (NetworkManager.Singleton != null && (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer))
            NetworkManager.Singleton.Shutdown();

        await Cleanup();
        Debug.Log("[LobbyManager] Host canceled — lobby closed and network shut down.");
    }

    private void ApproveWithoutPlayerObject(
        NetworkManager.ConnectionApprovalRequest request,
        NetworkManager.ConnectionApprovalResponse response)
    {
        response.Approved = true;
        response.CreatePlayerObject = false;
    }

    private async void HandleLobbyHeartbeat()
    {
        if (_hostedLobby == null) return;

        _lobbyHeartbeatTimer -= Time.deltaTime;
        if (_lobbyHeartbeatTimer > 0) return;

        _lobbyHeartbeatTimer = HeartbeatInterval;
        await LobbyService.Instance.SendHeartbeatPingAsync(_hostedLobby.Id);
    }

    public async Task Cleanup()
    {
        if (_hostedLobby == null) return;

        string lobbyId = _hostedLobby.Id;
        _hostedLobby = null;

        try
        {
            await LobbyService.Instance.DeleteLobbyAsync(lobbyId);
            Debug.Log($"[LobbyManager] Lobby '{lobbyId}' deleted successfully.");
        }
        catch (Exception e)
        {
            Debug.LogError($"[LobbyManager] Failed to delete lobby '{lobbyId}': {e}");
        }
    }
}
