# Netcode for GameObjects Fundamentals

Unity's official networking framework for multiplayer games (package: `com.unity.netcode.gameobjects`).

## Core Concepts

### NetworkManager

The central singleton managing network state, connections, and spawning.

```csharp
using Unity.Netcode;

// Start as host (server + client)
NetworkManager.Singleton.StartHost();

// Start as dedicated server
NetworkManager.Singleton.StartServer();

// Start as client
NetworkManager.Singleton.StartClient();

// Shutdown
NetworkManager.Singleton.Shutdown();
```

### NetworkBehaviour

Base class for all networked MonoBehaviours. Provides access to network identity, ownership, and RPC methods.

```csharp
public class PlayerController : NetworkBehaviour
{
    public override void OnNetworkSpawn()
    {
        // Called when this object is spawned on the network
        if (IsOwner)
        {
            EnableInput();
        }
    }

    public override void OnNetworkDespawn()
    {
        // Called when this object is despawned from the network
    }
}
```

### Key Properties

| Property | Description |
|----------|-------------|
| `IsServer` | True on server (host or dedicated) |
| `IsClient` | True on client (host or remote) |
| `IsHost` | True on host (server + client) |
| `IsOwner` | True if local client owns this object |
| `IsLocalPlayer` | True if this is the local player's object |
| `NetworkObjectId` | Unique network ID for this object |
| `OwnerClientId` | Client ID of the owner |

## NetworkVariable

Synchronized variables that automatically replicate from server to clients.

```csharp
public class PlayerHealth : NetworkBehaviour
{
    // Only server can write (default), all can read
    private NetworkVariable<int> health = new NetworkVariable<int>(
        100,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public override void OnNetworkSpawn()
    {
        health.OnValueChanged += OnHealthChanged;
    }

    public override void OnNetworkDespawn()
    {
        health.OnValueChanged -= OnHealthChanged;
    }

    private void OnHealthChanged(int previousValue, int newValue)
    {
        // React to health change on all clients
        UpdateHealthBar(newValue);
    }
}
```

### Supported Types

NetworkVariable supports: `bool`, `byte`, `sbyte`, `short`, `ushort`, `int`, `uint`, `long`, `ulong`, `float`, `double`, `Vector2`, `Vector3`, `Vector4`, `Quaternion`, `Color`, `Color32`, `Ray`, `Ray2D`, and any struct implementing `INetworkSerializable`.

### Custom Serializable Types

```csharp
public struct PlayerData : INetworkSerializable
{
    public int Score;
    public FixedString32Bytes Name;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Score);
        serializer.SerializeValue(ref Name);
    }
}

// Usage
private NetworkVariable<PlayerData> playerData = new();
```

## Remote Procedure Calls (RPCs)

### ServerRpc

Called by clients, executed on server. Method name must end with `ServerRpc`.

```csharp
[ServerRpc]
private void ShootServerRpc(Vector3 direction)
{
    // Validate and process on server
    if (CanShoot())
    {
        SpawnProjectile(direction);
        // Notify all clients
        PlayShootEffectClientRpc();
    }
}
```

### ClientRpc

Called by server, executed on all clients. Method name must end with `ClientRpc`.

```csharp
[ClientRpc]
private void PlayShootEffectClientRpc()
{
    // Play visual/audio effect on all clients
    audioSource.PlayOneShot(shootSound);
    muzzleFlash.Play();
}
```

### RPC Parameters

```csharp
// Target specific clients
[ClientRpc]
private void SendMessageClientRpc(string message, ClientRpcParams rpcParams = default)
{
    DisplayMessage(message);
}

// Send to specific client
var targetParams = new ClientRpcParams
{
    Send = new ClientRpcSendParams
    {
        TargetClientIds = new ulong[] { targetClientId }
    }
};
SendMessageClientRpc("You win!", targetParams);
```

## Object Spawning

### Spawn Networked Objects

```csharp
// Server-side spawning
[ServerRpc]
private void SpawnObjectServerRpc()
{
    GameObject instance = Instantiate(prefab, spawnPoint.position, Quaternion.identity);
    NetworkObject networkObject;
    if (instance.TryGetComponent(out networkObject))
    {
        networkObject.Spawn(); // Spawn with server ownership

        // Or spawn with client ownership
        networkObject.SpawnWithOwnership(OwnerClientId);
    }
}

// Despawn
networkObject.Despawn(); // Removes from network and destroys
networkObject.Despawn(false); // Removes from network but keeps GameObject
```

### Network Prefab Registration

All networked prefabs must be registered in the NetworkManager's Network Prefab List or via code.

```csharp
// Runtime registration (before StartHost/StartClient)
NetworkManager.Singleton.AddNetworkPrefab(myPrefab);
```

## Connection Management

```csharp
void Start()
{
    NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
    NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
}

private void OnClientConnected(ulong clientId)
{
    Debug.Log($"Client {clientId} connected");
}

private void OnClientDisconnected(ulong clientId)
{
    Debug.Log($"Client {clientId} disconnected");
}
```

## Transform Synchronization

Add the `NetworkTransform` component for automatic position/rotation sync. For owner-authoritative movement, use `ClientNetworkTransform`.

```csharp
// Server-authoritative: server controls transform, clients interpolate
// Add NetworkTransform component

// Client-authoritative: owner controls transform
// Add ClientNetworkTransform component (from Netcode samples)
```

## Best Practices

1. **Server authority**: Validate all game-critical actions on the server
2. **Minimize NetworkVariables**: Only sync what clients need to know
3. **Use RPCs for events**: One-time actions, not continuous state
4. **Use NetworkVariables for state**: Continuously changing values
5. **Test with latency simulation**: Use Unity Transport's simulated latency
6. **Handle disconnection**: Clean up resources when clients disconnect
7. **Avoid spawning in Awake**: Wait for `OnNetworkSpawn` to access network state
