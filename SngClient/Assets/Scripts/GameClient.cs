using Nettention.Proud;
using UnityEngine;

public partial class GameClient : MonoBehaviour
{
    string m_serverAddr = "localhost";

    // world name. you may consider it as user name.
    string m_villeName = "Ville";

    // text to be shown on the button
    string m_loginButtonText = "Connect";

    // uses while the scene is 'error mode'
    public string m_failMessage = "";

    // #1
    NetClient m_netClient = new NetClient();

    // for sending client-to-server messages
    // and the vice versa.
    SocialGameC2S.Proxy m_C2SProxy = new SocialGameC2S.Proxy();
    SocialGameS2C.Stub m_S2CStub = new SocialGameS2C.Stub();

    // for P2P communication
    SocialGameC2C.Proxy m_C2CProxy = new SocialGameC2C.Proxy();
    SocialGameC2C.Stub m_C2CStub = new SocialGameC2C.Stub();

    public enum State
    {
        Standby,
        Connecting,
        LoggingOn, // After connecting done. only connecting to server does net say logon completion.
        InVille, // after logon successful. in main game mode.
        Failed,
    }

    public State m_state = State.Standby;

    // Use this for initialization
    void Start()
    {
        m_S2CStub.ReplyLogon = ReplyLogon;
        m_S2CStub.NotifyAddTree = NotifyAddTree;
        m_S2CStub.NotifyRemoveTree = NotifyRemoveTree;
        m_C2CStub.ScribblePoint = ScribblePoint;
    }

    // Update is called once per frame
    void Update()
    {
        m_netClient.FrameMove();

        switch (m_state)
        {
            case State.InVille:
                Update_InVille();
                break;
        }
    }

    public void OnDestroy()
    {
        m_netClient.Dispose();
    }

    public void OnGUI()
    {
        switch (m_state)
        {
            case State.Standby:
            case State.Connecting:
            case State.LoggingOn:
                OnGUI_Logon();
                break;
            case State.InVille:
                OnGUI_InVille();
                break;
            case State.Failed:
                GUI.Label(new Rect(10, 30, 200, 80), m_failMessage);
                if (GUI.Button(new Rect(10, 100, 180, 30), "Quit"))
                {
                    Application.Quit();
                }
                break;
        }

    }

    void OnGUI_Logon()
    {
        GUI.Label(new Rect(10, 10, 300, 70), "ProudNet sample: \nA Quite Basic Realtime Social Ville");
        GUI.Label(new Rect(10, 60, 180, 30), "Server Address");
        m_serverAddr = GUI.TextField(new Rect(10, 80, 180, 30), m_serverAddr);
        GUI.Label(new Rect(10, 110, 180, 30), "World Name");
        m_villeName = GUI.TextField(new Rect(10, 130, 180, 30), m_villeName);

        // if button is clicked
        if (GUI.Button(new Rect(10, 190, 100, 30), m_loginButtonText))
        {
            if (m_state == State.Standby)
            {
                m_state = State.Connecting;
                m_loginButtonText = "Connecting...";
                IssueConnect(); // attemp to connect and logon
            }
        }
    }

    private void IssueConnect()
    {
        // prepare network client
        m_netClient.AttachProxy(m_C2SProxy);
        m_netClient.AttachStub(m_S2CStub);

        m_netClient.AttachProxy(m_C2CProxy);
        m_netClient.AttachStub(m_C2CStub);
       
        // #2
        m_netClient.JoinServerCompleteHandler = (ErrorInfo info, ByteArray replyFromServer) =>
            {
                if (info.errorType == ErrorType.Ok)
                {
                    m_state = State.LoggingOn;
                    m_loginButtonText = "Logging on...";

                    // try to join the specified ville by name given by the user.
                    m_C2SProxy.RequestLogon(HostID.HostID_Server, RmiContext.ReliableSend, m_villeName, false);
                }
                else
                {
                    m_state = State.Failed;                    
                    m_failMessage = "Connect failed: " + info.errorType.ToString();
                }
            };

        // if the server connection is down, we should prepare for exit.
        m_netClient.LeaveServerHandler = (ErrorInfo info) =>
            {
                m_state = State.Failed;
                m_failMessage = "Disconnected from server: " + info.errorType.ToString();
            };

        // #3
        //fill parameters and go
        NetConnectionParam cp = new NetConnectionParam();
        cp.serverIP = m_serverAddr;
        cp.serverPort = 15001;
        cp.protocolVersion = new Guid("{0x4ea36ea0,0x3900,0x4b1d,{0xbb,0xde,0x3f,0xbf,0x42,0xf4,0xa,0x6b}}");

        m_netClient.Connect(cp);
    }
}
