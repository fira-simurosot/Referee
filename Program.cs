using System;
using System.Linq;
using FiraMessage.SimToRef;
using FiraMessage.RefToCli;
using Grpc.Core;
using Referee.Simuro5v5;
using Side = FiraMessage.RefToCli.Side;

//using 

namespace Referee
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("This project is still in early stage");
            Channel channel = new Channel("127.0.0.1:50051", ChannelCredentials.Insecure);
            var bluechannel = channel;
            var yellowchannel = channel;

            var clientSimulate = new Simulate.SimulateClient(channel);
            var blueClient = new FiraMessage.RefToCli.Referee.RefereeClient(bluechannel);
            var yellowClient = new FiraMessage.RefToCli.Referee.RefereeClient(yellowchannel);

            var replySimulate = clientSimulate.Simulate(new Packet());
            FoulInfo.Types.PhaseType matchstate = FoulInfo.Types.PhaseType.FirstHalf;

            //TODO: Robot positioning information at the beginning of the game?
            {
                FoulInfo info = new FoulInfo
                {
                    Actor = Side.Self,
                    Phase = FoulInfo.Types.PhaseType.FirstHalf,
                    Type = FoulInfo.Types.FoulType.PlaceKick
                };
                var sendClient = SimEnvironment2CliEnvironment(replySimulate, info);
                var replyClientBall = blueClient.SetBall(sendClient);
                UpdateCliEnvironment(sendClient, replyClientBall);
                var replyblueClientRobots = blueClient.SetFormerRobots(sendClient);
                sendClient.FoulInfo.Actor = Side.Opponent;
                UpdateCliEnvironment(sendClient, replyblueClientRobots, false);
                var replyyellowClientRobots = yellowClient.SetLaterRobots(sendClient);
                UpdateCliEnvironment(sendClient, replyyellowClientRobots, true);

                var matchinfo2 = CliEnvironment2MatchInfo(sendClient);
                //var matchinfo2 = All32MatchInfo(replyClientBall, replyblueClientRobots, replyyellowClientRobots);

                JudgeResult judgeResult = new JudgeResult
                {
                    ResultType = ResultType.NormalMatch,
                    Actor = Simuro5v5.Side.Blue
                };
                matchinfo2.Referee.JudgeAutoPlacement(matchinfo2, judgeResult, judgeResult.Actor);
                replySimulate = clientSimulate.Simulate(MatchInfo2Packet(matchinfo2));
            }

            while (true)
            {
                var matchinfo = SimEnvironment2MatchInfo(replySimulate);
                var judgeResult = matchinfo.Referee.Judge(matchinfo);

                FoulInfo info = new FoulInfo();
                if (judgeResult.ResultType == ResultType.NextPhase)
                {
                    info.Phase = matchstate switch
                    {
                        FoulInfo.Types.PhaseType.FirstHalf => FoulInfo.Types.PhaseType.SecondHalf,
                        FoulInfo.Types.PhaseType.SecondHalf => FoulInfo.Types.PhaseType.Overtime,
                        FoulInfo.Types.PhaseType.Overtime => FoulInfo.Types.PhaseType.PenaltyShootout,
                        FoulInfo.Types.PhaseType.PenaltyShootout => FoulInfo.Types.PhaseType.Stopped,
                        FoulInfo.Types.PhaseType.Stopped => FoulInfo.Types.PhaseType.Stopped,
                        _ => throw new ArgumentOutOfRangeException()
                    };
                    matchstate = info.Phase;
                }
                else
                {
                    if (judgeResult.ResultType == ResultType.NormalMatch)
                    {
                        info.Phase = matchstate;
                    }
                    else
                    {
                        info.Phase = FoulInfo.Types.PhaseType.Stopped;
                    }
                }

                info.Type = judgeResult.ResultType switch
                {
                    ResultType.NormalMatch => FoulInfo.Types.FoulType.PlayOn,
                    ResultType.NextPhase => FoulInfo.Types.FoulType.PlayOn,
                    ResultType.GameOver => FoulInfo.Types.FoulType.PlayOn,
                    ResultType.PlaceKick => FoulInfo.Types.FoulType.PlaceKick,
                    ResultType.GoalKick => FoulInfo.Types.FoulType.GoalKick,
                    ResultType.PenaltyKick => FoulInfo.Types.FoulType.PenaltyKick,
                    ResultType.FreeKickRightTop => FoulInfo.Types.FoulType.FreeBallRightTop,
                    ResultType.FreeKickRightBot => FoulInfo.Types.FoulType.FreeBallRightBot,
                    ResultType.FreeKickLeftTop => FoulInfo.Types.FoulType.FreeBallLeftTop,
                    ResultType.FreeKickLeftBot => FoulInfo.Types.FoulType.FreeBallLeftBot,
                    _ => throw new ArgumentOutOfRangeException()
                };

                if (info.Type == FoulInfo.Types.FoulType.PlayOn)
                {
                    info.Actor = Side.Self;
                    var replyblueClientCommand =
                        blueClient.RunStrategy(SimEnvironment2CliEnvironment(replySimulate, info));
                    var replyyellowClientCommand =
                        yellowClient.RunStrategy(SimEnvironment2CliEnvironment(replySimulate, info));
                    replySimulate =
                        clientSimulate.Simulate(CliCommand2Packet(replyblueClientCommand, replyyellowClientCommand));
                }
                else
                {
                    info.Actor = Side.Self;
                    var sendClient = SimEnvironment2CliEnvironment(replySimulate, info);
                    FiraMessage.Ball replyClientBall = new FiraMessage.Ball();
                    Robots replyblueClientRobots = new Robots();
                    Robots replyyellowClientRobots = new Robots();
                    if (judgeResult.Actor == Simuro5v5.Side.Blue)
                    {
                        replyClientBall = blueClient.SetBall(sendClient);
                        UpdateCliEnvironment(sendClient, replyClientBall);
                        replyblueClientRobots = blueClient.SetFormerRobots(sendClient);
                        sendClient.FoulInfo.Actor = Side.Opponent;
                        UpdateCliEnvironment(sendClient, replyblueClientRobots, false);
                        replyyellowClientRobots = yellowClient.SetLaterRobots(sendClient);
                        UpdateCliEnvironment(sendClient, replyyellowClientRobots, true);
                    }
                    else if (judgeResult.Actor == Simuro5v5.Side.Yellow)
                    {
                        replyClientBall = yellowClient.SetBall(sendClient);
                        UpdateCliEnvironment(sendClient, replyClientBall);
                        replyyellowClientRobots = yellowClient.SetFormerRobots(sendClient);
                        sendClient.FoulInfo.Actor = Side.Opponent;
                        UpdateCliEnvironment(sendClient, replyyellowClientRobots, true);
                        replyblueClientRobots = blueClient.SetLaterRobots(sendClient);
                        UpdateCliEnvironment(sendClient, replyblueClientRobots, false);
                    }

                    //Here are two conversion schemes.
                    //var matchinfo2 = CliEnvironment2MatchInfo(sendClient);
                    var matchinfo2 = All32MatchInfo(replyClientBall, replyblueClientRobots, replyyellowClientRobots);

                    matchinfo2.Referee.JudgeAutoPlacement(matchinfo2, judgeResult, judgeResult.Actor);
                    replySimulate = clientSimulate.Simulate(MatchInfo2Packet(matchinfo2));
                }
            }

            bluechannel.ShutdownAsync().Wait();
            yellowchannel.ShutdownAsync().Wait();
            channel.ShutdownAsync().Wait();
        }

        //TODO: We don't use FiraMessage.SimToRef.Environment.Field. We don't know how to use this var. Field should be const. 
        private static MatchInfo SimEnvironment2MatchInfo(FiraMessage.SimToRef.Environment environment)
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

            var robot = new Robot[10];
            for (int i = 0; i < 10; i++)
            {
                if (i < 5)
                {
                    robot[environment.Frame.RobotsBlue[i].RobotId].pos.x = environment.Frame.RobotsBlue[i].X;
                    robot[environment.Frame.RobotsBlue[i].RobotId].pos.y = environment.Frame.RobotsBlue[i].Y;
                    robot[environment.Frame.RobotsBlue[i].RobotId].rotation =
                        environment.Frame.RobotsBlue[i].Orientation;
                }
                else if (i >= 5)
                {
                    robot[5 + environment.Frame.RobotsYellow[i - 5].RobotId].pos.x =
                        environment.Frame.RobotsYellow[i - 5].X;
                    robot[5 + environment.Frame.RobotsYellow[i - 5].RobotId].pos.y =
                        environment.Frame.RobotsYellow[i - 5].Y;
                    robot[5 + environment.Frame.RobotsYellow[i - 5].RobotId].rotation =
                        environment.Frame.RobotsYellow[i - 5].Orientation;
                }
            }

            return new MatchInfo
            {
                TickMatch = (int) environment.Step,
                TickPhase = (int) (environment.Step % (5 * 60 * 66)),
                MatchPhase = matchPhase,
                Ball = new Ball
                {
                    pos = new Vector2D
                    {
                        x = environment.Frame.Ball.X,
                        y = environment.Frame.Ball.Y
                    }
                },
                BlueRobots = new[] {robot[0], robot[1], robot[2], robot[3], robot[4]},
                YellowRobots = new[] {robot[5], robot[6], robot[7], robot[8], robot[9]}
            };
        }

        private static FiraMessage.RefToCli.Environment SimEnvironment2CliEnvironment(
            FiraMessage.SimToRef.Environment simenvironment, FoulInfo info)
        {
            return new FiraMessage.RefToCli.Environment
            {
                Frame = simenvironment.Frame,
                FoulInfo = info
            };
        }

        private static Packet CliCommand2Packet(FiraMessage.RefToCli.Command bluecommand,
            FiraMessage.RefToCli.Command yellowcommand)
        {
            var cmds = new FiraMessage.SimToRef.Command[10];
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
                    cmds[5 + yellowcommand.Wheels[i - 5].RobotId].Id = (uint) yellowcommand.Wheels[i - 5].RobotId;
                    cmds[5 + yellowcommand.Wheels[i - 5].RobotId].Yellowteam = true;
                    cmds[5 + yellowcommand.Wheels[i - 5].RobotId].WheelLeft = yellowcommand.Wheels[i - 5].Left;
                    cmds[5 + yellowcommand.Wheels[i - 5].RobotId].WheelRight = yellowcommand.Wheels[i - 5].Right;
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
                }
            };
        }

        private static void UpdateCliEnvironment(FiraMessage.RefToCli.Environment cliEnvironment, FiraMessage.Ball ball)
        {
            cliEnvironment.Frame.Ball = ball;
        }

        private static void UpdateCliEnvironment(FiraMessage.RefToCli.Environment cliEnvironment, Robots robots,
            bool isyellow)
        {
            switch (isyellow)
            {
                case false:
                {
                    cliEnvironment.Frame.RobotsBlue[(int) robots.Robots_[0].RobotId] = robots.Robots_[0];
                    cliEnvironment.Frame.RobotsBlue[(int) robots.Robots_[1].RobotId] = robots.Robots_[1];
                    cliEnvironment.Frame.RobotsBlue[(int) robots.Robots_[2].RobotId] = robots.Robots_[2];
                    cliEnvironment.Frame.RobotsBlue[(int) robots.Robots_[3].RobotId] = robots.Robots_[3];
                    cliEnvironment.Frame.RobotsBlue[(int) robots.Robots_[4].RobotId] = robots.Robots_[4];
                    break;
                }
                case true:
                {
                    cliEnvironment.Frame.RobotsYellow[(int) robots.Robots_[0].RobotId] = robots.Robots_[0];
                    cliEnvironment.Frame.RobotsYellow[(int) robots.Robots_[1].RobotId] = robots.Robots_[1];
                    cliEnvironment.Frame.RobotsYellow[(int) robots.Robots_[2].RobotId] = robots.Robots_[2];
                    cliEnvironment.Frame.RobotsYellow[(int) robots.Robots_[3].RobotId] = robots.Robots_[3];
                    cliEnvironment.Frame.RobotsYellow[(int) robots.Robots_[4].RobotId] = robots.Robots_[4];
                    break;
                }
            }
        }

        private static MatchInfo CliEnvironment2MatchInfo(FiraMessage.RefToCli.Environment cliEnvironment)
        {
            var robot = new Robot[10];
            for (int i = 0; i < 10; i++)
            {
                if (i < 5)
                {
                    robot[cliEnvironment.Frame.RobotsBlue[i].RobotId].pos.x = cliEnvironment.Frame.RobotsBlue[i].X;
                    robot[cliEnvironment.Frame.RobotsBlue[i].RobotId].pos.y = cliEnvironment.Frame.RobotsBlue[i].Y;
                    robot[cliEnvironment.Frame.RobotsBlue[i].RobotId].rotation =
                        cliEnvironment.Frame.RobotsBlue[i].Orientation;
                }
                else if (i >= 5)
                {
                    robot[5 + cliEnvironment.Frame.RobotsYellow[i - 5].RobotId].pos.x =
                        cliEnvironment.Frame.RobotsYellow[i - 5].X;
                    robot[5 + cliEnvironment.Frame.RobotsYellow[i - 5].RobotId].pos.y =
                        cliEnvironment.Frame.RobotsYellow[i - 5].Y;
                    robot[5 + cliEnvironment.Frame.RobotsYellow[i - 5].RobotId].rotation =
                        cliEnvironment.Frame.RobotsYellow[i - 5].Orientation;
                }
            }

            return new MatchInfo
            {
                Ball = new Ball
                {
                    pos = new Vector2D
                    {
                        x = cliEnvironment.Frame.Ball.X,
                        y = cliEnvironment.Frame.Ball.Y
                    }
                },
                BlueRobots = new[] {robot[0], robot[1], robot[2], robot[3], robot[4]},
                YellowRobots = new[] {robot[5], robot[6], robot[7], robot[8], robot[9]}
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
                    robot[5 + yellowRobots.Robots_[i - 5].RobotId].pos.x = yellowRobots.Robots_[i - 5].X;
                    robot[5 + yellowRobots.Robots_[i - 5].RobotId].pos.y = yellowRobots.Robots_[i - 5].Y;
                    robot[5 + yellowRobots.Robots_[i - 5].RobotId].rotation = yellowRobots.Robots_[i - 5].Orientation;
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
                    //When Penalty kick, Turnon dictates which robots can't move?
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
                            Turnon = true
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
                            Turnon = true
                        })
                    }
                }
            };
        }
    }
}