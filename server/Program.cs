using System;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Net;
using System.Linq;
using System.Collections.Generic;

namespace SpeedTest.Server
{
    class Program
    {

        public static IPAddress Host = IPAddress.Any;
        public static int Port = 25565;
        public static int DefaultBufferSize = 1024;
        public static bool Running = false;
        static void Main(string[] args)
        {
            Task.WaitAll(
            Task.Run(() => TCPThread()),
            Task.Run(() => UDPThread())
            );

        }

        private static void UDPThread()
        {
            Console.WriteLine("Started UDPThread");
            UdpClient client = new UdpClient(Port);
            IPEndPoint RemoteIPEndPoint = new IPEndPoint(IPAddress.Any, 0);
            while (true)
            {
                try
                {
                    Console.WriteLine("UDP Looping");
                    byte[] buffer = client.Receive(ref RemoteIPEndPoint);
                    int bufferSize = BitConverter.ToInt32(buffer, 5);

                    Console.WriteLine("UDP got buffer size at {0}", bufferSize);
                    //client.Client.ReceiveBufferSize = client.Client.SendBufferSize = bufferSize;
                    buffer = new byte[bufferSize];
                    System.Text.ASCIIEncoding.ASCII.GetBytes("READY").CopyTo(buffer, 0);
                    client.Send(buffer, bufferSize, RemoteIPEndPoint);

                    var watch = new System.Diagnostics.Stopwatch();
                    List<byte[]> message = new List<byte[]>();
                    Console.WriteLine("Starting to receive UDP");
                    watch.Start();
                    client.Client.ReceiveTimeout = 5000;
                    while (true)
                    {
                        //Console.Write(" {0} ", messageCount);
                        buffer = client.Receive(ref RemoteIPEndPoint);
                        if (CheckForEndMessage(buffer)) break;

                        byte[] temp = new byte[bufferSize];
                        Array.Copy(buffer, temp, bufferSize);
                        message.Add(temp);
                    }
                    client.Client.ReceiveTimeout = 0;

                    watch.Stop();
                    int failures = 0;
                    int messageCount = 0;
                    foreach (var b in message)
                    {
                        failures += resultChecksum(b, (byte)messageCount);
                        messageCount++;
                    }

                    int bytes = bufferSize * messageCount;
                    double kilobytes = bytes / 1024.00;
                    double rate = kilobytes / watch.Elapsed.TotalSeconds;


                    Console.WriteLine("UDP Thread got {0} kb in {1} s, at a rate of {2} kbps", kilobytes, watch.Elapsed, rate);
                    Console.WriteLine("Obtained {0} malformed bytes, or {1}% of the message.", failures, 100 * failures / bytes);

                }
                catch (Exception e)
                {
                    Console.WriteLine("UDP thread timed out!");
                    Console.WriteLine(e);
                    client.Client.ReceiveTimeout = 0;
                }
            }
        }

        private static int resultChecksum(byte[] buffer, byte messageCount)
        {
            var failures = buffer.Where(b => b != messageCount).AsParallel();
            return failures.Count();
        }

        private static void TCPThread()
        {
            TcpListener listener = new TcpListener(Host, Port);
            listener.Start();
            Console.WriteLine("Started TCP Thread");
            while (true)
            {
                Console.WriteLine("TCP Looping");
                TcpClient client = listener.AcceptTcpClient();
                Task.Run(() => TCPMessageHandler(client));
            }
        }

        private static void TCPMessageHandler(TcpClient client)
        {
            try
            {
                Console.WriteLine("Got TCP Connection");
                byte[] buffer = new byte[DefaultBufferSize];
                NetworkStream stream = client.GetStream();
                stream.Read(buffer, 0, DefaultBufferSize);

                if (Running)
                {
                    Console.WriteLine("TCP Busy");
                    System.Text.ASCIIEncoding.ASCII.GetBytes("BUSY").CopyTo(buffer, 0);
                    stream.Write(buffer, 0, DefaultBufferSize);
                    client.Close();
                    return;
                }
                Running = true;

                int bufferSize = BitConverter.ToInt32(buffer, 5);
                Console.WriteLine("TCP got buffer size at {0}", bufferSize);
                client.Client.ReceiveBufferSize = client.Client.SendBufferSize = bufferSize;
                buffer = new byte[bufferSize];
                System.Text.ASCIIEncoding.ASCII.GetBytes("READY").CopyTo(buffer, 0);
                stream.Write(buffer, 0, bufferSize);

                bool finish = false;
                var watch = new System.Diagnostics.Stopwatch();
                int messageCount = -1;
                Console.WriteLine("Starting to receive TCP");
                watch.Start();
                while (!finish)
                {
                    messageCount++;
                    //Console.Write(" {0} ", messageCount);
                    stream.Read(buffer, 0, bufferSize);
                    finish = CheckForEndMessage(buffer);
                }
                watch.Stop();
                client.Close();
                Running = false;
                int bytes = bufferSize * messageCount;
                double kilobytes = bytes / 1024.00;
                double rate = kilobytes / watch.Elapsed.TotalSeconds;
                Console.WriteLine("TCP Thread got {0} kb in {1} s, at a rate of {2} kbps", kilobytes, watch.Elapsed, rate);
            }
            catch (Exception e)
            {
                Console.WriteLine("UDP thread timed out!");
                Console.WriteLine(e);
                client.Close();
                Running = false;
            }
        }

        private static bool CheckForEndMessage(byte[] buffer)
        {
            return System.Text.Encoding.ASCII.GetString(buffer).Contains("DONE");
        }
    }
}
