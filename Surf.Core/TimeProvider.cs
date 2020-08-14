
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Surf.Core
{
    public class TimeProvider : ITimeProvider
    {
        public long Diff(object now)
        {
            if (!(now is Stopwatch))
            {
                throw new ArgumentException("Argument should be of time Stopwatch");
            }
            (now as Stopwatch).Stop();
            return (now as Stopwatch).ElapsedMilliseconds;
        }

        public object NowForDiff()
        {
            var sw = new Stopwatch();
            sw.Start();
            return sw;
        }

        public DateTime UtcNow()
        {
            return DateTime.UtcNow;
        }

        public Task TaskDelay(int milliseconds)
        {
            return Task.Delay(milliseconds);
        }
    }
}
