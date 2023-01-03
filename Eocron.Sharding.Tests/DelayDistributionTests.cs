using System.Text;
using Eocron.Sharding.Helpers;
using NUnit.Framework;

namespace Eocron.Sharding.Tests
{
    [Explicit]
    [TestFixture]
    public class DelayDistributionTests
    {
        public static IEnumerable<TimeSpan> GetRandomDelays(int count, int min, int max)
        {
            var rnd = new Random();
            return Enumerable.Range(0, count).Select(x => TimeSpan.FromMilliseconds(rnd.Next(min, max)));
        }

        public static IEnumerable<TimeSpan> GetDelayApproximation(TimeSpan sample, Func<int, TimeSpan> provider, TimeSpan perTry)
        {
            TimeSpan sum = TimeSpan.Zero;
            int i = 0;
            while (sum < sample)
            {
                var part = provider(i++) + perTry;
                sum = sum + part;
                yield return part;
            }
        }

        public class MeasurementInfo
        {
            public TimeSpan MeanLoss { get; set; }

            public TimeSpan AverageLoss { get; set; }

            public TimeSpan TotalLoss { get; set; }

            public TimeSpan TotalTryLoss { get; set; }

            public float LossPercent { get; set; }
            public string Name { get; set; }

            public override string ToString()
            {
                var sb = new StringBuilder();
                sb.AppendLine(Name);
                sb.AppendFormat("  Mean loss:      {0}\n", MeanLoss);
                sb.AppendFormat("  Avg loss:       {0}\n", AverageLoss);
                sb.AppendFormat("  Total loss:     {0}\n", TotalLoss);
                sb.AppendFormat("  Total try loss: {0}\n", TotalTryLoss);
                sb.AppendFormat("  Loss: {0:F3}%\n", LossPercent*100);
                return sb.ToString();
            }
        }

        public static MeasurementInfo MeasureStatistics(IList<TimeSpan> actual, Func<int, TimeSpan> provider, TimeSpan perTryDelay, string name)
        {
            var diffs = new List<TimeSpan>();
            var tryCount = new List<long>();
            foreach (var sample in actual)
            {
                var matchings = GetDelayApproximation(sample, provider, perTryDelay).DefaultIfEmpty(TimeSpan.Zero).ToList();
                var sum = matchings.Aggregate((x, y) => x + y);
                var diff = sum - sample;
                diffs.Add(diff);
                tryCount.Add(matchings.Count);
            }
            diffs.Sort();
            var diffSum = diffs.Aggregate((x, y) => x + y);
            var actualSum = actual.Aggregate((x, y) => x + y);
            return new MeasurementInfo
            {
                Name = name,
                MeanLoss = diffs[diffs.Count / 2],
                AverageLoss = diffSum / diffs.Count,
                TotalLoss = diffSum,
                TotalTryLoss = tryCount.Sum() * perTryDelay,
                LossPercent = diffSum.Ticks / (float)actualSum.Ticks
            };
        }

        [Test]
        [TestCase(100000, 0, 10, 1000)]
        [TestCase(100000, 0, 100, 1000)]
        [TestCase(100000, 0, 1000, 1000)]
        [TestCase(100000, 0, 10000, 1000)]
        [TestCase(100000, 0, 100000, 1000)]
        [TestCase(100000, 0, 10, 100)]
        [TestCase(100000, 0, 100, 100)]
        [TestCase(100000, 0, 1000, 100)]
        [TestCase(100000, 0, 10000, 100)]
        [TestCase(100000, 0, 100000, 100)]
        [TestCase(100000, 0, 10, 1)]
        [TestCase(100000, 0, 100, 1)]
        [TestCase(100000, 0, 1000, 1)]
        [TestCase(100000, 0, 10000, 1)]
        [TestCase(100000, 0, 100000, 1)]
        public void Measure(int count, int minMs, int maxMs, int perTryDelayMs)
        {
            var perTryDelay = TimeSpan.FromMilliseconds(perTryDelayMs);
            var samples = GetRandomDelays(count, minMs, maxMs).ToList();
            var list = new List<MeasurementInfo>()
            {
                //MeasureDelayFunctionMs(
                //    samples,
                //    x => (int)DelayHelper
                //        .LinearDelayPolicy(x, TimeSpan.FromMilliseconds(1), TimeSpan.FromMinutes(1), 10)
                //        .TotalMilliseconds,
                //    perTryDelayMs,
                //    "Linear"),
                MeasureStatistics(
                    samples,
                    x => DelayHelper.ConstantDelayPolicy(x, TimeSpan.FromMilliseconds(100)),
                    perTryDelay,
                    "Constant"),
                MeasureStatistics(
                    samples,
                    x => DelayHelper.ExponentialDelayPolicy(x, TimeSpan.FromMilliseconds(1), TimeSpan.FromSeconds(5)),
                    perTryDelay,
                    "Exponential")
            };
            list.Sort((x,y)=> x.LossPercent.CompareTo(y.LossPercent));
            foreach (var measurementInfo in list)
            {
                Console.WriteLine(measurementInfo);
            }

            Console.WriteLine("BEST:");
            Console.WriteLine(list[0]);
        }

        [Test]
        [TestCase(0, 5000)]
        public void PrintExponential(int start, int count)
        {
            Console.WriteLine(string.Join("ms, ", Enumerable.Range(start, count).Select(i =>
                (int)DelayHelper.ExponentialDelayPolicy(i, TimeSpan.FromMilliseconds(1), TimeSpan.FromMinutes(1)).TotalMilliseconds)));
        }

        [Test]
        [TestCase(0, 5000)]
        public void PrintLinear(int start, int count)
        {
            Console.WriteLine(string.Join("ms, ", Enumerable.Range(start, count).Select(i =>
                (int)DelayHelper.LinearDelayPolicy(i, TimeSpan.FromMilliseconds(1), TimeSpan.FromSeconds(1), 10).TotalMilliseconds)));
        }
    }
}