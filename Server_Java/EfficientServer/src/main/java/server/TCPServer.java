package server;
import server.proto.*;

import java.io.*;
import java.net.*;
import java.nio.ByteBuffer;
import java.util.concurrent.*;
import java.util.*;
import java.util.concurrent.locks.*;


public class TCPServer {
    private static final int PORT = 12345; //Define the port number for the server
    private static final Graph graph = new Graph();
    private static final ExecutorService clientHandlerPool = Executors.newFixedThreadPool(100); //Allocating a thread pool of 100 threads for handling client connections

    //Synchronization primitives for managing request concurrency
    private static final Lock lock = new ReentrantLock();
    private static final Condition condition = lock.newCondition();
    private static int activeProcessWalks = 0;
    private static boolean oneToOperationInProgress = false;

    public static void main(String[] args) {
        System.out.println("Server started...");
        try (ServerSocket serverSocket = new ServerSocket(PORT)) {
            //Check for incoming connections constantly
            while (true) {
                Socket clientSocket = serverSocket.accept();
                clientHandlerPool.submit(() -> handleClient(clientSocket)); //Handle the new client in one of the threads in the threadpool
            }
        } catch (IOException e) {
            System.out.println("Server error: " + e.getMessage());
        }
    }

    /**
     * Handle a client from reading the request, processing it, constructing a response message and sending it back
     * Closing the socket when the client the connection
     * @param clientSocket - the allocated socket to the current client
     */
    private static void handleClient(Socket clientSocket) {
        //System.out.println("Client connected");
        try (InputStream in = clientSocket.getInputStream(); OutputStream out = clientSocket.getOutputStream()) {
            while (true) {
                //Read the length of the message
                byte[] lengthBytes = new byte[4];
                if (in.readNBytes(lengthBytes, 0, 4) != 4) {
                    //System.out.println("Zero read");
                    break;
                }
                int length = ByteBuffer.wrap(lengthBytes).getInt();

                //Read the actual message
                byte[] messageBytes = new byte[length];
                if (in.readNBytes(messageBytes, 0, length) != length) {
                    throw new Exception("Can't read enough bytes from the stream");
                }

                Request request = Request.parseFrom(messageBytes); //Parse the read in protobuf message
                Response response = processRequest(request); //Process the message based on its type
                byte[] responseBytes = response.toByteArray();
                try{
                    //Write back the response to the client
                    out.write(ByteBuffer.allocate(4).putInt(responseBytes.length).array());
                    out.write(responseBytes);
                    out.flush();
                    //System.out.println("Response sent: " + response.toString());
                }
                catch(IOException e){
                    System.out.println("Response error: " + e.getMessage());
                    break;
                }
            }
        } catch (Exception e) {
            System.out.println("Client error: " + e.getMessage());
        }
        finally {
            try {
                clientSocket.close();
            } catch (IOException e) {
                System.out.println("Error closing socket: " + e.getMessage());
            }
            //System.out.println("Client disconnected");
        }
    }

    /**
     * Handling every request message based on the request type
     * @param request - the received request parsed from the protobuf message
     * @return the constructed response protobuf message
     */
    private static Response processRequest(Request request) {
        Response.Builder responseBuilder = Response.newBuilder();
        try{
            //Handling Walk requests
            if (request.hasWalk()) {
                //System.out.println("Walk request received");
                try{
                    processWalk(request.getWalk());
                    responseBuilder.setStatus(Response.Status.OK);
                }catch(Exception e){
                    System.out.println("Walk error: " + e.getMessage());
                    responseBuilder.setStatus(Response.Status.ERROR).setErrMsg(e.getMessage());
                }

            //Handling OnToOne requests
            } else if (request.hasOneToOne()) {
                //System.out.println("OneToOne request received");
                try {
                    long length = processOneToOne(request.getOneToOne());
                    if(length != -1){
                        responseBuilder.setStatus(Response.Status.OK).setShortestPathLength(length);
                    }
                    else
                    {
                        responseBuilder.setStatus(Response.Status.ERROR).setErrMsg("Can't compute shortest path");
                    }
                }catch(Exception e){
                    System.out.println("OneToOne error: " + e.getMessage());
                    responseBuilder.setStatus(Response.Status.ERROR).setErrMsg(e.getMessage());
                }

            //Handling OneToAll requests
            } else if (request.hasOneToAll()) {
                //System.out.println("OneToAll request received");
                try {
                    long totalLength = processOneToAll(request.getOneToAll());
                    if(totalLength != -1){
                        responseBuilder.setStatus(Response.Status.OK).setTotalLength(totalLength);
                    }
                    else {
                        responseBuilder.setStatus(Response.Status.ERROR).setErrMsg("Can't compute total length");
                    }
                }catch(Exception e){
                    System.out.println("OneToAll error: " + e.getMessage());
                    responseBuilder.setStatus(Response.Status.ERROR).setErrMsg(e.getMessage());
                }

            //Handling Reset requests
            } else if (request.hasReset()) {
                System.out.println("Reset request received");
                try {
                    graph.reset();
                    responseBuilder.setStatus(Response.Status.OK);
                }
                catch(Exception e){
                    System.out.println("Reset error: " + e.getMessage());
                    responseBuilder.setStatus(Response.Status.ERROR).setErrMsg(e.getMessage());
                }
            } else {
                responseBuilder.setStatus(Response.Status.ERROR).setErrMsg("Unknown request type");
            }
        }catch(Exception e)
        {
            System.out.println("Error during processing request: " + e.getMessage());
        }

        return responseBuilder.build();
    }


