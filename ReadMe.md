# Protobuf based efficient server application

## Purpose of the application
This application calculates the shortest drivable distances between city locations using vehicle-generated route data. It aims to develop a robust server capable of handling multiple client requests efficiently. [^1]
[^1]: This project was developed for the [Effective Software](https://esw.pages.fel.cvut.cz/) course at ČVUT.

## Requirements towards the server
- Handle TCP transmissions consisting of a 4-byte message length and a serialized protobuf message
- Support around 100 simultaneous client connections without significant performance degradation
- The server must perform efficiently under the given constraints. This ensures that it meets the expected response times and reliability for all clients
- Respond to client requests with the shortest path lengths or total lengths for given locations using a directed graph built from client-provided Walk requests
- Ensure accurate location mapping, handle varying edge lengths, and follow specified rules for including Walk data in path calculations
- Reset its state upon receiving a Reset request to facilitate multiple test runs

Refined requirements:
- The server must respond to each client `Request` message with a `Response` message. The `Status` field in the response is required. Presence of other fields depend on the type of `Request`
- The server constructs a directed graph data structure based on `Walk` requests from clients. In this graph, nodes represent locations, and edges indicate the path lengths between these locations. There is a maximum of one edge between any pair of nodes. The length of an edge does not necessarily reflect the Euclidean distance between the edge's endpoints. Since the graph is directed, an edge from location A to B does not imply the existence or characteristics of an edge from B to A. Additionally, each `Walk` request contains unique edges, with no edge appearing more than once
- The `x` and `y` coordinates of the `Location` are given in millimeters
- Due to measurement inaccuracies, the same physical location might be represented by different locations in the requests. Assume that any two locations with a Euclidean distance of 50 cm or less represent the same physical location. The mapping between the sent locations and physical locations is unambiguous, meaning each `Location` will have at most one physical location within a 50 cm distance
- Multiple `Walk` requests might contain the same edge, but the lengths of that edge may differ by up to 1 meter
- The server responds to `OneToOne` requests by providing the shortest path length between the two specified locations (in the `shortest_path_length` field). This shortest path length is calculated as the sum of the average edge lengths. The average edge length is determined from all `Walk` requests that included that edge, using integer division
- The server responds to `OneToAll` requests by providing the sum of the lengths of the shortest paths between the origin node and all other nodes. Each path length is calculated in the same manner as for `OneToOne` requests. The resulting total is sent in the `total_length` field

## Implementation:
The server's implementation was created in Java and C#. The necessary protobuf source files can be generated from the protobuf (.proto) using protoc compiler.  
The Java code uses a predefined threadpool to allocate the handling of incoming requests to separate threads. Reentrant locks are used to manage the synchronization of request handlers. The OneTo* requests have to wait for all the ongoing `Walk` requests to finish processing in order to work on the correct graph structure for the calculations. The locations are stored in a ConcurrentHashMap to account for the simultaneous/concurrent threads. Calculating the Euclidean distance to every point in the graph, beyond a certain threshold, would be extremely time-consuming, so the server uses a hash grid instead to help locate the possible physical location of a received location. A Dijkstra algorithm based path finder is implemented to calculate the shortest paths between two points and the sum of path lenghts to every location from a specific point.  
A client was created in C# to test the server using the provided .pbf files and custom datasets. 

### Running on Linux
Compilation is done using [nix-shell](https://nix.dev/install-nix.html) in both cases.  
Java:
```
nix-shell
mvn compile
mvn exec:java -Dexec.mainClass="server.ProtobufTCPServer"
```

C#:
```
nix-shell
dotnet new console -n EfficientServer
dotnet build
dotnet run
```
