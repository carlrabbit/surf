
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
        private readonly MetricComponent _mc;

        public ProtocolStateComponent(int port, MetricComponent metricComponent)
        {
            _mc = metricComponent;

            //TODO: add cfg class
            _lambda = 3.0;
            _pingTimeout = 100;
            _protocolPeriodDurationMs = 1000;
            _self = new Member()
            {
                Address = IPAddress.IPv6Loopback,
                Port = port
            };

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
            _mc.UpdateAverageTurnaroundTime(_meanRoundTripTime);
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
