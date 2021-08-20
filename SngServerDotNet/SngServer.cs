using Nettention.Proud;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace SngServer
{
    public class SngServer : IDisposable
    {
        public NetServer m_netServer = new();

        private readonly ThreadPool m_netWorkerThreadPool = new(8);
        private readonly ThreadPool m_userWorkerThreadPool = new(8);

        // RMI proxy for server-to-client messaging
        internal SocialGameS2C.Proxy m_S2CProxy = new();
        // RMI stub for client-to-server messaging
        internal SocialGameC2S.Stub m_C2SStub = new();

        internal object m_mutex = new();
        private readonly Dictionary<string, Ville_S> m_nameToVilleMap = new();
        // guides which client is in which ville.
        private readonly Dictionary<HostID, Ville_S> m_clientToVilleMap = new();

        public SngServer()
        {
            m_netServer.AttachStub(m_C2SStub);
            m_netServer.AttachProxy(m_S2CProxy);

            m_netServer.ClientJoinHandler = (NetClientInfo clientInfo) =>
            {
                Console.WriteLine("OnClientJoin: {0}", clientInfo.hostID);
            };

            m_netServer.ClientLeaveHandler = OnClientLeave; 

            m_netServer.ErrorHandler = (ErrorInfo errorInfo) =>
            {
                Console.WriteLine($"OnError: {errorInfo}");
            };

            m_netServer.WarningHandler = (ErrorInfo errorInfo) =>
            {
                Console.WriteLine($"OnWarning: {errorInfo}");
            };

            m_netServer.ExceptionHandler = (Exception e) =>
            {
                Console.WriteLine($"OnWarning: {e.Message}");
            };

            m_netServer.InformationHandler = (ErrorInfo errorInfo) =>
            {
                Console.WriteLine($"OnInformation: {errorInfo}");
            };

            m_netServer.NoRmiProcessedHandler = (RmiID rmiID) =>
            {
                Console.WriteLine($"OnNoRmiProcessed: {rmiID}");
            };

            m_C2SStub.RequestLogon = RequestLogon;
            m_C2SStub.RequestAddTree = RequestAddTree;
            m_C2SStub.RequestRemoveTree = RequestRemoveTree;
        }

        void OnClientLeave(NetClientInfo clientInfo, ErrorInfo errorinfo, ByteArray comment)
        {
            lock (m_mutex)
            {
                Console.WriteLine("OnClientLeave: {0}", clientInfo.hostID);

                // remove the client and play info, and then remove the ville if it is empty.
                if (m_clientToVilleMap.TryGetValue(clientInfo.hostID, out var ville))
                {
                    ville.m_players.Remove(clientInfo.hostID);
                    m_clientToVilleMap.Remove(clientInfo.hostID);

                    if (ville.m_players.Count == 0)
                    {
                        UnloadVille(ville);
                    }
                }
            }
        }

        bool RequestLogon(HostID remote, RmiContext rmiContext, String villeName, bool isNewVille)
        {
            lock (m_mutex)
            {
                if(m_clientToVilleMap.TryGetValue(remote, out var ville)==true)
                {
                    m_S2CProxy.ReplyLogon(remote, RmiContext.ReliableSend, (int)0, false, "Already in a Ville."); // success
                    return true;
                }

                // find the appropriate ville and join to it.
                // if not found, then create a new ville.
                if (m_nameToVilleMap.TryGetValue(villeName, out ville))
                {
                    // the player should yet to enter the ville.
                    Debug.Assert(ville.m_players.ContainsKey(remote) == false);
                }
                else
                {
                    // create new one
                    ville = new Ville_S
                    {
                        // create a new P2P group. players will enter it.
                        m_p2pGroupID = m_netServer.CreateP2PGroup(new HostID[]{}, new ByteArray()), 
                        m_name = villeName
                    };

                    // add the new Ville to map
                    m_nameToVilleMap.Add(villeName, ville);
                }
                
                ville.m_players.Add(remote, new RemoteClient_S());
                m_clientToVilleMap.Add(remote, ville);

                // now, the player can do P2P communication with other player in the same ville.
                m_netServer.JoinP2PGroup(remote, ville.m_p2pGroupID);

                m_S2CProxy.ReplyLogon(remote, RmiContext.ReliableSend, (int)ville.m_p2pGroupID, true, ""); // success

                // notify current world state to new user
                foreach (var obj in ville.m_worldObjects)
                {
                    m_S2CProxy.NotifyAddTree(remote, RmiContext.ReliableSend, obj.Value.m_id, obj.Value.m_position);
                }

                return true; // any RMI stub implementation must always return true.
            }
        }

        bool RequestAddTree(HostID remote, RmiContext rmiContext, UnityEngine.Vector3 position)
        {
            lock (m_mutex)
            {
                // find the ville
                if (m_clientToVilleMap.TryGetValue(remote, out var ville) == false)
                    return true;    // nothing to do. just end this function.

                // add the tree
                WorldObject_S tree = new();
                tree.m_position = position;
                tree.m_id = ville.m_nextNewID;

                ville.m_worldObjects.Add(tree.m_id, tree);
                ville.m_nextNewID++;

                // notify the tree's creation to users
                m_S2CProxy.NotifyAddTree(ville.m_players.Keys.ToArray(), RmiContext.ReliableSend, tree.m_id, tree.m_position);

                return true;
            }
        }

        bool RequestRemoveTree(HostID remote, RmiContext rmiContext, int treeID)
        {
            lock (m_mutex)
            {
                // find the ville
                Ville_S ville;
                if (m_clientToVilleMap.TryGetValue(remote, out ville) == false)
                    return true;    // nothing to do. just end this function.

                // find the tree
                WorldObject_S tree;
                if (ville.m_worldObjects.TryGetValue(treeID, out tree) == false)
                    return true;

                ville.m_worldObjects.Remove(treeID);

                // notify the tree's destruction to users
                m_S2CProxy.NotifyRemoveTree(ville.m_players.Keys.ToArray(), RmiContext.ReliableSend, tree.m_id);

                return true;
            }
        }

        public void Start()
        {
            // each Ville will have an empty P2P group first, so we allow empty P2P group option.
            m_netServer.AllowEmptyP2PGroup(true);

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

        

        public void UnloadVille(Ville_S ville)
        {
            lock (m_mutex)
            {
                // kick out the players in the ville
                foreach (KeyValuePair<HostID, RemoteClient_S> iPlayer in ville.m_players)
                {
                    m_netServer.CloseConnection(iPlayer.Key);
                }

                // shutdown the loaded ville
                m_nameToVilleMap.Remove(ville.m_name);

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
