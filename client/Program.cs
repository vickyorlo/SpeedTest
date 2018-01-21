using System;
using System.Net;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Sockets;

namespace client
{
    class Program
    {
        public static IPAddress server = IPAddress.Parse("127.0.0.1");
        public static int port = 25565;
        public static int DefaultBufferSize = 1024;
        public static int bufferSize = 1024;
        public static bool useNagle = true;

        public static int messageLengthInBuffers = 1000;
        static void Main(string[] args)
        {
            try
            {
                server = IPAddress.Parse(args[0]);
                port = Int32.Parse(args[1]);
                messageLengthInBuffers = Int32.Parse(args[2]);
                useNagle = (args[3] == "yes");
            }
            catch (Exception)
            {
                Console.WriteLine("Invalid arguments!");
            }

            List<byte[]> message = PrepareMessage();

            Task.WaitAll(
            Task.Run(() => TCPSend(message)),
            Task.Run(() => UDPSend(message))
            );
        }

        private static void UDPSend(List<byte[]> message)
        {
            try
            {
                Console.WriteLine("UDP thread started");
                UdpClient client = new UdpClient();
                client.Connect(server, port);

                IPEndPoint host = new IPEndPoint(server, port);
                IPEndPoint remoteIPEndPoint = new IPEndPoint(IPAddress.Any, 0);

                byte[] buffer = new byte[DefaultBufferSize];
                System.Text.Encoding.ASCII.GetBytes("SIZE:").Concat(BitConverter.GetBytes(bufferSize)).ToArray().CopyTo(buffer, 0);
                client.Send(buffer, DefaultBufferSize);
                Console.WriteLine("UDP Sent buffer size");
                //client.Client.ReceiveBufferSize = client.Client.SendBufferSize = bufferSize;

                buffer = new byte[bufferSize];
                buffer = client.Receive(ref remoteIPEndPoint);
                if (System.Text.Encoding.ASCII.GetString(buffer).Substring(0, 10).Contains("READY"))
                {
                    Console.WriteLine("UDP r2g");
                    foreach (var packet in message)
                    {
                        client.Send(packet, bufferSize);
                        System.Threading.Thread.Sleep(1);
                    }

                    System.Text.Encoding.ASCII.GetBytes("DONE").CopyTo(buffer, 0);
                    client.Send(buffer, bufferSize);
                    Console.WriteLine("UDP DONE");
                }
                Console.WriteLine("UDP Thread quitting");
            }
            catch (Exception)
            {
                Console.WriteLine("UDP thread failed!");
            }
        }

        private static void TCPSend(List<byte[]> message)
        {
            try
            {
                Console.WriteLine("TCP thread started");
                TcpClient client = new TcpClient();
                client.Connect(server, port);

                Console.WriteLine("TCP Connected");
                client.Client.NoDelay = useNagle;
                var session = client.GetStream();

                byte[] buffer = new byte[DefaultBufferSize];
                System.Text.Encoding.ASCII.GetBytes("SIZE:").Concat(BitConverter.GetBytes(bufferSize)).ToArray().CopyTo(buffer, 0);
                session.Write(buffer, 0, DefaultBufferSize);
                Console.WriteLine("TCP sent buffer size");
                //client.Client.ReceiveBufferSize = client.Client.SendBufferSize = bufferSize;

                buffer = new byte[bufferSize];
                session.Read(buffer, 0, bufferSize);
                string data = System.Text.Encoding.ASCII.GetString(buffer);
                if (System.Text.Encoding.ASCII.GetString(buffer).Substring(0, 10).Contains("READY"))
                {
                    Console.WriteLine("TCP r2g");
                    foreach (var packet in message)
                    {
                        session.Write(packet, 0, bufferSize);
                        System.Threading.Thread.Sleep(1);
                    }
                    System.Text.Encoding.ASCII.GetBytes("DONE").CopyTo(buffer, 0);
                    session.Write(buffer, 0, bufferSize);
                    Console.WriteLine("TCP DONE");
                }
                else
                {
                    Console.WriteLine("TCP server is busy!");
                }
                Console.WriteLine("TCP thread quitting");

            }
            catch (Exception)
            {
                Console.WriteLine("TCP thread failed!");
            }
        }

        static List<byte[]> PrepareMessage()
        {
            var message = new List<byte[]>();

            for (int i = 0; i < messageLengthInBuffers; i++)
            {
                message.Add(System.Linq.Enumerable.Repeat((byte)i, bufferSize).ToArray());
            }

            return message;
        }
    }
}
