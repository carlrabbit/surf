using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nito.AsyncEx;

namespace Surf.Core
{
    /// <summary>
    /// TODO: MembershipComponent will react to basic chatter, e.g. instead of AddMemberAsync -> OnMemberJoined 
    /// </summary>
    public class MembershipComponent
    {
        private readonly StateAndConfigurationComponent _state;

        public MembershipComponent(StateAndConfigurationComponent state)
        {
            _state = state;
        }

        private readonly AsyncReaderWriterLock rwLock = new AsyncReaderWriterLock();
        private List<Member> _members = new List<Member>();
        private int _randomListIndex = 0;
        private Random _rng = new Random();
        public async Task<bool> AddMemberAsync(Member m)
        {
            using (await rwLock.WriterLockAsync().ConfigureAwait(false))
            {
                if (!_members.Any(e => e.Address == m.Address))
                {
                    //insert new member at a random position
                    _members.Insert(_rng.Next(0, _members.Count), m);

                    await _state.UpdateMemberCountAsync(_members.Count).ConfigureAwait(false);

                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        public async Task<bool> RemoveMemberAsync(Member member)
        {
            int memberCountBeforeFiltering;

            using (await rwLock.WriterLockAsync().ConfigureAwait(false))
            {
                memberCountBeforeFiltering = _members.Count;
                _members = _members.Where(m => m.Address != member.Address).ToList();

                if (memberCountBeforeFiltering != _members.Count)
                {
                    _randomListIndex--;
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

        public async Task<int> MemberCountAsync()
        {
            using (await rwLock.ReaderLockAsync().ConfigureAwait(false))
            {
                return _members.Count;
            }
        }

        public async Task<Member> NextRandomMemberAsync()
        {
            using (await rwLock.WriterLockAsync().ConfigureAwait(false))
            {
                if (_randomListIndex >= _members.Count)
                {
                    Shuffle(_members);
                    _randomListIndex = 0;
                }

                return _members[_randomListIndex++];
            }
        }
    }
}
