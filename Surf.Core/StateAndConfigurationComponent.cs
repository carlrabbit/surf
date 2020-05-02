
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
        private readonly AsyncReaderWriterLock lockSlim = new AsyncReaderWriterLock();

        public StateAndConfigurationComponent()
        {
            //TODO: add cfg class
            _lambda = 3.0;

            // init internal defaults
            _clt = 0;
            _errorCycle = 0;
        }

        private double _lambda;
        private double _clt;
        public async Task<double> GetChatterLifeTimeAsync()
        {
            using (await lockSlim.ReaderLockAsync())
            {
                return _clt;
            }
        }

        public async Task UpdateMemberCountAsync(int activeMembers)
        {
            using (await lockSlim.WriterLockAsync())
            {
                _clt = Math.Ceiling(_lambda * Math.Log10(activeMembers));
            }
        }

        public long _errorCycle;
        public Task IncreaseErrorCycleNumber()
        {
            Interlocked.Increment(ref _errorCycle);
            //TODO: handle overflows
            return Task.CompletedTask;
        }

        public Task<long> GetErrorCycleNumberAsync()
        {
            return Task.FromResult(Interlocked.Read(ref _errorCycle));
        }
    }
}
