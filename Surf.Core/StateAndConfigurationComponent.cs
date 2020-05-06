
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nito.AsyncEx;

namespace Surf.Core
{

    /// <summary>
    /// The component manages all protocol specific paramters.
    /// 
    /// With it dynamic configuration parameters are managed, such TTL parameters or current message round trip times.
    /// 
    /// Updating other static parameters is supported as well to allow dynamic configuration updates in users of Surf.Core.
    /// </summary>
    public class StateAndConfigurationComponent
    {
        private readonly AsyncReaderWriterLock _rwLock = new AsyncReaderWriterLock();

        public StateAndConfigurationComponent()
        {
            //TODO: add cfg class
            _lambda = 3.0;
            _pingTimeout = 300;
            _protocolPeriodMs = 1000;

            // init internal defaults
            _clt = 0;
            _errorCycle = 0;
            _meanRoundTripTime = _pingTimeout;
        }

        private double _lambda;
        private double _clt;
        public async Task<double> GetChatterLifeTimeAsync()
        {
            using (await _rwLock.ReaderLockAsync())
            {
                return _clt;
            }
        }

        public async Task UpdateMemberCountAsync(int activeMembers)
        {
            using (await _rwLock.WriterLockAsync())
            {
                _clt = Math.Ceiling(_lambda * Math.Log10(activeMembers));
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
        }

        public async Task<double> GetAverageRoundTripTimeAsync()
        {
            using (await _rwLock.ReaderLockAsync())
            {
                return _meanRoundTripTime;
            }
        }

        private int _errorCycle;
        public int IncreaseProtocolPeriod()
        {
            return Interlocked.Increment(ref _errorCycle);
            //TODO: handle overflows
        }

        public Task<int> GetErrorCycleNumberAsync()
        {
            return Task.FromResult(_errorCycle);
        }

        private int _pingTimeout;
        public Task<int> GetCurrentPingTimeoutAsync()
        {
            return Task.FromResult(_pingTimeout);
        }

        private int _protocolPeriodMs;
        public Task<int> GetProtocolPeriodAsync()
        {
            return Task.FromResult(_protocolPeriodMs);
        }
    }
}
