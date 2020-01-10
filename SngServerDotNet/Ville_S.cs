using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nettention.Proud;

namespace SngServer
{
    public class Ville_S
    {
        public Ville_S()
        {
            m_nextNewID = 1;
            m_p2pGroupID = HostID.HostID_None;
        }

        // the players who are online.
        public ConcurrentDictionary<HostID, RemoteClient_S> m_players = new ConcurrentDictionary<HostID,RemoteClient_S>();

        // ville name
        public String m_name;

        // increases for every new world object is added.
        // this value is saved to database, too.
        public int m_nextNewID;

        // world objects
        public ConcurrentDictionary<int, WorldObject_S> m_worldObjects = new ConcurrentDictionary<int,WorldObject_S>();

        // every players in this ville are P2P communicated.
        // this is useful for lesser latency for cloud-hosting servers (e.g. Amazon AWS)
        public HostID m_p2pGroupID;
    }
}
