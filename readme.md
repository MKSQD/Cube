# Core

## Getting Started

### Connecting server and client in editor
Create new GameObject in the scene and add the *Core/ClientGame* component. Create another new GameObject and add the *Core/ServerGame* component. 
When you start the game now you should see log output of the client connecting to the server.

Note that the instant connection in ClientGame is just enabled in the Unity editor.

Now that we've got a connection we can start looking at replication.

### Replication
The hearth of Cube is a powerful replication system.