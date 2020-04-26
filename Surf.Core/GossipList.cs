using System.Collections.Generic;
using System.Linq;

namespace Surf.Core
{

    public class GossipList
    {
        List<Surf.Proto.Gossip> _list = new List<Proto.Gossip>();

        public void Add(Surf.Proto.Gossip msg)
        {
            _list.Add(msg);
        }
        public int Count()
        {
            return _list.Count;
        }
        public List<Surf.Proto.Gossip> FetchNext(int next)
        {
            var result = _list.Take(next).ToList();

            _list = new List<Proto.Gossip>(_list.Skip(next).Concat(_list.Take(next)));

            return result;
        }
    }
}
