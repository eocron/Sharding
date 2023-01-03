using System.Text;
using Eocron.Sharding.Helpers;
using NUnit.Framework;

namespace Eocron.Sharding.Tests
{
    [Explicit]
    [TestFixture]
    public class DistributionTests
    {
        public static IEnumerable<int> GetRandomInts(int count, int min, int max)
        {
            var rnd = new Random();
            return Enumerable.Range(0, count).Select(x => rnd.Next(min, max));
        }

        public static IEnumerable<int> GetMatchings(long sample, Func<int, int> provider, int perTry)
        {
            var sum = 0;
            int i = 0;
            while (sum < sample)
            {
                var part = provider(i++) + perTry;
                sum += part;
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

        public static MeasurementInfo MeasureDelayFunctionMs(IEnumerable<int> randomProvider, Func<int, int> provider, int perTryDelayMs, string name)
        {
            var diffs = new List<long>();
            var samples = randomProvider.Select(x=> (long)x).ToList();
            var tryCount = new List<long>();
            foreach (var sample in samples)
            {
                var matchings = GetMatchings(sample, provider, perTryDelayMs).ToList();
                var sum = matchings.Sum();
                var diff = sum - sample;
                diffs.Add(diff);
                tryCount.Add(matchings.Count);
            }
            diffs.Sort();
            return new MeasurementInfo
            {
                Name = name,
                MeanLoss = TimeSpan.FromMilliseconds(diffs[diffs.Count / 2]),
                AverageLoss = TimeSpan.FromMilliseconds(diffs.Average()),
                TotalLoss = TimeSpan.FromMilliseconds(diffs.Sum()),
                TotalTryLoss = TimeSpan.FromMilliseconds(tryCount.Sum() * perTryDelayMs),
                LossPercent = diffs.Sum() / (float)samples.Sum()
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
            var samples = GetRandomInts(count, minMs, maxMs).ToList();
            var list = new List<MeasurementInfo>()
            {
                //MeasureDelayFunctionMs(
                //    samples,
                //    x => (int)DelayHelper
                //        .LinearDelayPolicy(x, TimeSpan.FromMilliseconds(1), TimeSpan.FromMinutes(1), 10)
                //        .TotalMilliseconds,
                //    perTryDelayMs,
                //    "Linear"),
                MeasureDelayFunctionMs(
                    samples,
                    x => (int)DelayHelper.ConstantDelayPolicy(x, TimeSpan.FromMilliseconds(100)).TotalMilliseconds,
                    perTryDelayMs,
                    "Constant"),
                MeasureDelayFunctionMs(
                    samples,
                    x => (int)DelayHelper.ExponentialDelayPolicy(x, TimeSpan.FromMilliseconds(1),
                        TimeSpan.FromSeconds(5),
                        DelayHelper.GoldenRatio).TotalMilliseconds,
                    perTryDelayMs,
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