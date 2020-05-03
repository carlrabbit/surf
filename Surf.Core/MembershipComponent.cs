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
        public async Task<bool> AddMemberAsync(Member m)
        {
            using (await rwLock.WriterLockAsync().ConfigureAwait(false))
            {
                if (!_members.Any(e => e.Address == m.Address))
                {
                    _members.Insert(0, m);
                    await _state.UpdateMemberCountAsync(_members.Count);
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
            using (await rwLock.ReaderLockAsync())
            {
                if (_members.Any(m => m.Address == member.Address) == false)
                {
                    return false;
                }
            }
            using (await rwLock.WriterLockAsync())
            {
                _members = _members.Where(m => m.Address != member.Address).ToList();
            }
            return true;
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
                    //sort
                    _randomListIndex = 0;
                }

                return _members[_randomListIndex++];
            }
        }
    }
}
