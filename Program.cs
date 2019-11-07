using System;
using FiraSim.Referee.Proto.SimToRef;
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
                }
            };
            var reply = client.simulate(packet);

            channel.ShutdownAsync().Wait();
        }

        private Commands MatchInfo2Commands(MatchInfo matchInfo)
        {
            return new Commands
            {
                RobotCommands =
                {
                    new Command { 
                        Id = 0, 
                        Yellowteam = false, 
                        WheelLeft = matchInfo.BlueRobots[0].wheel.left, 
                        WheelRight = matchInfo.BlueRobots[0].wheel.right 
                    },
                    new Command { 
                        Id = 1, 
                        Yellowteam = false, 
                        WheelLeft = matchInfo.BlueRobots[1].wheel.left, 
                        WheelRight = matchInfo.BlueRobots[1].wheel.right 
                    },
                    new Command { 
                        Id = 2, 
                        Yellowteam = false, 
                        WheelLeft = matchInfo.BlueRobots[2].wheel.left, 
                        WheelRight = matchInfo.BlueRobots[2].wheel.right 
                    },
                    new Command { 
                        Id = 3, 
                        Yellowteam = false, 
                        WheelLeft = matchInfo.BlueRobots[3].wheel.left, 
                        WheelRight = matchInfo.BlueRobots[3].wheel.right 
                    },
                    new Command { 
                        Id = 4, 
                        Yellowteam = false, 
                        WheelLeft = matchInfo.BlueRobots[4].wheel.left, 
                        WheelRight = matchInfo.BlueRobots[4].wheel.right 
                    },
                    
                    new Command { 
                        Id = 0, 
                        Yellowteam = true, 
                        WheelLeft = matchInfo.YellowRobots[0].wheel.left, 
                        WheelRight = matchInfo.YellowRobots[0].wheel.right 
                    },
                    new Command { 
                        Id = 1, 
                        Yellowteam = true, 
                        WheelLeft = matchInfo.YellowRobots[1].wheel.left, 
                        WheelRight = matchInfo.YellowRobots[1].wheel.right 
                    },
                    new Command { 
                        Id = 2, 
                        Yellowteam = true, 
                        WheelLeft = matchInfo.YellowRobots[2].wheel.left, 
                        WheelRight = matchInfo.YellowRobots[2].wheel.right 
                    },
                    new Command { 
                        Id = 3, 
                        Yellowteam = true, 
                        WheelLeft = matchInfo.YellowRobots[3].wheel.left, 
                        WheelRight = matchInfo.YellowRobots[3].wheel.right 
                    },
                    new Command { 
                        Id = 4, 
                        Yellowteam = true, 
                        WheelLeft = matchInfo.YellowRobots[4].wheel.left, 
                        WheelRight = matchInfo.YellowRobots[4].wheel.right 
                    }
                }
            };
        }
        
    }
}
