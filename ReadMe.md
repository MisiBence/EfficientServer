Effective Software (BE4M36ESW) - Efficient Servers assignment
Name: Kis Mihály Bence (username: kismihal)
Language: Java

Purpose of the application:
This application calculates the shortest drivable distances between city locations using vehicle-generated route data. It aims to develop a robust server capable of handling multiple client requests efficiently.
Requirements towards the server:
-Handle TCP transmissions consisting of a 4-byte message length and a serialized protobuf message
-Support around 100 simultaneous client connections without significant performance degradation
-The server must perform efficiently under the given constraints. This ensures that it meets the expected response times and reliability for all clients
-Respond to client requests with the shortest path lengths or total lengths for given locations using a directed graph built from client-provided Walk requests
-Ensure accurate location mapping, handle varying edge lengths, and follow specified rules for including Walk data in path calculations
-Reset its state upon receiving a Reset request to facilitate multiple test runs


Implementation:
My first intention was to develop the application in C#. I also created a test client in C# to test the server with the provided .pbf files and custom datasets. Every test ran correctly on my local machine and met the defined performance criteria, but when I uploaded it to ritchie and tried with the automatic evaluator, the full test run for more than 60 seconds almost every time (despite running the quick test correctly around 0.05 on average). So I had to give up my idea after 1 week of trying and switched to developing the server in Java.

Firstly I manually generated the .java files from the protobuf schema (.proto) using protoc compiler. My Java code uses a predefined threadpool to allocate the handling of incoming requests to separate threads. I used reentrant locks to manage the synchronization of request handlers. The OneTo* requests have to wait for all the ongoing Walk requests to finish processing in order to work on the correct graph structure for the calculations. The locations are stored in a ConcurrentHashMap to account for the simultaneous/concurrent threads. Calculating the Euclidean distance to every point in the graph (above a certain number of points) would take forever, so the server uses a hash grid instead to help locate the possible physical location of a received location. I implemented a Dijkstra algorithm based path finder to calculate the shortest paths between two points and the sum of path lenghts to every location from a specific point.

Compilation is done using nix-shell:

nix-shell
mvn compile
mvn exec:java -Dexec.mainClass="server.ProtobufTCPServer"