using System;
using FiraSim.Referee.Proto.SimToRef;
using Grpc.Core;

namespace FiraSim.Referee
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("This project is still in early stage");
            Channel channel = new Channel("127.0.0.1:50051", ChannelCredentials.Insecure);

            var client = new Simulate.SimulateClient(channel);
            var packet = new Packet
            {
                Cmd = new Commands
                {
                    RobotCommands =
                    {
                        new Command { Id = 1, Yellowteam = false, WheelLeft = 0, WheelRight = 125 },
                        new Command { Id = 2, Yellowteam = true, WheelLeft = 0, WheelRight = 125 },
                    }
                }
            };
            var reply = client.simulate(packet);

            channel.ShutdownAsync().Wait();
        }
    }
}
