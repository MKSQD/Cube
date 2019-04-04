# Cube.Networking.Transport
Minimal game network abstraction layer. Currently contains only a Lidgren based implementation.

Server:
```cs
using UnityEngine;
using Cube.Networking.Transport;

class Server : MonoBehaviour {
    IServerNetworkInterface networkInterface;
    IServerReactor reactor;

    void Start() {
        var port = 1234;
        networkInterface = new LidgrenServerNetworkInterface(port);

        reactor = new ServerReactor(networkInterface);
        reactor.AddHandler((byte)MessageId.NewConnectionEstablished, OnNewConnectionEstablished);
        reactor.AddHandler((byte)MessageId.DisconnectNotification, OnDisconnectNotification);
    }

     void Update() {
         reactor.Update();
         networkInterface.Update();
     }
    
      void OnNewConnectionEstablished(Connection connection, BitStream bs) {
      }

      void OnDisconnectNotification(Connection connection, BitStream bs) {
      }
}
```

Client:
```cs
using UnityEngine;
using Cube.Networking.Transport;

class Client : MonoBehaviour {
    IClientNetworkInterface networkInterface;
    IClientReactor reactor;

    void Start() {
        networkInterface = new LidgrenClientNetworkInterface(lagSettings);
        reactor = new ClientReactor(networkInterface);
    }
    
    void Update() {
        reactor.Update();
        networkInterface.Update();
    }
    
    void OnApplicationQuit() {
        networkInterface.Shutdown(0);
    }
}
```
