using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nito.AsyncEx;

namespace Surf.Core
{
    /// <summary>
    /// TODO: MembershipComponent will react to basic chatter, e.g. instead of AddMemberAsync -> OnMemberJoined 
    /// 
    /// The membership component manages the actual members of a group and their status. It does not track the current member itself.
    /// </summary>
    public class MembershipComponent : IMembershipComponent
    {
        private readonly IProtocolStateComponent _state;

        public MembershipComponent(IProtocolStateComponent state)
        {
            _state = state;
        }

        private readonly AsyncReaderWriterLock _rwLock = new AsyncReaderWriterLock();
        private List<Member> _members = new List<Member>();
        private int _randomListIndex = 0;
        private readonly Random _rng = new Random();

        /// <summary>
        /// Adds a new member to the member list. Returns true if the member was not listed before
        /// and false if the member was already listed.
        /// </summary>
        public async Task<bool> AddMemberAsync(Member member)
        {
            if (member.Port == _state.GetSelf().Port)
            {
                return false;
            }

            using (await _rwLock.WriterLockAsync().ConfigureAwait(false))
            {
                if (!_members.Any(e => e.Port == member.Port))
                {
                    //insert new member at a random position
                    _members.Insert(_rng.Next(0, _members.Count), member);

                    // TODO: increase _randomListIndex if new member was added before it

                    await _state.UpdateMemberCountAsync(_members.Count).ConfigureAwait(false);

                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// Removes a member of the member list. Returns true if the member was listed and removed.
        /// If the member was not listed anymore, returns false.
        /// </summary>
        public async Task<bool> RemoveMemberAsync(Member member)
        {
            if (member.Port == _state.GetSelf().Port)
            {
                return false;
            }

            using (await _rwLock.WriterLockAsync().ConfigureAwait(false))
            {
                var memberCountBeforeFiltering = _members.Count;
                _members = _members.Where(m => m.Port != member.Port).ToList();

                if (memberCountBeforeFiltering != _members.Count)
                {
                    // TODO: only decrement if member was taken from before the _randomListIndex
                    _randomListIndex = Math.Max(0, _randomListIndex - 1);
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// Performs a Fisher Yates Shuffle on the member list. Expects the member list to be locked upfront. 
        /// </summary>
        /// <remarks>
        /// Based on https://www.dotnetperls.com/fisher-yates-shuffle
        /// </remarks>
        private void Shuffle(List<Member> members)
        {
            var n = members.Count;
            for (var i = 0; i < (n - 1); i++)
            {
                var r = i + _rng.Next(n - i);
                var t = members[r];
                members[r] = members[i];
                members[i] = t;
            }
        }

        /// <summary>
        /// Returns the current active members.
        /// </summary>
        public async Task<int> GetMemberCountAsync()
        {
            using (await _rwLock.ReaderLockAsync().ConfigureAwait(false))
            {
                return _members.Count;
            }
        }

        /// <summary>
        /// Picks a random member from the member list and guarantees that each member will be
        /// picked at least once during 2*N calls where N is the number of members currently listed.
        /// </summary>
        public async Task<Member?> PickRandomMemberForPingAsync()
        {
            using (await _rwLock.WriterLockAsync().ConfigureAwait(false))
            {
                if (_members.Count == 0)
                {
                    return null;
                }

                if (_randomListIndex >= _members.Count)
                {
                    Shuffle(_members);
                    _randomListIndex = 0;
                }

                return _members[_randomListIndex++];
            }
        }

        /// <summary>
        /// Picks a completely random member from the member list. It is not
        /// guaranteed that all members will be chosen at least once.
        /// </summary>
        public async Task<Member?> PickRandomMemberPingReqAsync()
        {
            using (await _rwLock.ReaderLockAsync().ConfigureAwait(false))
            {
                if (_members.Count == 0)
                {
                    return null;
                }
                return _members[_rng.Next(_members.Count)];
            }
        }
    }
}
