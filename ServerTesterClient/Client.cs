//#define FILE1
//#define FILE2
//#define FILE3
//#define FILE4
//#define FILE5
#define FILE6
//#define CUSTOM

using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using static System.Runtime.InteropServices.JavaScript.JSType;
using Google.Protobuf;
using System.Runtime.CompilerServices;
using System.Net;
using System.IO.Pipes;

namespace EfficientServer
{
    public class ProtobufTcpClient
    {        
        public static void Main()
        {
            string filePath1 = "C:\\MyStuff\\University\\Erasmus\\Prague\\Courses\\Effective software\\Laboratories\\8-EfficientServer\\ServerTesterClient\\walk1nodes3-2.pbf";
            string filePath2 = "C:\\MyStuff\\University\\Erasmus\\Prague\\Courses\\Effective software\\Laboratories\\8-EfficientServer\\ServerTesterClient\\walk1nodes100-2.pbf";
            string filePath3 = "C:\\MyStuff\\University\\Erasmus\\Prague\\Courses\\Effective software\\Laboratories\\8-EfficientServer\\ServerTesterClient\\walk2nodes6.pbf";
            string filePath4 = "C:\\MyStuff\\University\\Erasmus\\Prague\\Courses\\Effective software\\Laboratories\\8-EfficientServer\\ServerTesterClient\\walk3000.pbf";
            string filePath5 = "C:\\MyStuff\\University\\Erasmus\\Prague\\Courses\\Effective software\\Laboratories\\8-EfficientServer\\ServerTesterClient\\walk10nodes500.pbf";
            string filePath6 = "C:\\MyStuff\\University\\Erasmus\\Prague\\Courses\\Effective software\\Laboratories\\8-EfficientServer\\ServerTesterClient\\static.pbf";


            #if FILE1
            TcpClient client1 = new TcpClient("localhost", 12345);
                ThreadPool.QueueUserWorkItem(SendFile, (client1, filePath1));        
            #endif

            #if FILE2
                TcpClient client2 = new TcpClient("localhost", 12345);
                ThreadPool.QueueUserWorkItem(SendFile, (client2 , filePath2));
            #endif

            #if FILE3
                TcpClient client3 = new TcpClient("localhost", 12345);
                ThreadPool.QueueUserWorkItem(SendFile, (client3, filePath3));
            #endif

            #if FILE4
                TcpClient client4 = new TcpClient("localhost", 12345);
                ThreadPool.QueueUserWorkItem(SendFile, (client4, filePath4));
            #endif

            #if FILE5
                TcpClient client5 = new TcpClient("localhost", 12345);
                ThreadPool.QueueUserWorkItem(SendFile, (client5, filePath5));
            #endif

            #if FILE6
                TcpClient client6 = new TcpClient("localhost", 12345);
                ThreadPool.QueueUserWorkItem(SendFile, (client6, filePath6));
            #endif

            #if CUSTOM
                TcpClient clientTest = new TcpClient("localhost", 12345);
                Task taskCustom = Task.Run(() => Test(clientTest));
            #endif

            Console.ReadKey();
        }

        //A method to create a Request object consisting of 5 walks
        private static Request CreateRequest(Request.MsgOneofCase type)
        {
            Request request = new Request();

            if(type == Request.MsgOneofCase.Walk)
            {
                request.Walk = new Walk();

                request.Walk.Locations.Add(new Location { X = 0, Y = 0 });
                request.Walk.Locations.Add(new Location { X = 10000, Y = 10000 });
                request.Walk.Lengths.Add(10000);
                request.Walk.Locations.Add(new Location { X = 20000, Y = 20000 });
                request.Walk.Lengths.Add(10000);
                request.Walk.Locations.Add(new Location { X = 30000, Y = 30000 });
                request.Walk.Lengths.Add(10000);
                request.Walk.Locations.Add(new Location { X = 40000, Y = 40000 });
                request.Walk.Lengths.Add(10000);
                request.Walk.Locations.Add(new Location { X = 50000, Y = 50000 });
                request.Walk.Lengths.Add(10000);
                request.Walk.Locations.Add(new Location { X = 60000, Y = 60000 });
                request.Walk.Lengths.Add(10000);
                request.Walk.Locations.Add(new Location { X = 70000, Y = 70000 });
                request.Walk.Lengths.Add(10000);
                request.Walk.Locations.Add(new Location { X = 80000, Y = 80000 });
                request.Walk.Lengths.Add(10000);
                request.Walk.Locations.Add(new Location { X = 90000, Y = 90000 });
                request.Walk.Lengths.Add(10000);
                request.Walk.Locations.Add(new Location { X = 100000, Y = 100000 });
                request.Walk.Lengths.Add(10000);
            }
            else if(type == Request.MsgOneofCase.OneToOne)
            {
                request.OneToOne = new OneToOne();
                request.OneToOne.Origin = new Location { X = 10010, Y = 10005 };
                request.OneToOne.Destination = new Location { X = 20020, Y = 20001 };
            }
            else if(type == Request.MsgOneofCase.OneToAll)
            {
                request.OneToAll = new OneToAll();
                request.OneToAll.Origin = new Location { X = 0, Y = 0 };
            }
            else if(type == Request.MsgOneofCase.Reset)
            {
                request.Reset = new Reset();
            }
            
            return request;
        }

        private static void Test(TcpClient client)
        {
            Console.WriteLine("Starting the client...");

            Request request;

            try
            {
                using (var stream = client.GetStream())
                {
                    Console.WriteLine($"Sending walk request");
                    request = CreateRequest(Request.MsgOneofCase.Walk);
                    SendRequest(stream, request);
                    ReceiveResponse(stream);

                    Console.WriteLine($"Sending OneToOne request");
                    request = CreateRequest(Request.MsgOneofCase.OneToOne);
                    SendRequest(stream, request);
                    ReceiveResponse(stream);

                    Console.WriteLine($"Sending OneToAll request");
                    request = CreateRequest(Request.MsgOneofCase.OneToAll);
                    SendRequest(stream, request);
                    ReceiveResponse(stream);                    
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }

            finally
            {
                client.Close();
            }
        }

        private static void ReceiveResponse(NetworkStream stream)
        {
            byte[] responseLength = new byte[4];
            stream.Read(responseLength, 0, 4);
            int responseSize = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(responseLength, 0));
            if(responseSize != 0)
            {
                Console.WriteLine($"Response size: {responseSize}");

                byte[] responseBytes = new byte[responseSize];
                stream.Read(responseBytes, 0, responseSize);
                Response response = Response.Parser.ParseFrom(responseBytes);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Response: {response}");
                Console.ResetColor();
            }
        }

        private static void SendRequest(NetworkStream stream, Request request)
        {
            byte[] requestLength = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(request.CalculateSize()));
            stream.Write(requestLength, 0, 4);
            request.WriteTo(stream);
            Console.WriteLine("Request has been sent successfully.");
        }

        private static void SendFile(object ob)
        {
            Console.WriteLine($"Sending file to the server from task....");

            (TcpClient client, string filePath) = ((TcpClient, string))ob;

            try
            {
                using (var networkStream = client.GetStream())
                using (var fileStream = File.OpenRead(filePath))
                {
                    long count = fileStream.Length;

                    Console.WriteLine($"Sending {count} bytes to the server...");

                    // File's content is sent as it is
                    fileStream.CopyTo(networkStream);
                    Console.WriteLine("File has been sent successfully.");

                    while(true)
                    {
                        ReceiveResponse(networkStream);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }

            finally
            {
                client.Close();
            }
        }
    }
}

