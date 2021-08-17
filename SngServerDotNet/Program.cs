using System;
using System.Threading;

namespace SngServer
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("<ProudNet Realtime Social Ville Game Server>");
            Console.WriteLine("ESC: Quit");

            using (SngServer server = new())
            {
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
                    Console.WriteLine($"Exception: {e.Message}");
                }
            }
        }
    }
}
