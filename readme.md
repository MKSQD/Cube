# Cube
GameObject based network replication library for [Unity3d](https://unity.com/). Simple network protocol based on eventual consistency.

## Features
- Integration with [Unreal-style gameplay framework](https://github.com/NoDogsInc/GameFramework)
- Eventual consistency based network model (loosely based on [GDC Vault: I Shot You First! Gameplay Networking in Halo: Reach](http://www.gdcvault.com/play/1014345/I-Shot-You-First-Networking))
- Develop server and client at the same time
- Very simple API
- RPCs as method calls with automatic argument serialisation (including ScriptableObject, arrays, ...)
- Client/server prefabs and assets are discovered automatically, no factory functions to implement or lists to manually maintain
- Transport based on [Lidgren](https://github.com/lidgren/lidgren-network-gen3) but can be replaced easily
- Tooling included

## Getting Started
Clone the git repository into your **Assets** folder.

### Connecting server and client in editor
Create new GameObject *ClientGame* in the scene and add the *Cube/ClientGame* component.
Create another new GameObject *ServerGame* and add the *Cube/ServerGame* component. 
When you start the game now you should see log output of the client connecting to the server.

Note that the automatic connection to the server in ClientGame is just enabled in the Unity editor.

Now that we've got a connection we can start looking at ...

## Replication

A **Replica** is replicated from the server to all clients. Replicas must always be instances of prefabs for Cube to be able to create client instances of them.


Create a new *GameObject* in the scene. Add the *Cube/Replica* component to mark it as an Replica.
Add the *Cube/ReplicaTransform* component to keep their transforms synchronized.

Then create a prefab *TestReplica* from it by dragging the GameObject into the project explorer. Delete the original instance.

Create a new script TestServerGame:
```C#
using Cube.Networking;
using Cube.Replication;
using Cube.Transport;
using UnityEngine;
using BitStream = Cube.Transport.BitStream;

public class TestServerGame : ServerGame {
	public GameObject prefab;

    protected override void OnNewIncomingConnection(Connection connection, BitStream bs) {
        // Create a new ReplicaView for this Connection
        var view = new GameObject("ReplicaView " + connection);
        view.transform.parent = transform;

        var rw = view.AddComponent<ReplicaView>();
        rw.connection = connection;
        
        server.replicaManager.AddReplicaView(rw);

        // Instantiate some Replica
        server.replicaManager.InstantiateReplica(prefab);
    }
}
```
Replace the *ServerGame* component on the ServerGame scene GameObject. Assign TestReplica to it's prefab field.

A **ReplicaView** observes Replicas for a connection. Its position and settings is used to scope and priorize which Replicas to send. Without a ReplicaView the client will not receive any Replicas.

Start the game now and you should see the Replica prefab being replicated. Try to move around the server-side instance in the editor.

For the client to instantiate a different prefab, rename your prefab to *Server_TestReplica*
and create a new prefab variant *Client_TestReplica* (The name prefixes **Server_** and **Client_** are important). 

#### ReplicaBehaviour instead of MonoBehaviour
ReplicaBehaviour derives from MonoBehaviour and is used to implement multiplayer related functionaly on a Replica.
The server/client members can be used to access the Cube instance the Replica is part of. Rpcs are only available for ReplicaBehaviours.

```C#
using Cube.Replication;
using UnityEngine;

public class Test : ReplicaBehaviour {
    void DoTest() {
        if (!isServer)
            return;

         RpcTest();
    }
    
    [ReplicaRpc(RpcTarget.Client)]
    void RpcTest() {
        Debug.Log("Client rpc called");
    }
}
```

#### Replica ownership
Each Replica has an owning (Replica.owner) connection. Assign ownership with **Replica.AssignOwnership** and take it away with **Replica.TakeOwnership**. Only the server can set and remove ownership. Ownership information is sent to the owning client. 

### ReplicaRpc
ReplicaBehaviours can send **unreliable** rpcs. Rpcs are prioritized aggressively, so never rely on these to transmit actual gameplay state. Instead, these should be used for additional, optional effects and cues. 

Rpcs can only be private functions with an \[ReplicaRpc(...)] attribute and its name starting with Rpc.

```C#
using Cube.Replication;
using UnityEngine;

public class TestReplica : ReplicaBehaviour {
    void Update() {
        if (!isServer)
            return;

        RpcTest();
    }

    [ReplicaRpc(RpcTarget.Client)]
    void RpcTest() {
        Debug.Log("Client rpc called");
    }
}
```

## Todo
- Add validation/changed callbacks to ReplicaVars
- Improve documentation (doh)
