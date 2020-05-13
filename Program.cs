using System;
using System.Linq;
using FiraMessage;
using FiraMessage.SimToRef;
using FiraMessage.RefToCli;
using Google.Protobuf.Collections;
using Grpc.Core;
using Referee.Simuro5v5;
using Ball = Referee.Simuro5v5.Ball;
using Environment = FiraMessage.SimToRef.Environment;
using Robot = Referee.Simuro5v5.Robot;
using Side = FiraMessage.RefToCli.Side;

//using 

namespace Referee
{
    static class Program
    {
        private static void Main(string[] args)
        {
            // Get gRPC client
            Console.WriteLine("This project is still in early stage");
            var blueChannel = new Channel("127.0.0.1:50052", ChannelCredentials.Insecure);
            var yellowChannel = new Channel("127.0.0.1:50053", ChannelCredentials.Insecure);
            var blueClient = new FiraMessage.RefToCli.Referee.RefereeClient(blueChannel);
            var yellowClient = new FiraMessage.RefToCli.Referee.RefereeClient(yellowChannel);
            
            //var simulationChannel = new Channel("127.0.0.1:50051", ChannelCredentials.Insecure);
            //var simulationClient = new GrpcSimulateClient(new Simulate.SimulateClient(simulationChannel));
            using var simulationClient = new SimulateClient("127.0.0.1", 50051, 50054);

            try
            {
                // Register strategy
                var blueTeamInfo = new FiraMessage.RefToCli.TeamInfo {Color = Color.B};
                var blueName = blueClient.Register(blueTeamInfo);
                var yellowTeamInfo = new FiraMessage.RefToCli.TeamInfo {Color = Color.Y};
                var yellowName = yellowClient.Register(yellowTeamInfo);

                var matchInfo = MainLoop(blueClient, yellowClient, simulationClient);

                if (matchInfo.Score.BlueScore > matchInfo.Score.YellowScore)
                {
                    blueName.Name += " Win";
                    Console.WriteLine(blueName.Name);
                }
                else if (matchInfo.Score.BlueScore < matchInfo.Score.YellowScore)
                {
                    yellowName.Name += " Win";
                    Console.WriteLine(yellowName.Name);
                }
                else
                {
                    throw new ArgumentOutOfRangeException();
                }
            }
            catch (RpcException ex)
            {
                if (ex.Status.StatusCode == StatusCode.Unavailable)
                {
                    Console.WriteLine("An agent client has closed.");
                }
            }
            finally
            {
                blueChannel.ShutdownAsync().Wait();
                yellowChannel.ShutdownAsync().Wait();
                //simulationChannel.ShutdownAsync().Wait();
            }
        }