    /**
     * --- Handle Walk requests ---
     * Multiple Walk requests can be handled simultaneously, but can't start the process of a new one
     * until a received OneTo* request is finished
     * @param walk - the received OneToAll request
     */
    private static void processWalk(Walk walk) {
        lock.lock();
        try{
            while(oneToOperationInProgress) {
                condition.await(); //Wait until the ongoing OnTo* request handling is finished
            }
            activeProcessWalks++;
        }catch(InterruptedException e){
            System.out.println("Handling Walk request interrupted");
            Thread.currentThread().interrupt();
        }finally{
            lock.unlock();
        }

        try {
            //Get all the locations and lengths from the request
            List<Location> locations = walk.getLocationsList();
            List<Integer> lengths = walk.getLengthsList();

            for (int i = 0; i < locations.size() - 1; i++) {
                Location from = locations.get(i);
                Location to = locations.get(i + 1);
                int length = lengths.get(i);
                graph.addEdge(from, to, length);
            }
        }
        catch(Exception e){
            System.out.println("Error during Walk request processing: " + e.getMessage());
        }
        finally {
            lock.lock();
            try {
                activeProcessWalks--;
                if (activeProcessWalks == 0) {
                    condition.signalAll();
                }
            }
            catch(Exception e){
                System.out.println("Handling Walk request interrupted during unlocking: " + e.getMessage());
            }
            finally {
                lock.unlock();
            }
        }
    }


    /**
     * --- Handle OneToOne requests ---
     * Multiple OneTo* requests can be handled simultaneously, but they have to wait until all the
     * ongoing Walk requests are finished to guarantee a synchronized state
     * @param request - the received OneToOne request
     * @return the length of the calculated shortest path, -1 upon error
     */
    private static long processOneToOne(OneToOne request) {
        lock.lock();
        try {
            while (activeProcessWalks > 0) {
                condition.await(); //Wait until the ongoing Walk request handlers are finished
            }
            oneToOperationInProgress = true; //Block the handling of new Walk requests
        } catch (InterruptedException e) {
            System.out.println("Handling OneToOne request interrupted");
            Thread.currentThread().interrupt();
        } finally {
            lock.unlock();
        }

        try {
            Location src = request.getOrigin();
            Location dest = request.getDestination();

            Location physicalSrc = graph.getCorrespondingLocation(src);
            Location physicalDest = graph.getCorrespondingLocation(dest);
            if (physicalSrc == null || physicalDest == null) {
                throw new IllegalArgumentException("Corresponding location not found in the graph");
            }
            return graph.computeShortestPath(physicalSrc, physicalDest);
        }
        catch(Exception e){
            System.out.println("Error during OneToOne request processing: " + e.getMessage());
        }

        finally {
            lock.lock();
            try {
                oneToOperationInProgress = false;
                condition.signalAll();
            }
            catch(Exception e){
                System.out.println("Handling OneToOne request interrupted during unlocking: " + e.getMessage());
            }
            finally {
                lock.unlock();
            }
        }

        return -1;
    }


    /**
     * --- Handle OneToAll requests ---
     * Multiple OneTo* requests can be handled simultaneously, but they have to wait until all the
     * ongoing Walk requests are finished to guarantee a synchronized state
     * @param request - the received OneToAll request
     * @return the sum of path lengths to every location from a specified origin, -1 upon error
     */
    private static long processOneToAll(OneToAll request) {

        lock.lock();
        try {
            while (activeProcessWalks > 0) {
                condition.await(); //Wait until the ongoing Walk request handlers are finished
            }
            oneToOperationInProgress = true; //Block the handling of new Walk requests
        } catch (InterruptedException e) {
            System.out.println("Handling OneToAll request interrupted");
        } finally {
            lock.unlock();
        }

        try {
            Location src = request.getOrigin();
            Location physicalSrc = graph.getCorrespondingLocation(src);
            if (physicalSrc == null) {
                throw new IllegalArgumentException("Corresponding location not found in the graph");
            }
            return graph.computeTotalLength(physicalSrc);
        }
        catch(Exception e){
            System.out.println("Error during OneToAll request processing: " + e.getMessage());
        }
        finally {
            lock.lock();
            try {
                oneToOperationInProgress = false;
                condition.signalAll();
            }
            catch(Exception e){
                System.out.println("Handling OneToAll request interrupted during unlocking: " + e.getMessage());
            }
            finally {
                lock.unlock();
            }
        }

        return -1;
    }
}
