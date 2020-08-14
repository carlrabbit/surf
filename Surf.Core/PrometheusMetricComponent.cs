using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Prometheus;

namespace Surf.Core
{
    /// <summary>
    /// The metric component handles collection of surf specific metrics.
    /// </summary>
    public class PrometheusMetricComponent : IMetricComponent
    {
        private readonly Summary _averageTurnaroundTime =
            Metrics.CreateSummary("surf_average_turnaround_time_seconds",
                help: "",
                new SummaryConfiguration()
                {
                    Objectives = new[]{
                        new QuantileEpsilonPair(0.5, 0.05),
                        new QuantileEpsilonPair(0.9, 0.05),
                        new QuantileEpsilonPair(0.95, 0.01),
                        new QuantileEpsilonPair(0.99, 0.005)
                    }
                });

        public PrometheusMetricComponent()
        {
            Metrics.SuppressDefaultMetrics();
        }

        public void TrackMeanRoundtripTime(double milliSeconds)
        {
            _averageTurnaroundTime.Observe(milliSeconds / 1000);
        }

        public async Task<string> Dump()
        {
            using var s = new MemoryStream();
            await Metrics.DefaultRegistry.CollectAndExportAsTextAsync(s);
            s.Position = 0;

            using var reader = new StreamReader(s);

            return reader.ReadToEnd();
        }

    }
}
