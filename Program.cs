using System;
using System.Linq;
using FiraMessage.SimToRef;
using Grpc.Core;
using Referee.Simuro5v5;

//using 

namespace Referee
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
                },
                Replace = new Replacement()
            };
            var reply = client.Simulate(packet);

            channel.ShutdownAsync().Wait();
        }

        private Commands MatchInfo2Commands(MatchInfo matchInfo)
        {
            return new Commands
            {
                RobotCommands =
                {
                    matchInfo.BlueRobots.Select((robot, i) => new Command
                    {
                        Id = (uint)i,
                        Yellowteam = false, 
                        WheelLeft = robot.wheel.left, 
                        WheelRight = robot.wheel.right 
                    }),
                    matchInfo.YellowRobots.Select((robot, i) => new Command
                    {
                        Id = (uint)i,
                        Yellowteam = true, 
                        WheelLeft = robot.wheel.left, 
                        WheelRight = robot.wheel.right 
                    })
                }
            };
        }
    }
}
