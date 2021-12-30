//using System;
//using System.Net;
//using System.Net.Sockets;
//using System.Text;
//using System.Text.Json;
//using System.Threading;

//namespace AppKettle
//{



//    class Program
//    {
//        private static bool turnedOn = false;

//        static void Main(string[] args)
//        {
//            var ak = AppKettle.DiscoverKettle("10.183.2.255").Result;

//            Console.WriteLine($"Found kettle at IP: {ak.IpAddress} (IMEI: {ak.Imei})");

//            while (true)
//            {
//                ak.Connect().Wait();
//                ak.Off().Wait();
//                while (ak.Connected)
//                {
//                    Console.WriteLine($"Kettle Temp: {ak.CurrentTemp}, {ak.Volume}ml - {ak.State}");
//                    Thread.Sleep(1000);

//                    if(!turnedOn && (ak.CurrentTemp > 0))
//                    {
//                        turnedOn = true;
//                        Console.WriteLine("Turning on kettle");
                        
//                        if (ak.State == KettleState.Standby)
//                        {
//                            ak.Wake().Wait();
//                        }

//                        ak.Off().Wait();
//                    }
//                }
//                Console.WriteLine("Disconnected - will reconnect");
//                Thread.Sleep(1000);
//            }

//        }

//    }
//}