        private static MatchInfo MainLoop(FiraMessage.RefToCli.Referee.RefereeClient blueClient,
            FiraMessage.RefToCli.Referee.RefereeClient yellowClient, ISimulateClient simulationClient)
        {
            var matchInfo = new MatchInfo();
            var environment = InitSimEnvironment();
            bool isSecondHalf = false;
            while (true)
            {
                try
                {
                    //TODO: just for test
                    Console.Out.WriteLine("MatchPhase = {0}", matchInfo.MatchPhase);
                    Console.Out.WriteLine("matchInfo.TickPhase = {0}", matchInfo.TickPhase);
                    Console.Out.WriteLine("matchInfo.TickMatch = {0}", matchInfo.TickMatch);
                    if (matchInfo.MatchPhase == MatchPhase.Penalty)
                    {
                        Console.Out.WriteLine("BlueScore = {0}", matchInfo.Score.BlueScore);
                        Console.Out.WriteLine("YellowScore = {0}", matchInfo.Score.YellowScore);
                    }

                    // Get JudgeResult from the judge
                    var judgeResult = matchInfo.Referee.Judge(matchInfo);
                    FoulInfo info = ExtractFoulInfo(judgeResult, matchInfo);
                    switch (judgeResult.ResultType)
                    {
                        case ResultType.NormalMatch:
                        {
                            // The game continues, move to next frame
                            var blueCliEnvironment = EnvironmentSimToCli(environment, info);
                            var blueClientCommandReply = blueClient.RunStrategy(blueCliEnvironment);

                            var yellowCliEnvironment = ConvertToRight(EnvironmentSimToCli(environment, info));
                            var yellowClientCommandReply = yellowClient.RunStrategy(yellowCliEnvironment);

                            simulationClient.Simulate(CommandCliToSim(blueClientCommandReply,
                                yellowClientCommandReply, isSecondHalf));
                            break;
                        }
                        case ResultType.NextPhase:
                        {
                            // This Phase is over, start next phase
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
                            return matchInfo;
                        }
                        case ResultType.PlaceKick:
                        case ResultType.GoalKick:
                        case ResultType.PenaltyKick:
                        case ResultType.FreeKickRightTop:
                        case ResultType.FreeKickRightBot:
                        case ResultType.FreeKickLeftTop:
                        case ResultType.FreeKickLeftBot:
                        {
                            // A foul happened

                            // Get ball position
                            var cliEnvironment = EnvironmentSimToCli(environment, info);
                            var ballPosition = judgeResult.WhosBall switch
                            {
                                Simuro5v5.Side.Nobody => FoulBallPosition(info, judgeResult),
                                Simuro5v5.Side.Blue => blueClient.SetBall(cliEnvironment),
                                Simuro5v5.Side.Yellow => ConvertFromRight(
                                    yellowClient.SetBall(ConvertToRight(cliEnvironment))),
                                _ => throw new ArgumentOutOfRangeException()
                            };
                            Robots blueClientRobotsReply;
                            Robots yellowClientRobotsReply;

                            cliEnvironment.Frame.Ball = ballPosition;
                            switch (judgeResult.WhoisFirst)
                            {
                                case Simuro5v5.Side.Blue:
                                    blueClientRobotsReply = blueClient.SetFormerRobots(cliEnvironment);
                                    cliEnvironment.FoulInfo.Actor = Side.Opponent;
                                    cliEnvironment.Frame.RobotsBlue.SetRobots(blueClientRobotsReply);
                                    matchInfo.UpdateByCliEnvironment(cliEnvironment);

                                    matchInfo.Referee.JudgeAutoPlacement(matchInfo, judgeResult, Simuro5v5.Side.Blue);
                                    MatchInfo2CliEnvironment(matchInfo, ref cliEnvironment, false);
                                    yellowClientRobotsReply =
                                        yellowClient.SetLaterRobots(ConvertToRight(cliEnvironment));
                                    ConvertFromRight(ref yellowClientRobotsReply);
                                    cliEnvironment.Frame.RobotsYellow.SetRobots(yellowClientRobotsReply);
                                    matchInfo.UpdateByCliEnvironment(cliEnvironment);
                                    matchInfo.Referee.JudgeAutoPlacement(matchInfo, judgeResult, Simuro5v5.Side.Yellow);
                                    MatchInfo2CliEnvironment(matchInfo, ref cliEnvironment, true);
                                    break;
                                case Simuro5v5.Side.Yellow:
                                    yellowClientRobotsReply =
                                        yellowClient.SetFormerRobots(ConvertToRight(cliEnvironment));
                                    ConvertFromRight(ref yellowClientRobotsReply);
                                    cliEnvironment.FoulInfo.Actor = Side.Opponent;
                                    cliEnvironment.Frame.RobotsYellow.SetRobots(yellowClientRobotsReply);
                                    matchInfo.UpdateByCliEnvironment(cliEnvironment);
                                    matchInfo.Referee.JudgeAutoPlacement(matchInfo, judgeResult, Simuro5v5.Side.Yellow);
                                    MatchInfo2CliEnvironment(matchInfo, ref cliEnvironment, true);
                                    blueClientRobotsReply = blueClient.SetLaterRobots(cliEnvironment);
                                    cliEnvironment.Frame.RobotsBlue.SetRobots(blueClientRobotsReply);
                                    matchInfo.UpdateByCliEnvironment(cliEnvironment);
                                    matchInfo.Referee.JudgeAutoPlacement(matchInfo, judgeResult, Simuro5v5.Side.Blue);
                                    MatchInfo2CliEnvironment(matchInfo, ref cliEnvironment, false);
                                    break;
                                default:
                                    throw new ArgumentOutOfRangeException();
                            }

                            simulationClient.Simulate(MatchInfo2Packet(matchInfo, isSecondHalf));
                            break;
                        }
                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                    if (isSecondHalf)
                    {
                        SecondHalfTransform(ref environment);
                    }

                    SimEnvironment2MatchInfo(environment, ref matchInfo);
                    matchInfo.TickMatch++;
                    matchInfo.TickPhase++;
                }
                catch (RpcException ex)
                {
                    Console.WriteLine($"RPC exception occured: {ex.Message}");
                    Console.WriteLine("Press ENTER to retry...");
                    Console.ReadLine();
                }
            }
        }

        /// Get Initialization of FiraMessage.SimToRef.Environment
        private static Environment InitSimEnvironment()
        {
            double[] blueX = {0.30, 0.80, 0.95, 0.80, 0.30};
            double[] blueY = {0.60, 0.60, 0.0, -0.60, -0.60};
            double[] yellowX = {-0.30, -0.80, -0.95, -0.80, -0.30};
            double[] yellowY = {0.60, 0.60, 0.0, -0.60, -0.60};

            var simEnvironment = new Environment
            {
                Step = 0, Field = new Field(), Frame = new Frame()
            };

            for (int i = 0; i < 5; i++)
            {
                simEnvironment.Frame.RobotsBlue.Add(new FiraMessage.Robot
                    {RobotId = (uint) i, X = blueX[i], Y = blueY[i], Orientation = 0});
                simEnvironment.Frame.RobotsYellow.Add(new FiraMessage.Robot
                    {RobotId = (uint) i, X = yellowX[i], Y = yellowY[i], Orientation = 0});
            }

            return simEnvironment;
        }

        /// Extract <see cref="FoulInfo"/> from inner <see cref="JudgeResult"/> type
        private static FoulInfo ExtractFoulInfo(JudgeResult judgeResult, MatchInfo matchInfo)
        {
            // Don't use FoulInfo.Types.PhaseType.Stopped.
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

        ///Get the ball position corresponding to the foul type
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

        ///Message type conversion
        private static void SimEnvironment2MatchInfo(Environment environment,
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
                    robot[environment.Frame.RobotsBlue[i].RobotId].pos.x = environment.Frame.RobotsBlue[i].X * 100;
                    robot[environment.Frame.RobotsBlue[i].RobotId].pos.y = environment.Frame.RobotsBlue[i].Y * 100;
                    robot[environment.Frame.RobotsBlue[i].RobotId].rotation =
                        environment.Frame.RobotsBlue[i].Orientation;
                }
                else if (i >= 5)
                {
                    robot[5 + environment.Frame.RobotsYellow[i - 5].RobotId].pos.x =
                        environment.Frame.RobotsYellow[i - 5].X * 100;
                    robot[5 + environment.Frame.RobotsYellow[i - 5].RobotId].pos.y =
                        environment.Frame.RobotsYellow[i - 5].Y * 100;
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
                    x = environment.Frame.Ball.X * 100,
                    y = environment.Frame.Ball.Y * 100
                }
            };
            matchInfo.BlueRobots = robot.Take(5).ToArray();
            matchInfo.YellowRobots = robot.Skip(5).ToArray();
        }

