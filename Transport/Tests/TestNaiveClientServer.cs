using System.Collections;
using UnityEngine.TestTools;
using NUnit.Framework;

namespace Cube.Transport.Tests {
    public class TestNaiveClientServer {
        [Test]
        public void StartAndShutdownServer() {
            var server = new LiteNetServerNetworkInterface(42000);
            Assert.IsTrue(server.IsRunning);

            server.Shutdown();

            Assert.IsFalse(server.IsRunning);
        }

        [UnityTest]
        public IEnumerator ClientServerConnect() {
            var server = new LiteNetServerNetworkInterface(42000);
            server.NewConnectionEstablished += peer => { };
            server.ApproveConnection += bs => { return new ApprovalResult() { Approved = true }; };

            var client = new LiteNetClientNetworkInterface();
            client.ConnectionRequestAccepted += () => { };

            client.Connect("127.0.0.1", 42000);

            yield return Utils.RunTill(() => {
                server.Update();
                client.Update();
                return false;
            }, 1f);

            Assert.IsTrue(client.IsConnected);

            client.Shutdown(0);
            server.Shutdown();

            yield return null;
        }
    }
}
