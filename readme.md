# Cube
Minimal UDP replication and networking library for [Unity3d](https://unity.com/).

## Features
- Support for multiple clients/servers in one process (no singletons, switch between Server + Client/Server/Client in editor)
- Eventual consistency based network model (loosely based on [GDC Vault: I Shot You First! Gameplay Networking in Halo: Reach](http://www.gdcvault.com/play/1014345/I-Shot-You-First-Networking))
- Object-based replication
- Full support for ScriptableObjects (as rpc arguments)
- Automation (client/server prefabs and assets are discovered automatically)
- Transport current based on [Lidgren](https://github.com/lidgren/lidgren-network-gen3)

## Getting Started
Clone the git repository into your **Assets** folder.

### Connecting server and client in editor
Create new GameObject *ClientGame* in the scene and add the *Core/ClientGame* component.
Create another new GameObject *ServerGame* and add the *Core/ServerGame* component. 
When you start the game now you should see log output of the client connecting to the server.

Note that the instant connection in ClientGame is just enabled in the Unity editor.

Now that we've got a connection we can start looking at ...

### Replication
The hearth of Cube is a powerful replication system.


> A **Replica** is replicated from the server to all clients.
> Replicas must always be prefabs and instances of prefabs for Cube to be able to create client instances of them.


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

> A **ReplicaView** observes Replicas for a connection.
> Its position is used to scope and priorize which Replicas to send.

Start the game now and you should see the Replica prefab being replicated. Try to move around the server-side instance in the editor.

For the client to instantiate a different prefab, rename your prefab to *Server_TestReplica*
and create a new prefab variant *Client_TestReplica* (The name prefixes **Server_** and **Client_** are important).
Now you can for instance set a blue transparent material color on the server prefab.

#### ReplicaBehaviour instead of MonoBehaviour
ReplicaBehaviour dervices from MonoBehaviour and provides additional functionality:
- isServer/isClient and server/client
- Ownership and isOwner
- Rpcs

Only run behaviour on server/client.
```C#
using Cube.Replication;
using UnityEngine;

public class Test : ReplicaBehaviour {
    void Update() {
        if (!isServer) // or isClient
            return;

        var pos = transform.position;
        pos.x += Time.deltaTime;
        if (pos.x > 8)
            pos.x = -8;
        transform.position = pos;
    }
}
```

Each Replica has an owning (Replica.owner) connection. Assign ownership with **Replica.AssignOwnership** and take it away with **Replica.TakeOwnership**. Only the server can set and remove ownership. Ownership information is sent to the owning client. 

#### ReplicaRpc
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
