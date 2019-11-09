using System;
using System.Linq;
using FiraMessage.SimToRef;
using FiraMessage.RefToCli;
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

            var clientSimulate = new Simulate.SimulateClient(channel);
            var packet = new Packet
            {
                Cmd = new Commands(),
                Replace = new Replacement()
            };
            var replySimulate = clientSimulate.Simulate(packet);
            var matchinfo = Environment2MatchInfo(replySimulate);
            var judgeResult = matchinfo.Referee.Judge(matchinfo);
            var info = judgeResult.ResultType switch
            {
                ResultType.NormalMatch => FoulInfo.Types.FoulType.PlayOn,
                ResultType.NextPhase => FoulInfo.Types.FoulType.PlayOn,
                ResultType.GameOver => FoulInfo.Types.FoulType.PlayOn,
                ResultType.PlaceKick => FoulInfo.Types.FoulType.PlaceKick,
                ResultType.GoalKick => FoulInfo.Types.FoulType.GoalKick,
                ResultType.PenaltyKick => FoulInfo.Types.FoulType.PenaltyKick,
                ResultType.FreeKickRightTop => FoulInfo.Types.FoulType.FreeBallTop,
                ResultType.FreeKickRightBot => FoulInfo.Types.FoulType.FreeBallBot,
                ResultType.FreeKickLeftTop => FoulInfo.Types.FoulType.FreeBallTop,
                ResultType.FreeKickLeftBot => FoulInfo.Types.FoulType.FreeBallBot,
                _ => throw new ArgumentOutOfRangeException()
            };

            var bluechannel = channel;
            var yellowchannel = channel;
            var blueClient = new FiraMessage.RefToCli.Referee.RefereeClient(bluechannel);
            var yellowClient = new FiraMessage.RefToCli.Referee.RefereeClient(yellowchannel);
            if (info == FoulInfo.Types.FoulType.PlayOn)
            {
                var replyblueClientCommand = 
                    blueClient.RunStrategy(SimEnvironment2CliEnvironment(replySimulate, info));
                var replyyellowClientCommand =
                    yellowClient.RunStrategy(SimEnvironment2CliEnvironment(replySimulate, info));

                replySimulate =
                    clientSimulate.Simulate(CliCommand2Packet(replyblueClientCommand, replyyellowClientCommand));
                //END Next to the function's begin( var replySimulate = clientSimulate.Simulate(packet); ).
            }
            else
            {
                FiraMessage.Ball replyClientBall = new FiraMessage.Ball();
                Robots replyblueClientRobots = new Robots();
                Robots replyyellowClientRobots = new Robots();
                if (judgeResult.Actor == Side.Blue)
                {
                    replyClientBall = blueClient.SetBall(SimEnvironment2CliEnvironment(replySimulate, info));
                    replyblueClientRobots = blueClient.SetFormerRobots(SimEnvironment2CliEnvironment(replySimulate, info));
                    replyyellowClientRobots = yellowClient.SetLaterRobots(SimEnvironment2CliEnvironment(replySimulate, info));
                }
                else if (judgeResult.Actor == Side.Yellow)
                {
                    replyClientBall = yellowClient.SetBall(SimEnvironment2CliEnvironment(replySimulate, info));
                    replyyellowClientRobots = yellowClient.SetFormerRobots(SimEnvironment2CliEnvironment(replySimulate, info));
                    replyblueClientRobots = blueClient.SetLaterRobots(SimEnvironment2CliEnvironment(replySimulate, info));
                }
                var matchinfo2 = All32MatchInfo(replyClientBall, replyblueClientRobots, replyyellowClientRobots);
                matchinfo.Referee.JudgeAutoPlacement(matchinfo, judgeResult, judgeResult.Actor);
                
                replySimulate = clientSimulate.Simulate(MatchInfo2Packet(matchinfo2));
                //END Next to the function's begin( var replySimulate = clientSimulate.Simulate(packet); ).
            }

            channel.ShutdownAsync().Wait();
        }

        private static MatchInfo Environment2MatchInfo(FiraMessage.SimToRef.Environment environment)
        {
            var matchPhase = MatchPhase.FirstHalf;
            if (environment.Step > 5 * 60 * 6)
            {
                matchPhase = MatchPhase.SecondHalf;
            }
            else if (environment.Step > 2 * 5 * 60 * 66)
            {
                matchPhase = MatchPhase.OverTime;
            }
            else if (environment.Step > (2 * 5 + 3) * 60 * 66)
            {
                matchPhase = MatchPhase.Penalty;
            }

            var blueRobot = new Robot[5];
            var yellowRobot = new Robot[5];

            for (int i = 0; i < 5; i++)
            {
                blueRobot[environment.Frame.RobotsBlue[i].RobotId].pos.x = environment.Frame.RobotsBlue[i].X;
                blueRobot[environment.Frame.RobotsBlue[i].RobotId].pos.y = environment.Frame.RobotsBlue[i].Y;
                blueRobot[environment.Frame.RobotsBlue[i].RobotId].rotation =
                    environment.Frame.RobotsBlue[i].Orientation;

                yellowRobot[environment.Frame.RobotsBlue[i].RobotId].pos.x = environment.Frame.RobotsYellow[i].X;
                yellowRobot[environment.Frame.RobotsBlue[i].RobotId].pos.y = environment.Frame.RobotsYellow[i].Y;
                yellowRobot[environment.Frame.RobotsBlue[i].RobotId].rotation =
                    environment.Frame.RobotsYellow[i].Orientation;
            }

            return new MatchInfo
            {
                TickMatch = (int) environment.Step,
                TickPhase = (int) (environment.Step % 5 * 60 * 66),
                MatchPhase = matchPhase,
                Ball = new Ball
                {
                    pos = new Vector2D
                    {
                        x = environment.Frame.Ball.X,
                        y = environment.Frame.Ball.Y
                    }
                },

                BlueRobots = new[]
                {
                    blueRobot[0], blueRobot[1], blueRobot[2], blueRobot[3], blueRobot[4]
                },

                YellowRobots = new[]
                {
                    yellowRobot[0], yellowRobot[1], yellowRobot[2], yellowRobot[3], yellowRobot[4]
                }
            };
        }

        private static FiraMessage.RefToCli.Environment SimEnvironment2CliEnvironment(
            FiraMessage.SimToRef.Environment simenvironment, FoulInfo.Types.FoulType info)
        {
            return new FiraMessage.RefToCli.Environment
            {
                Frame = simenvironment.Frame,
                FoulInfo = new FoulInfo
                {
                    FoulType = info
                }
            };
        }

        private static Packet CliCommand2Packet(FiraMessage.RefToCli.Command bluecommand,
            FiraMessage.RefToCli.Command yellowcommand)
        {
            var cmds = new FiraMessage.SimToRef.Command [10];
            for (int i = 0; i < 10; i++)
            {
                if (i < 5)
                {
                    cmds[bluecommand.Wheels[i].RobotId].Id = (uint) bluecommand.Wheels[i].RobotId;
                    cmds[bluecommand.Wheels[i].RobotId].Yellowteam = false;
                    cmds[bluecommand.Wheels[i].RobotId].WheelLeft = bluecommand.Wheels[i].Left;
                    cmds[bluecommand.Wheels[i].RobotId].WheelRight = bluecommand.Wheels[i].Right;
                }

                if (i >= 5)
                {
                    cmds[yellowcommand.Wheels[i - 5].RobotId].Id = (uint) yellowcommand.Wheels[i - 5].RobotId;
                    cmds[yellowcommand.Wheels[i - 5].RobotId].Yellowteam = true;
                    cmds[yellowcommand.Wheels[i - 5].RobotId].WheelLeft = yellowcommand.Wheels[i - 5].Left;
                    cmds[yellowcommand.Wheels[i - 5].RobotId].WheelRight = yellowcommand.Wheels[i - 5].Right;
                }
            }

            return new Packet
            {
                Cmd = new Commands
                {
                    RobotCommands =
                    {
                        cmds[0], cmds[1], cmds[2], cmds[3], cmds[4],
                        cmds[5], cmds[6], cmds[7], cmds[8], cmds[9]
                    }
                },
            };
        }

        private static MatchInfo All32MatchInfo(FiraMessage.Ball ball, Robots blueRobots, Robots yellowRobots)
        {
            var robot = new Robot[10];
            for (int i = 0; i < 10; i++)
            {
                if (i < 5)
                {
                    robot[blueRobots.Robots_[i].RobotId].pos.x = blueRobots.Robots_[i].X;
                    robot[blueRobots.Robots_[i].RobotId].pos.y = blueRobots.Robots_[i].Y;
                    robot[blueRobots.Robots_[i].RobotId].rotation = blueRobots.Robots_[i].Orientation;
                }
                else if (i >= 5)
                {
                    robot[yellowRobots.Robots_[i].RobotId].pos.x = yellowRobots.Robots_[i].X;
                    robot[yellowRobots.Robots_[i].RobotId].pos.y = yellowRobots.Robots_[i].Y;
                    robot[yellowRobots.Robots_[i].RobotId].rotation = yellowRobots.Robots_[i].Orientation;
                }
            }

            return new MatchInfo
            {
                Ball = new Ball
                {
                    pos = new Vector2D
                    {
                        x = ball.X,
                        y = ball.Y
                    }
                },
                BlueRobots = new[] {robot[0], robot[1], robot[2], robot[3], robot[4]},
                YellowRobots = new[] {robot[5], robot[6], robot[7], robot[8], robot[9]}
            };
        }

        private static Packet MatchInfo2Packet(MatchInfo matchInfo)
        {
            return new Packet
            {
                Cmd = new Commands
                {
                    RobotCommands =
                    {
                        matchInfo.BlueRobots.Select((robot, i) => new FiraMessage.SimToRef.Command
                        {
                            Id = (uint) i,
                            Yellowteam = false,
                            WheelLeft = robot.wheel.left,
                            WheelRight = robot.wheel.right
                        }),
                        matchInfo.YellowRobots.Select((robot, i) => new FiraMessage.SimToRef.Command
                        {
                            Id = (uint) i,
                            Yellowteam = true,
                            WheelLeft = robot.wheel.left,
                            WheelRight = robot.wheel.right
                        })
                    }
                },
                Replace = new Replacement
                {
                    Ball = new BallReplacement
                    {
                        X = matchInfo.Ball.pos.x,
                        Y = matchInfo.Ball.pos.y,
                        Vx = matchInfo.Ball.linearVelocity.x,
                        Vy = matchInfo.Ball.linearVelocity.y
                    },

                    //TODO Don't know what is Turnon in RobotReplacement, and how RobotID in FiraMessage.Robot is sorted(Is the top five blue or yellow?).
                    Robots =
                    {
                        matchInfo.BlueRobots.Select((robot, i) => new RobotReplacement
                        {
                            Position = new FiraMessage.Robot
                            {
                                RobotId = (uint) i,
                                X = robot.pos.x,
                                Y = robot.pos.y,
                                Orientation = robot.rotation
                            },
                            Yellowteam = false,
                            Turnon = false
                        }),
                        matchInfo.YellowRobots.Select((robot, i) => new RobotReplacement
                        {
                            Position = new FiraMessage.Robot
                            {
                                RobotId = (uint) i,
                                X = robot.pos.x,
                                Y = robot.pos.y,
                                Orientation = robot.rotation
                            },
                            Yellowteam = true,
                            Turnon = false
                        })
                    }
                }
            };
        }

        private static MatchInfo Packet2MatchInfo(Packet packet)
        {
            uint blueId = 0;
            uint yellowId = 5;
            var robot = new Robot[10];
            for (int i = 0; i < 10; i++)
            {
                //TODO Don't know what is Turnon in RobotReplacement, and how RobotID in FiraMessage.Robot is sorted.
                if (!packet.Replace.Robots[i].Yellowteam)
                {
                    robot[blueId].pos.x = packet.Replace.Robots[i].Position.X;
                    robot[blueId].pos.y = packet.Replace.Robots[i].Position.Y;
                    robot[blueId].rotation = packet.Replace.Robots[i].Position.Orientation;
                    blueId++;
                }
                else
                {
                    robot[yellowId].pos.x = packet.Replace.Robots[i].Position.X;
                    robot[yellowId].pos.y = packet.Replace.Robots[i].Position.Y;
                    robot[yellowId].rotation = packet.Replace.Robots[i].Position.Orientation;
                    yellowId++;
                }

                if (!packet.Cmd.RobotCommands[i].Yellowteam)
                {
                    robot[packet.Cmd.RobotCommands[i].Id].wheel.left = packet.Cmd.RobotCommands[i].WheelLeft;
                    robot[packet.Cmd.RobotCommands[i].Id].wheel.right = packet.Cmd.RobotCommands[i].WheelRight;
                }
                else
                {
                    robot[packet.Cmd.RobotCommands[i].Id + 5].wheel.left = packet.Cmd.RobotCommands[i].WheelLeft;
                    robot[packet.Cmd.RobotCommands[i].Id + 5].wheel.right = packet.Cmd.RobotCommands[i].WheelRight;
                }
            }

            return new MatchInfo
            {
                Ball = new Ball
                {
                    pos = new Vector2D
                    {
                        x = packet.Replace.Ball.X,
                        y = packet.Replace.Ball.Y
                    },
                    linearVelocity = new Vector2D
                    {
                        x = packet.Replace.Ball.Vx,
                        y = packet.Replace.Ball.Vy
                    }
                },

                BlueRobots = new[]
                {
                    robot[0], robot[1], robot[2], robot[3], robot[4]
                },

                YellowRobots = new[]
                {
                    robot[5], robot[6], robot[7], robot[8], robot[9]
                }
            };
        }
        
    }
}