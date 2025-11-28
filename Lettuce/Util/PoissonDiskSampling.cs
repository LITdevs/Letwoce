using System.Drawing;

namespace Lettuce.Util;

public static class PoissonDiskSampling
{
    public static Point GeneratePoint(int gridWidth, int gridHeight, float minDistance, List<Point> existingPoints)
    {
        const int maxAttempts = 30;
        var random = Random.Shared;

        if (existingPoints.Count == 0)
        {
            return new Point(random.Next(0, gridWidth), random.Next(0, gridHeight));
        }

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            var candidate = new Point(random.Next(0, gridWidth), random.Next(0, gridHeight));

            if (IsValidPoint(candidate, existingPoints, minDistance))
            {
                return candidate;
            }
        }

        for (int attempt = 0; attempt < maxAttempts * 2; attempt++)
        {
            var basePoint = existingPoints[random.Next(existingPoints.Count)];
            var angle = random.NextDouble() * Math.PI * 2;
            var radius = minDistance + random.NextDouble() * minDistance;

            var candidate = new Point(
                (int)(basePoint.X + Math.Cos(angle) * radius),
                (int)(basePoint.Y + Math.Sin(angle) * radius)
            );

            if (candidate.X >= 0 && candidate.X < gridWidth &&
                candidate.Y >= 0 && candidate.Y < gridHeight &&
                IsValidPoint(candidate, existingPoints, minDistance))
            {
                return candidate;
            }
        }

        return new Point(random.Next(0, gridWidth), random.Next(0, gridHeight));
    }

    public static List<Point> GeneratePoints(int gridWidth, int gridHeight, int count, float minDistance)
    {
        var points = new List<Point>();

        for (int i = 0; i < count; i++)
        {
            var point = GeneratePoint(gridWidth, gridHeight, minDistance, points);
            points.Add(point);
        }

        return points;
    }

    private static bool IsValidPoint(Point candidate, List<Point> existingPoints, float minDistance)
    {
        foreach (var point in existingPoints)
        {
            var dx = candidate.X - point.X;
            var dy = candidate.Y - point.Y;
            var distance = Math.Sqrt(dx * dx + dy * dy);

            if (distance < minDistance)
            {
                return false;
            }
        }

        return true;
    }
}
