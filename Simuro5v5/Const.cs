﻿
namespace Referee.Simuro5v5
{
    static class Const
    {
        public readonly static int RobotsPerTeam = 5;
        public readonly static int MaxWheelVelocity = 125;
        public readonly static int MinWheelVelocity = -125;

        // FPS
        public readonly static int FramePerSecond = 66;

        // 模拟频率的倒数，即一拍的时间。为了减小模型陷入的概率，以两倍帧率运行，但只在奇数拍调用策略
        public readonly static double FixedDeltaTime = 1.0f / FramePerSecond / 2;

        public readonly static double inch2cm = 2.54f;
        public readonly static double cm2inch = 1.0f / 2.54f;

        public static class Field
        {
            public readonly static double Height = -3.933533f;
            public readonly static double Top = 90.0f;
            public readonly static double Bottom = -90.0f;
            public readonly static double Left = -110.0f;
            public readonly static double Right = 110.0f;
        }

        public static class Robot
        {
            public readonly static double Mass = 10;

            // linear
            public readonly static double ForwardForceFactor = 102.89712678726376f;
            public readonly static double DragFactor = 79.81736047779975f;
            public readonly static double DoubleZeroDragFactor = 760;
//            public readonly static double SidewayDragFactor = 30200;
            public readonly static double SidewayDragFactor = 1000;

            // angular
            public readonly static double TorqueFactor = 1156.1817018313f;
            public readonly static double AngularDragFactor = 3769.775104018879f;
            public readonly static double ZeroAngularDragFactor = 2097.9773f;

            public readonly static double RL = 7.8670658f; // 机器人边长         // 有舍入误差
            public const double HRL = 3.9335329f; // 机器人半边长       // 有舍入误差
            public readonly static double maxVelocity = 125.0f; // 机器人最大线速度

            public readonly static double maxAngularVelocity = 360; // 机器人最大角速度，角度制

            //public readonly static double maxAngularVelocity = 4024.07121363f; // 机器人最大角速度，角度制
            public readonly static double kv = 66.0f; // 使加速度为1时需要乘的系数
            public readonly static double kv1 = 75.81f; // 使加速度为1时需要乘的系数，f=1.0
            public readonly static double kv2 = 71.88599f; // 使加速度为1时需要乘的系数，f=0.6
            public readonly static double kw = 11.89119f; // 使角加速度为1时需要乘的系数
            public readonly static double k1Dym = -0.0007292f; // 修正引擎角速度非线性规律的参数
            public readonly static double k2Dym = 1.0f; // 修正引擎角速度非线性规律的参数
            public readonly static double range = 0.001f; // 重整角度阈值
            public readonly static double r = 0.53461992f; // 小车对z轴的惯性半径
            public readonly static double r2 = 0.56057f; // 小车对z轴的惯性半径2
            public readonly static double dyF = 0.1486f; // 小车的动摩擦因数
            public readonly static double stF = dyF; // 小车的静摩擦因数
            public readonly static double bonc = 0.2f; // 小车的弹性系数
        }

        public static class Ball
        {
//            public readonly static double mass = 10 / 16.7f;
            public readonly static double mass = 0.95f;

            public readonly static double BL = 5.043424f; // 球的直径
            public const double HBL = 2.521712f; // 球的半径
            public readonly static double dyF = 1.0f; // 球的动摩擦因数
            public readonly static double stF = dyF; // 球的静摩擦因数
            public readonly static double bonc = 0.8f; // 球的弹性系数
        }

        public static class Wheel
        {
            public readonly static double mass = 10f;
            public readonly static double radius = 3.54018f * 10;
        }
    }
}