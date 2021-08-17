using Nettention.Proud;
using System;
using System.Collections.Generic;

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
        public Dictionary<HostID, RemoteClient_S> m_players = new();

        // ville name
        public String m_name;

        // increases for every new world object is added.
        // this value is saved to database, too.
        public int m_nextNewID;

        // world objects
        public Dictionary<int, WorldObject_S> m_worldObjects = new();

        // every players in this ville are P2P communicated.
        // this is useful for lesser latency for cloud-hosting servers (e.g. Amazon AWS)
        public HostID m_p2pGroupID;
    }
}
