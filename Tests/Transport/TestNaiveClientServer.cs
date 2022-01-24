using System.Collections;
using Cube.Transport.LiteNet;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Cube.Transport.Tests {
    public class TestNaiveClientServer {
        static LiteNetTransport Transport => new LiteNetTransport() { Port = 42000 };

        [Test]
        public void StartAndShutdownServer() {
            var server = new LiteNetServerNetworkInterface(Transport);

            Assert.IsTrue(server.IsRunning);

            server.Shutdown();

            Assert.IsFalse(server.IsRunning);
        }

        [UnityTest]
        public IEnumerator ClientServerConnect() {
            var server = new LiteNetServerNetworkInterface(Transport);
            server.NewConnectionEstablished += peer => { };
            server.ApproveConnection += bs => { return new ApprovalResult() { Approved = true }; };

            var client = new LiteNetClientNetworkInterface(Transport);
            client.ConnectionRequestAccepted += () => { };

            client.Connect("127.0.0.1");

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
