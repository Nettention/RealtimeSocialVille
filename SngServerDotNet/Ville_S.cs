using Nettention.Proud;
using System;
using System.Collections.Generic;

namespace SngServer
{
    public class Ville_S
    {
        // the players who are online.
        public Dictionary<HostID, RemoteClient_S> m_players = new();

        // ville name
        public string m_name;

        // increases for every new world object is added.
        public int m_nextNewID = 1;

        // world objects
        public Dictionary<int, WorldObject_S> m_worldObjects = new();

        // every players in this ville are P2P communicated.
        // this is useful for lesser latency for cloud-hosting servers (e.g. Amazon AWS)
        public HostID m_p2pGroupID = HostID.HostID_None;
    }
}
