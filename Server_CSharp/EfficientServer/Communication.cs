//#define LOG
//#define DEBUG_MSG

using Google.Protobuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;
using System.Collections;


namespace EfficientServer
{
    public class Communication
    {
        Graph graph = new Graph();
        private TcpListener _server;
        private bool _isRunning;

        static private ReaderWriterLockSlim lockSlim = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);

        private static int walkCounter = 0;
        private static int locationCounter = 0;

        public Communication(int port)
        {
            _server = new TcpListener(IPAddress.Any, port);
            _server.Start();            
            _isRunning = true;

            //Allocate a thread pool of 50 threads
            ThreadPool.SetMinThreads(80, 80);
        }

        public void Listen()
        {
            while (_isRunning)
            {
                #if LOG
                    Console.WriteLine("Waiting for a connection...");
                #endif
                TcpClient client = _server.AcceptTcpClient();                
                #if LOG
                    Console.WriteLine("Client connected!");
                #endif

                if (client != null)
                {
                    //Handle the client in a new thread
                    ThreadPool.QueueUserWorkItem(HandleClient, client);
                }                
            }
        }

        private void HandleClient(object obj)
        {
            #if LOG
                Console.WriteLine("Handling client...");
            #endif

            TcpClient client = (TcpClient)obj;
            NetworkStream stream = client.GetStream();
            byte[] lengthBytes = new byte[4];
            int read = 0;
            int length = 0;            

            try
            {
                while (true)
                {
                    // Read message length
                    read = stream.Read(lengthBytes, 0, 4);

                    //Convert length from network to host order
                    length = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(lengthBytes, 0));

                    if(read == 0 || length == 0)
                    {
                        #if LOG
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("\nZero read!");
                            Console.ResetColor();
                        #endif
                        break;
                    }
                                        
                    // Read protobuf message
                    byte[] buffer = new byte[length];                    
                    stream.ReadExactly(buffer, 0, length);
                    Request request = Request.Parser.ParseFrom(buffer);

                    //Log the request message
                    #if LOG
                        Console.WriteLine("\nReceived request: ");
                        if(request.MsgCase == Request.MsgOneofCase.Walk)
                        {
                            Console.WriteLine($"Walk, number of locations: {request.Walk.Locations.Count}");
                        }
                        else if(request.MsgCase == Request.MsgOneofCase.OneToOne)
                        {
                            Console.WriteLine($"OneToOne: {request.OneToOne}");
                        }
                        else if(request.MsgCase == Request.MsgOneofCase.OneToAll)
                        {
                            Console.WriteLine($"OneToAll: {request.OneToAll}");
                        }
                        else if(request.MsgCase == Request.MsgOneofCase.Reset)
                        {
                            Console.WriteLine("Reset");
                        }
                        else
                        {
                            Console.WriteLine("Unknown request");
                        }
                    #endif
                    
                    Response response = HandleRequest(request); // Handle the request based on the type

                    // Serialize and send the response
                    byte[] responseLength = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(response.CalculateSize()));
                    stream.Write(responseLength, 0, 4);
                    response.WriteTo(stream);

                    #if LOG
                        Console.WriteLine("Response sent successfully!");
                    #endif
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
            finally
            {
                stream.Close();
                client.Close();

                #if LOG
                    Console.WriteLine("Client disconnected!");
                #endif
            }
        }

        private Response HandleRequest(Request request)
        {
            Response response = new Response { Status = Response.Types.Status.Ok };

            if (request.MsgCase == Request.MsgOneofCase.Walk)
            {
                lockSlim.EnterReadLock();
                try
                {
                    graph.AddWalk(request.Walk);
                }
                finally
                {
                    lockSlim.ExitReadLock();
                }
            }
            else if (request.MsgCase == Request.MsgOneofCase.OneToOne)
            {
                #if DEBUG_MSG
                    Console.WriteLine($"Handling OneToOne request!");
                #endif

                ulong shortestPathLength = 0;
                
                lockSlim.EnterUpgradeableReadLock();
                try
                {
                    lockSlim.EnterWriteLock();
                    try
                    {
                        shortestPathLength = graph.ComputeShortestPathDijkstraImproved(request.OneToOne.Origin, request.OneToOne.Destination);
                    }
                    finally
                    {
                        lockSlim.ExitWriteLock();
                    }
                }
                finally
                {
                    lockSlim.ExitUpgradeableReadLock();
                }                

                if (shortestPathLength > 0)
                {
                    response.ShortestPathLength = shortestPathLength;

                    #if LOG
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"Response is {shortestPathLength}");
                        Console.ResetColor();
                    #endif
                }
                else
                {
                    response.Status = Response.Types.Status.Error;
                    response.ErrMsg = "Path not found";
                }
            }
            else if (request.MsgCase == Request.MsgOneofCase.OneToAll)
            {
                #if DEBUG_MSG
                    Console.WriteLine($"Handling OneToAll request!");
                #endif

                ulong totalLength;
                
                lockSlim.EnterUpgradeableReadLock();
                try
                {
                    lockSlim.EnterWriteLock();
                    try
                    {
                        totalLength = graph.ComputePathsFromImproved(request.OneToAll.Origin);
                    }
                    finally
                    {
                        lockSlim.ExitWriteLock();
                    }
                }
                finally
                {
                    lockSlim.ExitUpgradeableReadLock();
                }

                if (totalLength != 0)
                {                    
                    response.TotalLength = totalLength;

                    #if LOG
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"Response is {totalLength}");
                        Console.ResetColor();
                    #endif
                }
                else
                {
                    response.Status = Response.Types.Status.Error;
                    response.ErrMsg = "Failed to compute paths from origin";
                }
            }
            else if (request.MsgCase == Request.MsgOneofCase.Reset)
            {
                graph.Reset();

                #if LOG
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("Graph has been reset");
                    Console.ResetColor();
                #endif
            }

            return response;
        }
    }
}
