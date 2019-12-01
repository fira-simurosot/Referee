using System;
using System.Linq;
using FiraMessage.SimToRef;
using FiraMessage.RefToCli;
using Grpc.Core;
using Referee.Simuro5v5;
using Ball = Referee.Simuro5v5.Ball;
using Robot = Referee.Simuro5v5.Robot;
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
            var blueChannel = channel;
            var yellowChannel = channel;
            var clientSimulate = new Simulate.SimulateClient(channel);
            var blueClient = new FiraMessage.RefToCli.Referee.RefereeClient(blueChannel);
            var yellowClient = new FiraMessage.RefToCli.Referee.RefereeClient(yellowChannel);

            MatchInfo matchInfo = new MatchInfo();
            FiraMessage.SimToRef.Environment replySimulate = new FiraMessage.SimToRef.Environment();
            while (true)
            {
                var judgeResult = matchInfo.Referee.Judge(matchInfo);
                FoulInfo info = RefereeState(judgeResult, matchInfo);
                switch (judgeResult.ResultType)
                {
                    case ResultType.NormalMatch:
                        var replyBlueClientCommand =
                            blueClient.RunStrategy(SimEnvironment2CliEnvironment(replySimulate, info));
                        var replyYellowClientCommand =
                            yellowClient.RunStrategy(SimEnvironment2CliEnvironment(replySimulate, info));
                        replySimulate =
                            clientSimulate.Simulate(CliCommand2Packet(replyBlueClientCommand,
                                replyYellowClientCommand));
                        break;
                    case ResultType.NextPhase:
                        switch (matchInfo.MatchPhase)
                        {
                            case MatchPhase.FirstHalf:
                                matchInfo.MatchPhase = MatchPhase.SecondHalf;
                                break;
                            case MatchPhase.SecondHalf:
                                matchInfo.MatchPhase = MatchPhase.OverTime;
                                break;
                            case MatchPhase.OverTime:
                            case MatchPhase.Penalty:
                                matchInfo.MatchPhase = MatchPhase.Penalty;
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }

                        matchInfo.TickPhase = 0;
                        continue;
                    case ResultType.GameOver:
                        goto GAMEOVER_EXIT;
                    default:
                        var sendClient = SimEnvironment2CliEnvironment(replySimulate, info);
                        FiraMessage.Ball replyClientBall;
                        Robots replyBlueClientRobots;
                        Robots replyYellowClientRobots;
                        switch (judgeResult.WhosBall)
                        {
                            case Simuro5v5.Side.Nobody:
                                replyClientBall = FoulBallPosition(info, judgeResult);
                                break;
                            case Simuro5v5.Side.Blue:
                                replyClientBall = blueClient.SetBall(sendClient);
                                break;
                            case Simuro5v5.Side.Yellow:
                                replyClientBall = yellowClient.SetBall(sendClient);
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }

                        UpdateCliEnvironment(ref sendClient, replyClientBall);
                        switch (judgeResult.WhoisFirst)
                        {
                            case Simuro5v5.Side.Blue:
                                replyBlueClientRobots = blueClient.SetFormerRobots(sendClient);
                                sendClient.FoulInfo.Actor = Side.Opponent;
                                UpdateCliEnvironment(ref sendClient, replyBlueClientRobots, false);
                                replyYellowClientRobots = yellowClient.SetLaterRobots(sendClient);
                                UpdateCliEnvironment(ref sendClient, replyYellowClientRobots, true);
                                break;
                            case Simuro5v5.Side.Yellow:
                                replyYellowClientRobots = yellowClient.SetFormerRobots(sendClient);
                                sendClient.FoulInfo.Actor = Side.Opponent;
                                UpdateCliEnvironment(ref sendClient, replyYellowClientRobots, true);
                                replyBlueClientRobots = blueClient.SetLaterRobots(sendClient);
                                UpdateCliEnvironment(ref sendClient, replyBlueClientRobots, false);
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }

                        CliEnvironment2MatchInfo(sendClient, ref matchInfo);
                        matchInfo.Referee.JudgeAutoPlacement(matchInfo, judgeResult, judgeResult.Actor);
                        replySimulate = clientSimulate.Simulate(MatchInfo2Packet(matchInfo));
                        break;
                }

                SimEnvironment2MatchInfo(replySimulate, ref matchInfo);
                matchInfo.TickMatch++;
                matchInfo.TickPhase++;
            }

            GAMEOVER_EXIT:
            if (matchInfo.Score.BlueScore > matchInfo.Score.YellowScore)
            {
                //Blue win
            }
            else if (matchInfo.Score.BlueScore < matchInfo.Score.YellowScore)
            {
                //Yellow win
            }
            else
            {
                throw new ArgumentOutOfRangeException();
            }

            blueChannel.ShutdownAsync().Wait();
            yellowChannel.ShutdownAsync().Wait();
            channel.ShutdownAsync().Wait();
        }

        private static FoulInfo RefereeState(JudgeResult judgeResult, MatchInfo matchInfo)
        {
            //Don't use FoulInfo.Types.PhaseType.Stopped.
            FoulInfo info = new FoulInfo
            {
                Actor = Side.Self,
                Phase = matchInfo.MatchPhase switch
                {
                    MatchPhase.FirstHalf => FoulInfo.Types.PhaseType.FirstHalf,
                    MatchPhase.SecondHalf => FoulInfo.Types.PhaseType.SecondHalf,
                    MatchPhase.OverTime => FoulInfo.Types.PhaseType.Overtime,
                    MatchPhase.Penalty => FoulInfo.Types.PhaseType.PenaltyShootout,
                    _ => throw new ArgumentOutOfRangeException()
                },
                Type = judgeResult.ResultType switch
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
                }
            };

            return info;
        }

        private static FiraMessage.Ball FoulBallPosition(FoulInfo info, JudgeResult judgeResult)
        {
            FiraMessage.Ball replyClientBall = new FiraMessage.Ball();
            switch (info.Type)
            {
                case FoulInfo.Types.FoulType.PlayOn:
                    break;
                case FoulInfo.Types.FoulType.PlaceKick:
                    replyClientBall.X = Const.PlaceKickX;
                    replyClientBall.Y = Const.PlaceKickY;
                    break;
                case FoulInfo.Types.FoulType.PenaltyKick:
                    if (judgeResult.WhoisFirst == Simuro5v5.Side.Blue)
                    {
                        replyClientBall.X = Const.PenaltyKickRightX;
                        replyClientBall.Y = Const.PenaltyKickRightY;
                    }
                    else if (judgeResult.WhoisFirst == Simuro5v5.Side.Yellow)
                    {
                        replyClientBall.X = Const.PenaltyKickLeftX;
                        replyClientBall.Y = Const.PenaltyKickLeftY;
                    }

                    break;
                case FoulInfo.Types.FoulType.FreeKick:
                    break;
                case FoulInfo.Types.FoulType.GoalKick:
                    break;
                case FoulInfo.Types.FoulType.FreeBallLeftTop:
                    replyClientBall.X = Const.FreeBallLeftTopX;
                    replyClientBall.Y = Const.FreeBallLeftTopY;
                    break;
                case FoulInfo.Types.FoulType.FreeBallRightTop:
                    replyClientBall.X = Const.FreeBallRightTopX;
                    replyClientBall.Y = Const.FreeBallRightTopY;
                    break;
                case FoulInfo.Types.FoulType.FreeBallLeftBot:
                    replyClientBall.X = Const.FreeBallLeftBotX;
                    replyClientBall.Y = Const.FreeBallLeftBotY;
                    break;
                case FoulInfo.Types.FoulType.FreeBallRightBot:
                    replyClientBall.X = Const.FreeBallRightBotX;
                    replyClientBall.Y = Const.FreeBallRightBotY;
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }

            return replyClientBall;
        }

        //TODO: We don't use FiraMessage.SimToRef.Environment.Field. We don't know how to use this var. Field should be const. 
        private static void SimEnvironment2MatchInfo(FiraMessage.SimToRef.Environment environment,
            ref MatchInfo matchInfo)
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

            //TODO: How to use environment.Step?
            //matchInfo.TickMatch = (int) environment.Step;
            //matchInfo.TickPhase = (int) (environment.Step % (5 * 60 * 66));
            matchInfo.MatchPhase = matchPhase;
            matchInfo.Ball = new Ball
            {
                pos = new Vector2D
                {
                    x = environment.Frame.Ball.X,
                    y = environment.Frame.Ball.Y
                }
            };
            matchInfo.BlueRobots = new[] {robot[0], robot[1], robot[2], robot[3], robot[4]};
            matchInfo.YellowRobots = new[] {robot[5], robot[6], robot[7], robot[8], robot[9]};
        }

        private static FiraMessage.RefToCli.Environment SimEnvironment2CliEnvironment(
            FiraMessage.SimToRef.Environment simEnvironment, FoulInfo info)
        {
            return new FiraMessage.RefToCli.Environment
            {
                Frame = simEnvironment.Frame,
                FoulInfo = info
            };
        }

        private static Packet CliCommand2Packet(FiraMessage.RefToCli.Command blueCommand,
            FiraMessage.RefToCli.Command yellowCommand)
        {
            var commands = new FiraMessage.SimToRef.Command[10];
            for (int i = 0; i < 10; i++)
            {
                if (i < 5)
                {
                    commands[blueCommand.Wheels[i].RobotId].Id = (uint) blueCommand.Wheels[i].RobotId;
                    commands[blueCommand.Wheels[i].RobotId].Yellowteam = false;
                    commands[blueCommand.Wheels[i].RobotId].WheelLeft = blueCommand.Wheels[i].Left;
                    commands[blueCommand.Wheels[i].RobotId].WheelRight = blueCommand.Wheels[i].Right;
                }

                if (i >= 5)
                {
                    commands[5 + yellowCommand.Wheels[i - 5].RobotId].Id = (uint) yellowCommand.Wheels[i - 5].RobotId;
                    commands[5 + yellowCommand.Wheels[i - 5].RobotId].Yellowteam = true;
                    commands[5 + yellowCommand.Wheels[i - 5].RobotId].WheelLeft = yellowCommand.Wheels[i - 5].Left;
                    commands[5 + yellowCommand.Wheels[i - 5].RobotId].WheelRight = yellowCommand.Wheels[i - 5].Right;
                }
            }

            return new Packet
            {
                Cmd = new Commands
                {
                    RobotCommands =
                    {
                        commands[0], commands[1], commands[2], commands[3], commands[4],
                        commands[5], commands[6], commands[7], commands[8], commands[9]
                    }
                }
            };
        }

        private static void UpdateCliEnvironment(ref FiraMessage.RefToCli.Environment cliEnvironment,
            FiraMessage.Ball ball)
        {
            cliEnvironment.Frame.Ball = ball;
        }

        private static void UpdateCliEnvironment(ref FiraMessage.RefToCli.Environment cliEnvironment, Robots robots,
            bool isYellow)
        {
            switch (isYellow)
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

        private static void CliEnvironment2MatchInfo(FiraMessage.RefToCli.Environment cliEnvironment,
            ref MatchInfo matchInfo)
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

            matchInfo.Ball = new Ball
            {
                pos = new Vector2D
                {
                    x = cliEnvironment.Frame.Ball.X,
                    y = cliEnvironment.Frame.Ball.Y
                }
            };
            matchInfo.BlueRobots = new[] {robot[0], robot[1], robot[2], robot[3], robot[4]};
            matchInfo.YellowRobots = new[] {robot[5], robot[6], robot[7], robot[8], robot[9]};
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