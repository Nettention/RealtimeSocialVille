using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SngCommon
{
    public class Vars
    {
        // SngClient/Assets/Scripts 안의 값과 동일해야 함
        public static System.Guid g_sngProtocolVersion = new System.Guid("{0x4ea36ea0,0x3900,0x4b1d,{0xbb,0xde,0x3f,0xbf,0x42,0xf4,0xa,0x6b}}");
        public static int g_serverPort = 15001;

        static Vars()
        {
            
        }
    }
}
