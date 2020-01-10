using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Nettention.Proud;

namespace SngServer
{
    public class SngServer
    {
        public bool m_runLoop;

        public NetServer m_netServer = new NetServer();

        private Nettention.Proud.ThreadPool netWorkerThreadPool = new Nettention.Proud.ThreadPool(8);
        private Nettention.Proud.ThreadPool userWorkerThreadPool = new Nettention.Proud.ThreadPool(8);

        // RMI proxy for server-to-client messaging
        internal SocialGameS2C.Proxy m_S2CProxy = new SocialGameS2C.Proxy();
        // RMI stub for client-to-server messaging
        internal SocialGameC2S.Stub m_C2SStub = new SocialGameC2S.Stub();

        private ConcurrentDictionary<String, Ville_S> m_villes = new ConcurrentDictionary<string, Ville_S>();
        // guides which client is in which ville.
        private ConcurrentDictionary<HostID, Ville_S> m_remoteClients = new ConcurrentDictionary<HostID, Ville_S>();

        public SngServer()
        {
            m_runLoop = true;

            m_netServer.AttachStub(m_C2SStub);
            m_netServer.AttachProxy(m_S2CProxy);

            m_netServer.ConnectionRequestHandler = (AddrPort clientAddr, ByteArray userDataFromClient, ByteArray reply) =>
            {
                reply = new ByteArray();
                reply.Clear();
                return true;
            };

            m_netServer.ClientHackSuspectedHandler = (HostID clientID, HackType hackType) =>
            {

            };

            m_netServer.ClientJoinHandler = (NetClientInfo clientInfo) =>
            {
                Console.WriteLine("OnClientJoin: {0}", clientInfo.hostID);
            };

            m_netServer.ClientLeaveHandler = (NetClientInfo clientInfo, ErrorInfo errorinfo, ByteArray comment) =>
            {
                Console.WriteLine("OnClientLeave: {0}", clientInfo.hostID);
                
                Monitor.Enter(this);

                Ville_S ville;

                // remove the client and play info, and then remove the ville if it is empty.
                if (m_remoteClients.TryGetValue(clientInfo.hostID, out ville))
                {
                    RemoteClient_S clientValue;
                    Ville_S villeValue;

                    ville.m_players.TryRemove(clientInfo.hostID, out clientValue);
                    m_remoteClients.TryRemove(clientInfo.hostID, out villeValue);

                    if (ville.m_players.Count == 0)
                    {
                        UnloadVille(ville);
                    }
                }

                Monitor.Exit(this);
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

            m_netServer.P2PGroupJoinMemberAckCompleteHandler = (HostID groupHostID, HostID memberHostID, ErrorType result) =>
            {

            };

            m_netServer.TickHandler = (object context) =>
            {

            };

            m_netServer.UserWorkerThreadBeginHandler = () =>
            {

            };

            m_netServer.UserWorkerThreadEndHandler = () =>
            {

            };

            m_C2SStub.RequestLogon = (HostID remote, RmiContext rmiContext, String villeName, bool isNewVille) =>
            {
                Monitor.Enter(this);

                // find the appropriate ville and join to it.
                // if not found, then create a new ville.
                Ville_S ville;
                Nettention.Proud.HostID[] list = m_netServer.GetClientHostIDs();

                if (!m_villes.TryGetValue(villeName, out ville))
                {
                    // create new one
                    ville = new Ville_S();
                    
                    ville.m_p2pGroupID = m_netServer.CreateP2PGroup(list, new ByteArray()); // empty P2P groups. players will join it.

                    Console.WriteLine("m_p2pGroupID : {0}", ville.m_p2pGroupID);

                    NetClientInfo info = m_netServer.GetClientInfo(list.Last());
                    Console.WriteLine("Client HostID : {0}, IP:Port : {1}:{2}", info.hostID, info.tcpAddrFromServer.IPToString(), info.tcpAddrFromServer.port);

                    // load ville info
                    m_villes.TryAdd(villeName, ville);
                    ville.m_name = villeName;
                }

                m_S2CProxy.ReplyLogon(remote, RmiContext.ReliableSend, (int)ville.m_p2pGroupID, 0, ""); // success
                MoveRemoteClientToLoadedVille(remote, ville);

                Monitor.Exit(this);

                return true; // any RMI stub implementation must always return true.
            };

            m_C2SStub.RequestAddTree = (HostID remote, RmiContext rmiContext, UnityEngine.Vector3 position) =>
            {
                Monitor.Enter(this);

                // find the ville
                Ville_S ville;
                Nettention.Proud.HostID[] list = m_netServer.GetClientHostIDs();
                WorldObject_S tree = new WorldObject_S();
                if (m_remoteClients.TryGetValue(remote, out ville))
                {
                    // add the tree
                    tree.m_position = position;
                    tree.m_id = ville.m_nextNewID;
                    ville.m_worldObjects.TryAdd(tree.m_id, tree);
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

                Monitor.Exit(this);

                return true;
            };

            m_C2SStub.RequestRemoveTree = (HostID remote, RmiContext rmiContext, int treeID) =>
            {
                Monitor.Enter(this);

                // find the ville
                Ville_S ville;
                if (m_remoteClients.TryGetValue(remote, out ville))
                {
                    // find the tree
                    WorldObject_S tree;
                    if (ville.m_worldObjects.TryGetValue(treeID, out tree))
                    {
                        WorldObject_S obj;
                        ville.m_worldObjects.TryRemove(treeID, out obj);

                        Nettention.Proud.HostID[] list = m_netServer.GetClientHostIDs();
                        foreach (HostID id in list)
                        {
                            // notify the tree's destruction to users
                            m_S2CProxy.NotifyRemoveTree(id, RmiContext.ReliableSend, (int)ville.m_p2pGroupID, tree.m_id);
                        }
                    }
                }

                Monitor.Exit(this);

                return true;
            };
        }

        public void Start()
        {
            // fill server startup parameters
            StartServerParameter sp = new StartServerParameter();
            sp.protocolVersion = new Nettention.Proud.Guid(SngCommon.Vars.g_sngProtocolVersion);
            sp.tcpPorts = new IntArray();
            sp.tcpPorts.Add(SngCommon.Vars.g_serverPort); // must be same to the port number at client
            sp.serverAddrAtClient = "192.168.77.138";
            sp.localNicAddr = "192.168.77.138";
            sp.SetExternalNetWorkerThreadPool(netWorkerThreadPool);
            sp.SetExternalUserWorkerThreadPool(userWorkerThreadPool);

            //m_netServer.SetDefaultTimeoutTimeMs(1000 * 60 * 60);
            //m_netServer.SetMessageMaxLength(100000, 100000);

            // let's start!
            m_netServer.Start(sp);
        }

        public void MoveRemoteClientToLoadedVille(HostID remote, Ville_S ville)
        {
            RemoteClient_S remoteClientValue;
            Ville_S villeValue;

            if (!ville.m_players.TryGetValue(remote, out remoteClientValue) && !m_remoteClients.TryGetValue(remote, out villeValue))
            {
                ville.m_players.TryAdd(remote, new RemoteClient_S());
                m_remoteClients.TryAdd(remote, ville);
            }

            // now, the player can do P2P communication with other player in the same ville.
            m_netServer.JoinP2PGroup(remote, ville.m_p2pGroupID);

            // notify current world state to new user
            foreach (KeyValuePair<int, WorldObject_S> iWO in ville.m_worldObjects)
            {
                m_S2CProxy.NotifyAddTree(remote, RmiContext.ReliableSend, (int)ville.m_p2pGroupID, iWO.Value.m_id, iWO.Value.m_position);
            }
        }

        public void UnloadVille(Ville_S ville)
        {
            // ban the players in the ville
            foreach (KeyValuePair<HostID, RemoteClient_S> iPlayer in ville.m_players)
            {
                m_netServer.CloseConnection(iPlayer.Key);
            }

            Ville_S villeValue;

            // shutdown the loaded ville
            m_villes.TryRemove(ville.m_name, out villeValue);

            // release the cached data tree
            m_netServer.DestroyP2PGroup(ville.m_p2pGroupID);
        }

        public void Dispose()
        {
            // NetServer의 경우 프로그램 종료 또는 NetServer 객체 파괴시 명시적으로 NetServer.Dispose() 를 호출해주어야 합니다.
            m_netServer.Dispose();
        }
    }
    
}
