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
        private readonly List<Member> _members = new List<Member>();
        private int _randomListIndex = 0;
        public async Task<bool> AddMemberAsync(Member m)
        {
            using (await rwLock.WriterLockAsync().ConfigureAwait(false))
            {
                if (!_members.Any(e => e.Address == m.Address))
                {
                    _members.Add(m);
                    await _state.UpdateMemberCountAsync(_members.Count);
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        public async ValueTask<int> MemberCountAsync()
        {
            using (await rwLock.ReaderLockAsync().ConfigureAwait(false))
            {
                return _members.Count;
            }
        }

        public async Task<Member> NextRandomMember()
        {
            using (await rwLock.WriterLockAsync().ConfigureAwait(false))
            {
                if (_randomListIndex >= _members.Count)
                {
                    //sort
                    _randomListIndex = 0;
                }

                return _members[_randomListIndex++];
            }
        }
    }
}
