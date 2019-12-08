using System;
using System.Linq;
using FiraMessage;
using FiraMessage.SimToRef;
using FiraMessage.RefToCli;
using Google.Protobuf.Collections;
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
            var blueChannel = new Channel("127.0.0.1:50052", ChannelCredentials.Insecure);
            var yellowChannel = new Channel("127.0.0.1:50053", ChannelCredentials.Insecure);
            var clientSimulate = new Simulate.SimulateClient(channel);
            var blueClient = new FiraMessage.RefToCli.Referee.RefereeClient(blueChannel);
            var yellowClient = new FiraMessage.RefToCli.Referee.RefereeClient(yellowChannel);

            FiraMessage.RefToCli.TeamInfo teamInfo = new FiraMessage.RefToCli.TeamInfo();
            teamInfo.Color = Color.B;
            var blueName = blueClient.Register(teamInfo);
            teamInfo.Color = Color.Y;
            var yellowName = yellowClient.Register(teamInfo);

            MatchInfo matchInfo = new MatchInfo();
            FiraMessage.SimToRef.Environment replySimulate = new FiraMessage.SimToRef.Environment();
            InitSimEnvironment(ref replySimulate);
            bool isSecondHalf = false;
            while (true)
            {
                Console.Out.WriteLine("matchInfo.TickPhase = {0}", matchInfo.TickPhase);
                Console.Out.WriteLine("matchInfo.TickMatch = {0}", matchInfo.TickMatch);

                var judgeResult = matchInfo.Referee.Judge(matchInfo);
                FoulInfo info = RefereeState(judgeResult, matchInfo);
                switch (judgeResult.ResultType)
                {
                    case ResultType.NormalMatch:
                    {
                        var replyBlueClientCommand =
                            blueClient.RunStrategy(SimEnvironment2CliEnvironment(replySimulate, info));
                        var replyYellowClientCommand =
                            yellowClient.RunStrategy(YellowRight(SimEnvironment2CliEnvironment(replySimulate, info)));
                        replySimulate =
                            clientSimulate.Simulate(CliCommand2Packet(replyBlueClientCommand,
                                replyYellowClientCommand, isSecondHalf));
                        break;
                    }
                    case ResultType.NextPhase:
                    {
                        switch (matchInfo.MatchPhase)
                        {
                            case MatchPhase.FirstHalf:
                                matchInfo.MatchPhase = MatchPhase.SecondHalf;
                                isSecondHalf = true;
                                break;
                            case MatchPhase.SecondHalf:
                                matchInfo.MatchPhase = MatchPhase.OverTime;
                                isSecondHalf = false;
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
                    }
                    case ResultType.GameOver:
                    {
                        goto GAME_EXIT;
                    }
                    case ResultType.PlaceKick:
                    case ResultType.GoalKick:
                    case ResultType.PenaltyKick:
                    case ResultType.FreeKickRightTop:
                    case ResultType.FreeKickRightBot:
                    case ResultType.FreeKickLeftTop:
                    case ResultType.FreeKickLeftBot:
                    {
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
                                replyClientBall = yellowClient.SetBall(YellowRight(sendClient));
                                YellowLeft(ref replyClientBall);
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
                                replyYellowClientRobots = yellowClient.SetLaterRobots(YellowRight(sendClient));
                                YellowLeft(ref replyYellowClientRobots);
                                UpdateCliEnvironment(ref sendClient, replyYellowClientRobots, true);
                                break;
                            case Simuro5v5.Side.Yellow:
                                replyYellowClientRobots = yellowClient.SetFormerRobots(YellowRight(sendClient));
                                YellowLeft(ref replyYellowClientRobots);
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
                        replySimulate = clientSimulate.Simulate(MatchInfo2Packet(matchInfo, isSecondHalf));
                        break;
                    }
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                if (isSecondHalf)
                {
                    SecondHalfTransform(ref replySimulate);
                }

                SimEnvironment2MatchInfo(replySimulate, ref matchInfo);
                matchInfo.TickMatch++;
                matchInfo.TickPhase++;
            }

            GAME_EXIT:
            if (matchInfo.Score.BlueScore > matchInfo.Score.YellowScore)
            {
                blueName.Name += " Win";
                Console.Out.WriteLine(blueName.Name);
            }
            else if (matchInfo.Score.BlueScore < matchInfo.Score.YellowScore)
            {
                yellowName.Name += " Win";
                Console.Out.WriteLine(yellowName.Name);
            }
            else
            {
                throw new ArgumentOutOfRangeException();
            }

            blueChannel.ShutdownAsync().Wait();
            yellowChannel.ShutdownAsync().Wait();
            channel.ShutdownAsync().Wait();
        }

        //Initialization FiraMessage.SimToRef.Environment
        private static void InitSimEnvironment(ref FiraMessage.SimToRef.Environment simEnvironment)
        {
            double[] blueX = {30, 80, 95, 80, 30};
            double[] blueY = {60, 60, 0, -60, -60};
            double[] yellowX = {-30, -80, -95, -80, -30};
            double[] yellowY = {60, 60, 0, -60, -60};
            simEnvironment.Step = 0;
            simEnvironment.Field = new Field();
            simEnvironment.Frame = new Frame();
            for (int i = 0; i < 5; i++)
            {
                simEnvironment.Frame.RobotsBlue.Add(new FiraMessage.Robot
                    {RobotId = (uint) i, X = blueX[i], Y = blueY[i], Orientation = 0});
                simEnvironment.Frame.RobotsYellow.Add(new FiraMessage.Robot
                    {RobotId = (uint) i, X = yellowX[i], Y = yellowY[i], Orientation = 0});
            }
        }

        //Message type conversion
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

        //Get the ball position corresponding to the foul type
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

        //Message type conversion
        private static void SimEnvironment2MatchInfo(FiraMessage.SimToRef.Environment environment,
            ref MatchInfo matchInfo)
        {
            /*var matchPhase = MatchPhase.FirstHalf;
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
            }*/

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
            //matchInfo.MatchPhase = matchPhase;
            matchInfo.Ball = new Ball
            {
                pos = new Vector2D
                {
                    x = environment.Frame.Ball.X,
                    y = environment.Frame.Ball.Y
                }
            };
            matchInfo.BlueRobots = robot.Take(5).ToArray();
            matchInfo.YellowRobots = robot.Skip(5).ToArray();
        }

        //Message type conversion
        private static FiraMessage.RefToCli.Environment SimEnvironment2CliEnvironment(
            FiraMessage.SimToRef.Environment simEnvironment, FoulInfo info)
        {
            return new FiraMessage.RefToCli.Environment
            {
                Frame = simEnvironment.Frame,
                FoulInfo = info
            };
        }

        //Message type conversion
        private static Packet CliCommand2Packet(FiraMessage.RefToCli.Command blueCommand,
            FiraMessage.RefToCli.Command yellowCommand, bool isSecondHalf)
        {
            var commands = new FiraMessage.SimToRef.Command[10];
            for (int i = 0; i < 10; i++)
            {
                if (i < 5)
                {
                    commands[blueCommand.Wheels[i].RobotId].Id = (uint) blueCommand.Wheels[i].RobotId;
                    commands[blueCommand.Wheels[i].RobotId].Yellowteam = isSecondHalf;
                    commands[blueCommand.Wheels[i].RobotId].WheelLeft = blueCommand.Wheels[i].Left;
                    commands[blueCommand.Wheels[i].RobotId].WheelRight = blueCommand.Wheels[i].Right;
                }

                if (i >= 5)
                {
                    commands[5 + yellowCommand.Wheels[i - 5].RobotId].Id = (uint) yellowCommand.Wheels[i - 5].RobotId;
                    commands[5 + yellowCommand.Wheels[i - 5].RobotId].Yellowteam = !isSecondHalf;
                    commands[5 + yellowCommand.Wheels[i - 5].RobotId].WheelLeft = yellowCommand.Wheels[i - 5].Left;
                    commands[5 + yellowCommand.Wheels[i - 5].RobotId].WheelRight = yellowCommand.Wheels[i - 5].Right;
                }
            }

            return new Packet
            {
                Cmd = new Commands
                {
                    RobotCommands = { commands }
                }
            };
        }

        //Update message after receiving positioning message
        private static void UpdateCliEnvironment(ref FiraMessage.RefToCli.Environment cliEnvironment,
            FiraMessage.Ball ball)
        {
            cliEnvironment.Frame.Ball = ball;
        }

        //Update message after receiving positioning message
        private static void UpdateCliEnvironment(ref FiraMessage.RefToCli.Environment cliEnvironment, Robots robots,
            bool isYellow)
        {
            if (!isYellow)
            {
                cliEnvironment.Frame.RobotsBlue[(int) robots.Robots_[0].RobotId] = robots.Robots_[0];
                cliEnvironment.Frame.RobotsBlue[(int) robots.Robots_[1].RobotId] = robots.Robots_[1];
                cliEnvironment.Frame.RobotsBlue[(int) robots.Robots_[2].RobotId] = robots.Robots_[2];
                cliEnvironment.Frame.RobotsBlue[(int) robots.Robots_[3].RobotId] = robots.Robots_[3];
                cliEnvironment.Frame.RobotsBlue[(int) robots.Robots_[4].RobotId] = robots.Robots_[4];
            }
            else
            {
                cliEnvironment.Frame.RobotsYellow[(int) robots.Robots_[0].RobotId] = robots.Robots_[0];
                cliEnvironment.Frame.RobotsYellow[(int) robots.Robots_[1].RobotId] = robots.Robots_[1];
                cliEnvironment.Frame.RobotsYellow[(int) robots.Robots_[2].RobotId] = robots.Robots_[2];
                cliEnvironment.Frame.RobotsYellow[(int) robots.Robots_[3].RobotId] = robots.Robots_[3];
                cliEnvironment.Frame.RobotsYellow[(int) robots.Robots_[4].RobotId] = robots.Robots_[4];
            }
        }

        //Message type conversion
        private static void CliEnvironment2MatchInfo(FiraMessage.RefToCli.Environment cliEnvironment,
            ref MatchInfo matchInfo)
        {
            var robotBlue = new Robot[5];
            var robotYellow = new Robot[5];
            for (int i = 0; i < 5; i++)
            {
                robotBlue[cliEnvironment.Frame.RobotsBlue[i].RobotId] = new Robot
                {
                    pos = new Vector2D(cliEnvironment.Frame.RobotsBlue[i].X, cliEnvironment.Frame.RobotsBlue[i].Y),
                    rotation = cliEnvironment.Frame.RobotsBlue[i].Orientation
                };
                robotYellow[cliEnvironment.Frame.RobotsYellow[i].RobotId] = new Robot
                {
                    pos = new Vector2D(cliEnvironment.Frame.RobotsYellow[i].X, cliEnvironment.Frame.RobotsYellow[i].Y),
                    rotation = cliEnvironment.Frame.RobotsYellow[i].Orientation
                };
            }

            matchInfo.Ball = new Ball
            {
                pos = new Vector2D
                {
                    x = cliEnvironment.Frame.Ball.X,
                    y = cliEnvironment.Frame.Ball.Y
                }
            };
            matchInfo.BlueRobots = robotBlue;
            matchInfo.YellowRobots = robotYellow;
        }

        //Message type conversion
        private static Packet MatchInfo2Packet(MatchInfo matchInfo, bool isSecondHalf)
        {
            double flag = isSecondHalf ? -1 : 1;
            return new Packet
            {
                Cmd = new Commands
                {
                    RobotCommands =
                    {
                        matchInfo.BlueRobots.Select((robot, i) => new FiraMessage.SimToRef.Command
                        {
                            Id = (uint) i,
                            Yellowteam = isSecondHalf,
                            WheelLeft = robot.wheel.left,
                            WheelRight = robot.wheel.right
                        }),
                        matchInfo.YellowRobots.Select((robot, i) => new FiraMessage.SimToRef.Command
                        {
                            Id = (uint) i,
                            Yellowteam = !isSecondHalf,
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
                                X = flag * robot.pos.x,
                                Y = flag * robot.pos.y,
                                Orientation = isSecondHalf
                                    ? (robot.rotation > 0 ? robot.rotation - 180 : robot.rotation + 180)
                                    : robot.rotation
                            },
                            Yellowteam = isSecondHalf,
                            Turnon = true
                        }),
                        matchInfo.YellowRobots.Select((robot, i) => new RobotReplacement
                        {
                            Position = new FiraMessage.Robot
                            {
                                RobotId = (uint) i,
                                X = flag * robot.pos.x,
                                Y = flag * robot.pos.y,
                                Orientation = isSecondHalf
                                    ? (robot.rotation > 0 ? robot.rotation - 180 : robot.rotation + 180)
                                    : robot.rotation
                            },
                            Yellowteam = !isSecondHalf,
                            Turnon = true
                        })
                    }
                }
            };
        }

        //In the second half of the game, the information of the two teams will be converted before the information of the simulation is transmitted.
        private static void SecondHalfTransform(ref FiraMessage.SimToRef.Environment replySimulate)
        {
            var clone = replySimulate;
            replySimulate.Frame = new Frame
            {
                Ball = new FiraMessage.Ball
                {
                    X = -clone.Frame.Ball.X,
                    Y = -clone.Frame.Ball.Y
                },
                RobotsBlue =
                {
                    clone.Frame.RobotsYellow.Select((robot, i) => new FiraMessage.Robot
                    {
                        RobotId = robot.RobotId,
                        X = -robot.X,
                        Y = -robot.Y,
                        Orientation = robot.Orientation > 0 ? robot.Orientation - 180 : robot.Orientation + 180
                    })
                },
                RobotsYellow =
                {
                    clone.Frame.RobotsBlue.Select((robot, i) => new FiraMessage.Robot
                    {
                        RobotId = robot.RobotId,
                        X = -robot.X,
                        Y = -robot.Y,
                        Orientation = robot.Orientation > 0 ? robot.Orientation - 180 : robot.Orientation + 180
                    })
                }
            };
        }

        //Before transmitting information to the Yellow client, the coordinates are converted to yellow on the right and blue on the left.
        private static FiraMessage.RefToCli.Environment YellowRight(FiraMessage.RefToCli.Environment cliEnvironment)
        {
            return new FiraMessage.RefToCli.Environment
            {
                FoulInfo = cliEnvironment.FoulInfo,
                Frame = new Frame
                {
                    Ball = new FiraMessage.Ball
                    {
                        X = -cliEnvironment.Frame.Ball.X,
                        Y = -cliEnvironment.Frame.Ball.Y
                    },
                    RobotsBlue =
                    {
                        cliEnvironment.Frame.RobotsBlue.Select((robot, i) => new FiraMessage.Robot
                        {
                            RobotId = robot.RobotId,
                            X = -robot.X,
                            Y = -robot.Y,
                            Orientation = robot.Orientation > 0 ? robot.Orientation - 180 : robot.Orientation + 180
                        })
                    },
                    RobotsYellow =
                    {
                        cliEnvironment.Frame.RobotsYellow.Select((robot, i) => new FiraMessage.Robot
                        {
                            RobotId = robot.RobotId,
                            X = -robot.X,
                            Y = -robot.Y,
                            Orientation = robot.Orientation > 0 ? robot.Orientation - 180 : robot.Orientation + 180
                        })
                    }
                }
            };
        }

        //The coordinates of the positioning information received from the Yellow client are converted to blue on the right and yellow on the left
        private static void YellowLeft(ref FiraMessage.Ball ball)
        {
            ball.X = -ball.X;
            ball.Y = -ball.Y;
        }

        //The coordinates of the positioning information received from the Yellow client are converted to blue on the right and yellow on the left
        private static void YellowLeft(ref Robots robots)
        {
            robots = new Robots
            {
                Robots_ =
                {
                    robots.Robots_.Select((robot, i) => new FiraMessage.Robot
                    {
                        RobotId = robot.RobotId,
                        X = -robot.X,
                        Y = -robot.Y,
                        Orientation = robot.Orientation > 0 ? robot.Orientation - 180 : robot.Orientation + 180
                    })
                }
            };
        }
    }
}