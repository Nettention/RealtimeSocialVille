using Nettention.Proud;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SngServer
{
    public class SngServer : IDisposable
    {
        public bool m_runLoop = true;

        // #4
        public NetServer m_netServer = new();

        private readonly ThreadPool m_netWorkerThreadPool = new(8);
        private readonly ThreadPool m_userWorkerThreadPool = new(8);

        // RMI proxy for server-to-client messaging
        internal SocialGameS2C.Proxy m_S2CProxy = new();
        // RMI stub for client-to-server messaging
        internal SocialGameC2S.Stub m_C2SStub = new();

        internal object m_mutex = new();
        private readonly Dictionary<String, Ville_S> m_villes = new();
        // guides which client is in which ville.
        private readonly Dictionary<HostID, Ville_S> m_remoteClients = new();

        public SngServer()
        {
            m_netServer.AttachStub(m_C2SStub);
            m_netServer.AttachProxy(m_S2CProxy);

            m_netServer.ClientJoinHandler = (NetClientInfo clientInfo) =>
            {
                Console.WriteLine("OnClientJoin: {0}", clientInfo.hostID);
            };

            m_netServer.ClientLeaveHandler = (NetClientInfo clientInfo, ErrorInfo errorinfo, ByteArray comment) =>
            {
                lock (m_mutex)
                {
                    Console.WriteLine("OnClientLeave: {0}", clientInfo.hostID);

                    Ville_S ville;

                    // remove the client and play info, and then remove the ville if it is empty.
                    if (m_remoteClients.TryGetValue(clientInfo.hostID, out ville))
                    {
                        ville.m_players.Remove(clientInfo.hostID);
                        m_remoteClients.Remove(clientInfo.hostID);

                        if (ville.m_players.Count == 0)
                        {
                            UnloadVille(ville);
                        }
                    }
                }
            };

            m_netServer.ErrorHandler = (ErrorInfo errorInfo) =>
            {
                Console.WriteLine("OnError! {0}", errorInfo.ToString());
            };

            m_netServer.WarningHandler = (ErrorInfo errorInfo) =>
            {
                Console.WriteLine("OnWarning! {0}", errorInfo.ToString());
            };

            m_netServer.ExceptionHandler = (Exception e) =>
            {
                Console.WriteLine("OnWarning! {0}", e.Message.ToString());
            };

            m_netServer.InformationHandler = (ErrorInfo errorInfo) =>
            {
                Console.WriteLine("OnInformation! {0}", errorInfo.ToString());
            };

            m_netServer.NoRmiProcessedHandler = (RmiID rmiID) =>
            {
                Console.WriteLine("OnNoRmiProcessed! {0}", rmiID);
            };

            m_C2SStub.RequestLogon = RequestLogon;
            m_C2SStub.RequestAddTree = RequestAddTree;
            m_C2SStub.RequestRemoveTree = RequestRemoveTree;
        }

        bool RequestLogon(HostID remote, RmiContext rmiContext, String villeName, bool isNewVille)
        {
            lock (m_mutex)
            {
                // find the appropriate ville and join to it.
                // if not found, then create a new ville.
                Ville_S ville;
                Nettention.Proud.HostID[] list = m_netServer.GetClientHostIDs();

                if (m_villes.TryGetValue(villeName, out ville) == false)
                {
                    // create new one
                    ville = new Ville_S();

                    ville.m_p2pGroupID = m_netServer.CreateP2PGroup(list, new ByteArray()); // empty P2P groups. players will join it.

                    Console.WriteLine("m_p2pGroupID : {0}", ville.m_p2pGroupID);

                    NetClientInfo info = m_netServer.GetClientInfo(list.Last());
                    Console.WriteLine("Client HostID : {0}, IP:Port : {1}:{2}", info.hostID, info.tcpAddrFromServer.IPToString(), info.tcpAddrFromServer.port);

                    // load ville info
                    m_villes.Add(villeName, ville);
                    ville.m_name = villeName;
                }

                m_S2CProxy.ReplyLogon(remote, RmiContext.ReliableSend, (int)ville.m_p2pGroupID, 0, ""); // success
                MoveRemoteClientToLoadedVille(remote, ville);

                return true; // any RMI stub implementation must always return true.
            }
        }

        bool RequestAddTree(HostID remote, RmiContext rmiContext, UnityEngine.Vector3 position)
        {
            lock (m_mutex)
            {
                // find the ville
                Ville_S ville;
                Nettention.Proud.HostID[] list = m_netServer.GetClientHostIDs();
                WorldObject_S tree = new WorldObject_S();
                if (m_remoteClients.TryGetValue(remote, out ville))
                {
                    // add the tree
                    tree.m_position = position;
                    tree.m_id = ville.m_nextNewID;
                    ville.m_worldObjects.Add(tree.m_id, tree);
                    ville.m_nextNewID++;
                }
                else
                {
                    ville = new Ville_S();
                }

                foreach (HostID id in list)
                {
                    // notify the tree's creation to users
                    m_S2CProxy.NotifyAddTree(id, RmiContext.ReliableSend, (int)ville.m_p2pGroupID, tree.m_id, tree.m_position);
                }

                return true;
            }
        }

        bool RequestRemoveTree(HostID remote, RmiContext rmiContext, int treeID)
        {
            lock (m_mutex)
            {
                // find the ville
                Ville_S ville;
                if (m_remoteClients.TryGetValue(remote, out ville))
                {
                    // find the tree
                    WorldObject_S tree;
                    if (ville.m_worldObjects.TryGetValue(treeID, out tree))
                    {
                        ville.m_worldObjects.Remove(treeID);

                        Nettention.Proud.HostID[] list = m_netServer.GetClientHostIDs();
                        foreach (HostID id in list)
                        {
                            // notify the tree's destruction to users
                            m_S2CProxy.NotifyRemoveTree(id, RmiContext.ReliableSend, (int)ville.m_p2pGroupID, tree.m_id);
                        }
                    }
                }

                return true;
            }
        }
        public void Start()
        {
            // fill server startup parameters
            StartServerParameter sp = new();
            sp.protocolVersion = new Nettention.Proud.Guid(SngCommon.Vars.g_sngProtocolVersion);
            sp.tcpPorts = new IntArray();
            sp.tcpPorts.Add(SngCommon.Vars.g_serverPort); // must be same to the port number at client
            sp.udpPorts.Add(9000);
            sp.serverAddrAtClient = "ec2-44-192-247-75.compute-1.amazonaws.com"; // TODO: Modify this value for your own server.
            sp.SetExternalNetWorkerThreadPool(m_netWorkerThreadPool);
            sp.SetExternalUserWorkerThreadPool(m_userWorkerThreadPool);

            //m_netServer.SetDefaultTimeoutTimeMs(1000 * 60 * 60);
            //m_netServer.SetMessageMaxLength(100000, 100000);

            // let's start!
            m_netServer.Start(sp);
        }

        public void MoveRemoteClientToLoadedVille(HostID remote, Ville_S ville)
        {
            lock (m_mutex)
            {
                RemoteClient_S remoteClientValue;
                Ville_S villeValue;

                if (ville.m_players.TryGetValue(remote, out remoteClientValue) == false && m_remoteClients.TryGetValue(remote, out villeValue) == false)
                {
                    ville.m_players.Add(remote, new RemoteClient_S());
                    m_remoteClients.Add(remote, ville);
                }

                // now, the player can do P2P communication with other player in the same ville.
                m_netServer.JoinP2PGroup(remote, ville.m_p2pGroupID);

                // notify current world state to new user
                foreach (var iWorldObject in ville.m_worldObjects)
                {
                    m_S2CProxy.NotifyAddTree(remote, RmiContext.ReliableSend, (int)ville.m_p2pGroupID, iWorldObject.Value.m_id, iWorldObject.Value.m_position);
                }
            }
        }

        public void UnloadVille(Ville_S ville)
        {
            lock (m_mutex)
            {
                // ban the players in the ville
                foreach (KeyValuePair<HostID, RemoteClient_S> iPlayer in ville.m_players)
                {
                    m_netServer.CloseConnection(iPlayer.Key);
                }

                // shutdown the loaded ville
                m_villes.Remove(ville.m_name);

                // release the cached data tree
                m_netServer.DestroyP2PGroup(ville.m_p2pGroupID);
            }
        }

        public void Dispose()
        {
            // NetServer의 경우 프로그램 종료 또는 NetServer 객체 파괴시 명시적으로 NetServer.Dispose() 를 호출해주어야 합니다.
            m_netServer.Dispose();
        }
    }

}