        /// Convert Sim <see cref="FiraMessage.SimToRef.Environment"/>
        /// to Cli <see cref="FiraMessage.RefToCli.Environment"/>
        private static FiraMessage.RefToCli.Environment EnvironmentSimToCli(
            Environment simEnvironment, FoulInfo info)
        {
            return new FiraMessage.RefToCli.Environment
            {
                Frame = simEnvironment.Frame,
                FoulInfo = info
            };
        }

        ///Message type conversion
        private static Packet CommandCliToSim(FiraMessage.RefToCli.Command blueCommand,
            FiraMessage.RefToCli.Command yellowCommand, bool isSecondHalf)
        {
            FiraMessage.SimToRef.Command[] commandBlue = new FiraMessage.SimToRef.Command[5];
            FiraMessage.SimToRef.Command[] commandYellow = new FiraMessage.SimToRef.Command[5];
            for (int i = 0; i < 5; i++)
            {
                commandBlue[blueCommand.Wheels[i].RobotId] = new FiraMessage.SimToRef.Command
                {
                    Yellowteam = isSecondHalf,
                    Id = (uint) blueCommand.Wheels[i].RobotId,
                    WheelLeft = blueCommand.Wheels[i].Left,
                    WheelRight = blueCommand.Wheels[i].Right
                };

                commandYellow[yellowCommand.Wheels[i].RobotId] = new FiraMessage.SimToRef.Command
                {
                    Yellowteam = !isSecondHalf,
                    Id = (uint) yellowCommand.Wheels[i].RobotId,
                    WheelLeft = yellowCommand.Wheels[i].Left,
                    WheelRight = yellowCommand.Wheels[i].Right
                };
            }

            return new Packet
            {
                Cmd = new Commands
                {
                    RobotCommands = {commandBlue, commandYellow}
                }
            };
        }

        ///Update message after receiving positioning message
        private static void SetRobots(this RepeatedField<FiraMessage.Robot> frameRobotsField, Robots robots)
        {
            frameRobotsField.Clear();
            frameRobotsField.AddRange(robots.Robots_);
        }

        ///Message type conversion
        private static void UpdateByCliEnvironment(this MatchInfo matchInfo,
            FiraMessage.RefToCli.Environment cliEnvironment)
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

