using System.Collections;
using UnityEngine.TestTools;
using NUnit.Framework;

namespace Cube.Transport.Tests {
    public class TestNaiveClientServer {
        [Test]
        public void StartAndShutdownServer() {
            var server = new LidgrenServerNetworkInterface("Test", 9600, new SimulatedLagSettings());
            Assert.IsTrue(server.IsRunning);
            server.Shutdown();
            Assert.IsFalse(server.IsRunning);
        }

        [UnityTest]
        public IEnumerator ClientServerConnect() {
            var server = new LidgrenServerNetworkInterface("Test", 9600, new SimulatedLagSettings());
            server.ApproveConnection += bs => { return new ApprovalResult() { Approved = true }; };

            var client = new LidgrenClientNetworkInterface("Test", new SimulatedLagSettings());
            client.Connect("127.0.0.1", 9600);

            yield return Utils.RunTill(() => {
                server.Update();
                client.Update();
                return false;
            }, 1f);

            Assert.IsTrue(client.IsConnected);

            yield return null;
        }
    }
}
