using System;
using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;

namespace Cube.Transport.Tests {
    public class TestReactor {
#if CLIENT && SERVER
        [Test]
        public void TestClientReactor() {
            var client = new LocalClientInterface();
            var reactor = new ClientReactor(client);

            bool messageHandlerCalled = false;
            reactor.AddHandler(1, (BitStream bs) => {
                messageHandlerCalled = true;
            });

            var message = new BitStream();
            message.WriteByte(1);
            client.EnqueueMessage(message);

            reactor.Update();

            Assert.IsTrue(messageHandlerCalled);
        }

        [Test]
        public void TestServerReactor() {
            var server = new LocalServerInterface();
            var reactor = new ServerReactor(server);
            
            bool messageHandlerCalled = false;
            reactor.AddHandler(1, (Connection connection, BitStream bs) => {
                messageHandlerCalled = true;
            });

            var message = new BitStream();
            message.WriteByte(1);
            server.EnqueueMessage(Connection.Invalid, message);

            reactor.Update();

            Assert.IsTrue(messageHandlerCalled);
        }
#endif
    }
}
