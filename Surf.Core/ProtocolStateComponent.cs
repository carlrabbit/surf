
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Nito.AsyncEx;

namespace Surf.Core
{
    /// <summary>
    /// The protocol state component manages all protocol specific parameters.
    /// 
    /// With it dynamic configuration parameters are managed, such TTL parameters or current message round trip times.
    /// 
    /// Updating other static parameters is supported as well to allow dynamic configuration updates.
    /// </summary>
    public class ProtocolStateComponent : IProtocolStateComponent
    {
        private readonly AsyncReaderWriterLock _rwLock = new AsyncReaderWriterLock();
        private readonly PrometheusMetricComponent _mc;

        public ProtocolStateComponent(SurfConfiguration cfg, PrometheusMetricComponent metricComponent)
        {
            _mc = metricComponent;

            _lambda = cfg.Lambda;
            _pingTimeout = cfg.PingTimeoutInMilliseconds;
            _protocolPeriodDurationMs = cfg.ProtocolPeriodDurationInMilliseconds;

            _self = new Member(IPAddress.Parse(cfg.BindAddress), cfg.Port ?? 6060);

            // init internal defaults
            _clt = 0;
            _protocolPeriod = 0;
            _meanRoundTripTime = _pingTimeout;
        }

        private readonly Member _self;
        public Member GetSelf()
        {
            return _self;
        }

        private double _lambda;
        private double _clt;
        public async Task<double> GetChatterLifeTimeAsync()
        {
            using (await _rwLock.ReaderLockAsync())
            {
                // Ensure gossip time has a value that does not 
                // lead to premature rejection in the dissemination component
                // TODO: check against other implementations
                return Math.Max(_lambda, _clt);
            }
        }

        public async Task UpdateMemberCountAsync(int activeMembers)
        {
            using (await _rwLock.WriterLockAsync())
            {
                _clt = Math.Ceiling(_lambda * Math.Log10(activeMembers + 1));
            }
        }

        private List<long> _measures = new List<long>();
        private double _meanRoundTripTime;

        public async Task UpdateAverageRoundTripTimeAsync(long measuredMilliseconds)
        {
            using (await _rwLock.WriterLockAsync())
            {
                if (_measures.Count > 100)
                {
                    _measures = _measures.Skip(1).Append(measuredMilliseconds).ToList();
                }
                else
                {
                    _measures.Add(measuredMilliseconds);
                }
                _meanRoundTripTime = _measures.Average();
            }
            _mc.TrackMeanRoundtripTime(_meanRoundTripTime);
        }

        public async Task<double> GetAverageRoundTripTimeAsync()
        {
            using (await _rwLock.ReaderLockAsync())
            {
                return _meanRoundTripTime;
            }
        }

        private int _protocolPeriod;
        public int IncreaseProtocolPeriodNumber()
        {
            return Interlocked.Increment(ref _protocolPeriod);
            //TODO: handle overflows
        }

        public Task<int> GetErrorCycleNumberAsync()
        {
            return Task.FromResult(_protocolPeriod);
        }

        private int _pingTimeout;
        public Task<int> GetCurrentPingTimeoutAsync()
        {
            return Task.FromResult(_pingTimeout);
        }

        private int _protocolPeriodDurationMs;
        public Task<int> GetProtocolPeriodNumberAsync()
        {
            return Task.FromResult(_protocolPeriodDurationMs);
        }
    }
}
