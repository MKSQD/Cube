# Cube.Networking
### Advantages over UNet ###
  - Support for multiple clients/servers in one process (no singletons, switch between Server+Client/Server/Client in editor)
  - Eventual consistency based network model (loosely based on [GDC Vault: I Shot You First! Gameplay Networking in Halo: Reach](http://www.gdcvault.com/play/1014345/I-Shot-You-First-Networking))
  - Full support for ScriptableObjects (as rpc arguments)
  - Automation (client/server prefabs are discovered automatically)
  - Full integration with the rest of Cube