        ///Message type conversion
        private static void MatchInfo2CliEnvironment(MatchInfo matchInfo,
            ref FiraMessage.RefToCli.Environment cliEnvironment, bool isYellow)
        {
            if (!isYellow)
            {
                for (int i = 0; i < 5; i++)
                {
                    cliEnvironment.Frame.RobotsBlue[i].X =
                        matchInfo.BlueRobots[cliEnvironment.Frame.RobotsBlue[i].RobotId].pos.x;
                    cliEnvironment.Frame.RobotsBlue[i].Y =
                        matchInfo.BlueRobots[cliEnvironment.Frame.RobotsBlue[i].RobotId].pos.y;
                    cliEnvironment.Frame.RobotsBlue[i].Orientation =
                        matchInfo.BlueRobots[cliEnvironment.Frame.RobotsBlue[i].RobotId].rotation;
                }
            }
            else
            {
                for (int i = 0; i < 5; i++)
                {
                    cliEnvironment.Frame.RobotsYellow[i].X =
                        matchInfo.BlueRobots[cliEnvironment.Frame.RobotsYellow[i].RobotId].pos.x;
                    cliEnvironment.Frame.RobotsYellow[i].Y =
                        matchInfo.BlueRobots[cliEnvironment.Frame.RobotsYellow[i].RobotId].pos.y;
                    cliEnvironment.Frame.RobotsYellow[i].Orientation =
                        matchInfo.BlueRobots[cliEnvironment.Frame.RobotsYellow[i].RobotId].rotation;
                }
            }
        }

        ///Message type conversion
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
                        X = matchInfo.Ball.pos.x / 100,
                        Y = matchInfo.Ball.pos.y / 100,
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
                                X = flag * robot.pos.x / 100,
                                Y = flag * robot.pos.y / 100,
                                Orientation = isSecondHalf
                                    ? (robot.rotation > 0 ? robot.rotation - Math.PI : robot.rotation + Math.PI)
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
                                X = flag * robot.pos.x / 100,
                                Y = flag * robot.pos.y / 100,
                                Orientation = isSecondHalf
                                    ? (robot.rotation > 0 ? robot.rotation - Math.PI : robot.rotation + Math.PI)
                                    : robot.rotation
                            },
                            Yellowteam = !isSecondHalf,
                            Turnon = true
                        })
                    }
                }
            };
        }

        ///In the second half of the game, the information of the two teams will be converted before the information of the simulation is transmitted.
        private static void SecondHalfTransform(ref Environment replySimulate)
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
                        Orientation = robot.Orientation > 0 ? robot.Orientation - Math.PI : robot.Orientation + Math.PI
                    })
                },
                RobotsYellow =
                {
                    clone.Frame.RobotsBlue.Select((robot, i) => new FiraMessage.Robot
                    {
                        RobotId = robot.RobotId,
                        X = -robot.X,
                        Y = -robot.Y,
                        Orientation = robot.Orientation > 0 ? robot.Orientation - Math.PI : robot.Orientation + Math.PI
                    })
                }
            };
        }

        /// Rotate the coordinates by 180 degrees.
        /// <para>This is done before transmitting information to the Yellow client.</para>
        private static FiraMessage.RefToCli.Environment ConvertToRight(FiraMessage.RefToCli.Environment cliEnvironment)
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
                            Orientation = robot.Orientation > 0 ? robot.Orientation - Math.PI : robot.Orientation + Math.PI
                        })
                    },
                    RobotsYellow =
                    {
                        cliEnvironment.Frame.RobotsYellow.Select((robot, i) => new FiraMessage.Robot
                        {
                            RobotId = robot.RobotId,
                            X = -robot.X,
                            Y = -robot.Y,
                            Orientation = robot.Orientation > 0 ? robot.Orientation - Math.PI : robot.Orientation + Math.PI
                        })
                    }
                }
            };
        }

        /// rotate the coordinates by 180 degrees.
        /// <para>this is done after transmitting information from the yellow client.</para>
        private static FiraMessage.Ball ConvertFromRight(FiraMessage.Ball ball)
        {
            return new FiraMessage.Ball { X = -ball.X, Y = -ball.Y, Z = ball.Z };
        }

        /// rotate the coordinates by 180 degrees.
        /// <para>this is done after transmitting information from the yellow client.</para>
        private static void ConvertFromRight(ref Robots robots)
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
                        Orientation = robot.Orientation > 0 ? robot.Orientation - Math.PI : robot.Orientation + Math.PI
                    })
                }
            };
        }
    }
}