using System.Collections;
using UnityEngine.TestTools;
using NUnit.Framework;

namespace Cube.Networking.Transport.Tests {
#if CLIENT && SERVER
    public class TestNaiveClientServer {
        [Test]
        public void StartAndShutdownServer() {
            var server = new LidgrenServerNetworkInterface(9600);
            Assert.IsTrue(server.isRunning);
            server.Shutdown();
            Assert.IsFalse(server.isRunning);
        }

        [UnityTest]
        public IEnumerator ClientServerConnect() {
            var server = new LidgrenServerNetworkInterface(9600);
            var client = new LidgrenClientNetworkInterface(new ClientSimulatedLagSettings());
            
            client.Connect("127.0.0.1", 9600);
                        
            yield return Utils.RunTill(() => {
                Connection con; //#TODO check ConnectionId
                server.Receive(out con);    //#TODO check BitStream ids
                client.Receive();   //#TODO check BitStream ids

                return false;
            }, 1f);

            Assert.IsTrue(client.IsConnected());
            
            yield return null;
        }
    }
#endif
}
