using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nito.AsyncEx;

namespace Surf.Core
{

    /// <summary>
    /// Handles gossip messages from other members.
    /// 
    /// The component keeps a list of gossip messages and decides how often they are 
    /// piggybacked to other members.
    /// 
    /// The number 
    /// </summary>
    public class DisseminationComponent
    {
        private readonly StateAndConfigurationComponent _state;
        private readonly AsyncReaderWriterLock _rwLock = new AsyncReaderWriterLock();

        private List<GossipWrapper> _basicMemberGossip = new List<GossipWrapper>();
        private HashSet<int> _knownGossip = new HashSet<int>();
        public DisseminationComponent(StateAndConfigurationComponent state)
        {
            _state = state;
        }

        private class GossipWrapper
        {
            public Proto.Gossip Msg { get; set; }
            public double LocalAge { get; set; } = 0.0;
        }

        public async ValueTask<int> StackCount()
        {
            using (await _rwLock.ReaderLockAsync())
            {
                return _basicMemberGossip.Count();
            }
        }

        public async Task AddAsync(Surf.Proto.Gossip msg)
        {
            using (await _rwLock.ReaderLockAsync())
            {
                if (_knownGossip.Contains(msg.GetHashCode()))
                {
                    return;
                }
            }
            using (await _rwLock.WriterLockAsync())
            {
                _basicMemberGossip.Insert(0, new GossipWrapper() { Msg = msg });
            }
        }

        /// <summary>
        /// Fetches the next gossip messages that should be sent, increases there age by 1 and removes the ones that 
        /// are too old.
        /// </summary>
        public async Task<List<Surf.Proto.Gossip>> FetchNextAsync(int next)
        {
            double maxAge = await _state.GetChatterLifeTimeAsync();
            using (await _rwLock.WriterLockAsync())
            {
                var elements = new List<Surf.Proto.Gossip>(next);

                _basicMemberGossip.Take(next).ToList().ForEach(m =>
                {
                    m.LocalAge++;
                    elements.Add(m.Msg);
                });

                _basicMemberGossip = _basicMemberGossip.Where(e => e.LocalAge <= maxAge).OrderBy(e => e.LocalAge).ToList();
                _knownGossip = _basicMemberGossip.Select(g => g.Msg.GetHashCode()).ToHashSet();
                return elements;
            }
        }
    }
}
