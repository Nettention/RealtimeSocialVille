using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SngServer
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("<ProudNet Realtime Social Game Server>");
            Console.WriteLine("ESC: Quit");

            SngServer server = new SngServer();

            try
            {
                server.Start();
                Console.WriteLine("Server start ok.");

                while (server.m_runLoop)
                {
                    if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape)
                    {
                        break;
                    }

                    Thread.Sleep(1000);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("{0}", e.Message.ToString());
            }
            finally
            {
                server.Dispose();
            }
        }
    }
}
