using System;
using System.Net;
using System.Net.Sockets;
using FiraMessage.SimToRef;
using Google.Protobuf;
using Environment = FiraMessage.SimToRef.Environment;

namespace Referee
{
    public interface ISimulateClient
    {
        Environment Simulate(Packet request);
    }
    
    public class SimulateClient : ISimulateClient, IDisposable
    {
        private readonly UdpClient sendClient;
        private readonly UdpClient receiveClient;
        
        public SimulateClient(string address, int sendPort, int listenPort)
        {
            sendClient = new UdpClient();
            sendClient.Connect(address, sendPort);
            
            receiveClient = new UdpClient(listenPort);
        }
        
        public Environment Simulate(Packet request)
        {
            var bytes = request.ToByteArray();
            sendClient.Send(bytes, bytes.Length);
            var endpoint = new IPEndPoint(IPAddress.Any, 0);
            var receive = receiveClient.Receive(ref endpoint);
            var environment = Environment.Parser.ParseFrom(receive);
            return environment;
        }

        public void Dispose()
        {
            sendClient.Close();
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