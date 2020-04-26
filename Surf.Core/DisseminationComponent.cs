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
        private readonly MembershipComponent _memberList;
        private readonly AsyncReaderWriterLock lockSlim = new AsyncReaderWriterLock();

        private List<GossipWrapper> _basicMemberGossip = new List<GossipWrapper>();
        public DisseminationComponent(MembershipComponent ml)
        {
            _memberList = ml;
        }

        private class GossipWrapper
        {
            public Proto.Gossip Msg { get; set; }
            public double LocalAge { get; set; } = 0;
        }

        public double Lambda => 3.0;

        public async ValueTask<double> CurrentMaxLocalAge()
        {
            return Math.Ceiling(Lambda * Math.Log10(await _memberList.MemberCountAsync()));
        }

        public async ValueTask<int> StackCount()
        {
            using (await lockSlim.ReaderLockAsync())
            {
                return _basicMemberGossip.Count();
            }
        }

        public async Task AddAsync(Surf.Proto.Gossip msg)
        {
            using (await lockSlim.WriterLockAsync())
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
            double maxAge = await CurrentMaxLocalAge();
            using (await lockSlim.WriterLockAsync())
            {
                List<Surf.Proto.Gossip> elements = new List<Surf.Proto.Gossip>(next);

                _basicMemberGossip.Take(next).ToList().ForEach(m =>
                {
                    m.LocalAge++;
                    elements.Add(m.Msg);
                });

                _basicMemberGossip = _basicMemberGossip.Where(e => e.LocalAge <= maxAge).OrderBy(e => e.LocalAge).ToList();
                return elements;
            }
        }
    }
}
