using System;
using System.Net;
using System.Net.Sockets;
using FiraMessage.SimToRef;
using Google.Protobuf;
using Microsoft.VisualBasic;
using Environment = FiraMessage.SimToRef.Environment;

namespace Referee
{
    public interface ISimulateClient
    {
        Environment Simulate(Packet request);
    }
    
    public class SimulateClient : ISimulateClient, IDisposable
    {
        private readonly int listenPort;
        private readonly UdpClient client;
        
        public SimulateClient(string address, int port, int listenPort)
        {
            this.listenPort = listenPort;
            client = new UdpClient(0);
            client.Connect(address, port);
        }
        
        public Environment Simulate(Packet request)
        {
            var bytes = request.ToByteArray();
            client.Send(bytes, bytes.Length);
            var endpoint = new IPEndPoint(IPAddress.Any, listenPort);
            var receive = client.Receive(ref endpoint);
            var environment = Environment.Parser.ParseFrom(receive);
            return environment;
        }

        public void Dispose()
        {
            client.Close();
        }
    }

    public class GrpcSimulateClient : ISimulateClient
    {
        private readonly Simulate.SimulateClient client;

        public GrpcSimulateClient(Simulate.SimulateClient client)
        {
            this.client = client;
        }
        
        public Environment Simulate(Packet request)
        {
            return client.Simulate(request);
        }
    }
}