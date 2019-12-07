using System;
using System.Collections.Generic;

namespace Referee.Simuro5v5
{
    public abstract class RectangleBase
    {
        // 1 --- 3
        // |  o  |
        // 4  -- 2

        /// <summary>
        /// 和 Point2 构成对角线
        /// </summary>
        public abstract Vector2D Point1 { get; }

        /// <summary>
        /// 和 Point1 构成对角线
        /// </summary>
        public abstract Vector2D Point2 { get; }

        /// <summary>
        /// 和 Point4 构成对角线
        /// </summary>
        public abstract Vector2D Point3 { get; }

        /// <summary>
        /// 和 Point3 构成对角线
        /// </summary>
        public abstract Vector2D Point4 { get; }

        /// <summary>
        /// 矩形中心点
        /// </summary>
        protected virtual Vector2D Midpoint => (Point1 + Point2) / 2;

        /// <summary>
        /// 获取一个集合，表示这个矩形的边缘线
        /// </summary>
        protected virtual List<(Vector2D, Vector2D)> Lines =>
            new List<(Vector2D, Vector2D)>
            {
                (Point1, Point3), (Point1, Point4),
                (Point3, Point2), (Point4, Point2)
            };


        /// <summary>
        /// 判断两条直线 AB 与 CD 是否相交
        /// </summary>
        static bool LineCross(Vector2D a, Vector2D b, Vector2D c, Vector2D d)
        {
            var ac = c - a;
            var ad = d - a;
            var bc = c - b;
            var bd = d - b;
            var ca = -ac;
            var cb = -bc;
            var da = -ad;
            var db = -bd;

            // 判断共线的情况
            if (Math.Abs(ac.Cross(ad)) < 1e-6 && Math.Abs(bc.Cross(bd)) < 1e-6)
            {
                return (ac * bc <= 0) || (ad * bd) <= 0 ||
                       (ca * da <= 0) || (cb * db) <= 0;
            }

            return (ac.Cross(ad)) * (bc.Cross(bd)) <= 0 &&
                   (ca.Cross(cb)) * (da.Cross(db)) <= 0;
        }

        public bool IsCrossedBy(RectangleBase rect)
        {
            foreach (var (a, b) in Lines)
            {
                foreach (var (c, d) in rect.Lines)
                {
                    if (LineCross(a, b, c, d))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public virtual bool ContainsPoint(Vector2D p)
        {
            return (Point3 - Point1).Cross(p - Point1) >= 0
                   && (Point2 - Point3).Cross(p - Point3) >= 0
                   && (Point4 - Point2).Cross(p - Point2) >= 0
                   && (Point1 - Point4).Cross(p - Point4) >= 0;
        }
    }

    public class UprightRectangle : RectangleBase
    {
        private double LeftX { get; }
        private double RightX { get; }
        private double TopY { get; }
        private double BotY { get; }

        public override Vector2D Point1 => new Vector2D(LeftX, TopY);
        public override Vector2D Point2 => new Vector2D(RightX, BotY);
        public override Vector2D Point3 => new Vector2D(RightX, TopY);
        public override Vector2D Point4 => new Vector2D(LeftX, BotY);

        public UprightRectangle(double leftX, double rightX, double topY, double botY)
        {
            LeftX = leftX;
            TopY = topY;
            RightX = rightX;
            BotY = botY;
        }

        public override bool ContainsPoint(Vector2D point)
        {
            return point.x < RightX && point.x > LeftX && point.y > BotY && point.y < TopY;
        }

        public bool ContainsCircle(Vector2D point, double radius)
        {
            return new UprightRectangle(LeftX + radius,
                RightX - radius,
                TopY - radius,
                BotY - radius).ContainsPoint(point);
        }
    }

    public class Square : RectangleBase
    {
        /// <summary>
        /// 通过正方形的两个对角点构造正方形
        /// </summary>
        public Square(Vector2D point1, Vector2D point2)
        {
            Point1 = point1;
            Point2 = point2;
        }

        /// <summary>
        /// 通过机器人中心与半径以及角度来构造机器人正方形
        /// </summary>
        public Square(Vector2D robotPosition, double angleInDegree = 0, double hrl = 3.9341f)
        {
            double robotRadius = hrl * Math.Sqrt(2);

            //角度规整
            while (angleInDegree > 45)
            {
                angleInDegree -= 90;
            }

            while (angleInDegree < -45)
            {
                angleInDegree += 90;
            }

            angleInDegree += 45;

            double point1X = robotPosition.x + robotRadius * Math.Cos(angleInDegree * Math.PI / 180);
            double point1Y = robotPosition.y + robotRadius * Math.Sin(angleInDegree * Math.PI / 180);
            double point2X = robotPosition.x - robotRadius * Math.Cos(angleInDegree * Math.PI / 180);
            double point2Y = robotPosition.y - robotRadius * Math.Sin(angleInDegree * Math.PI / 180);
            Point1 = new Vector2D(point1X, point1Y);
            Point2 = new Vector2D(point2X, point2Y);
        }

        /// <summary>
        /// 第一个对角点
        /// </summary>
        public override Vector2D Point1 { get; }

        /// <summary>
        /// 第二个对角点
        /// </summary>
        public override Vector2D Point2 { get; }

        public override Vector2D Point3 => (Point1 - Midpoint).Rotate(Math.PI / 2)
                                           + Midpoint;

        public override Vector2D Point4 => (Point1 - Midpoint).Rotate(-Math.PI / 2)
                                           + Midpoint;

        /// <summary>
        /// 判断正方形是否与园相交
        /// <br />
        /// TODO: 这个实现是不完整的
        /// </summary>
        public bool OverlapWithCircle(Vector2D center, double radius = Const.Ball.HBL)
        {
            foreach (var point in new[] {Point1, Point2, Point3, Point4})
            {
                if (Vector2D.Distance(point, center) < radius)
                {
                    return true;
                }
            }

            return ContainsPoint(center);
        }

        /// <summary>
        /// 测试是否在完全在一个矩形区域内部
        /// </summary>
        /// <param name="area">待判断的矩形区域</param>
        /// <returns></returns>
        public bool IsInRectangle(UprightRectangle area)
        {
            //TODO如果球员有大于零且小于二分之一区域在矩形内，判定结果为不在区域里，应该判断在区域里
            double width = Vector2D.Distance(Point1, Point3);
            double outer = Math.Max(
                Vector2D.Distance(area.Point1, area.Point3),
                Vector2D.Distance(area.Point1, area.Point4));
            return area.ContainsPoint(Midpoint) && !IsCrossedBy(area) && width < outer;
        }

        /// <summary>
        /// 测试是否与矩形交叉或者在矩形内
        /// </summary>
        /// <param name="area"></param>
        /// <returns></returns>
        public bool IsOverlapWithRectangle(UprightRectangle area)
        {
            return IsCrossedBy(area) || IsInRectangle(area);
        }
    }
}