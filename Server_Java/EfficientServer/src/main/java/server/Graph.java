package server;

import server.proto.Location;

import java.util.*;
import java.util.concurrent.ConcurrentHashMap;
import java.util.concurrent.CopyOnWriteArrayList;

public class Graph {
    private final Map<Location, Map<Location, List<Integer>>> adjacencyList = new ConcurrentHashMap<>(); //Store the graph edges
    private final Map<Integer, Map<Integer, Location>> hashGrid = new ConcurrentHashMap<>();
    private static final int GRID_SIZE = 250;
    private static final int DISTANCE_THRESHOLD_SQUARED = 500*500;

    /**
     * Helper function to calculate grid coordinates of a location
     * @param loc - the received location
     * @return the X and Y coordinates of the grid point that the location got allocated to
     */
    private int[] calculateGridCoordinates(Location loc) {
        int x = (int) Math.floor(loc.getX() / GRID_SIZE);
        int y = (int) Math.floor(loc.getY() / GRID_SIZE);
        return new int[]{x, y};
    }

    /**
     * Helper function to add a location to the hash grid
     * @param loc - the received location
     */
    private void addToHashGrid(Location loc) {
        int[] coordinates = calculateGridCoordinates(loc);
        hashGrid.computeIfAbsent(coordinates[0], k -> new ConcurrentHashMap<>())
                .put(coordinates[1], loc);
    }

    /**
     * Helper function to get nearby physical locations from the hash grid
     * A location is considered a neighbour if it is in the same or in one of the adjacent grid cells
     * @param receivedLocation - the location we want to find the neighbours of
     * @return a list containing all the neighbouring locations, empty list if there are none
     */
    private List<Location> getNearbyPhysicalLocations(Location receivedLocation) {
        List<Location> nearbyLocations = new ArrayList<>();
        int[] coordinates = calculateGridCoordinates(receivedLocation);

        //Check the same and all the adjacent grid cells
        for (int dx = -1; dx <= 1; dx++) {
            for (int dy = -1; dy <= 1; dy++) {
                int gridX = coordinates[0] + dx;
                int gridY = coordinates[1] + dy;
                if (hashGrid.containsKey(gridX) && hashGrid.get(gridX).containsKey(gridY)) {
                    nearbyLocations.add(hashGrid.get(gridX).get(gridY));
                }
            }
        }
        return nearbyLocations;
    }

    /**
     * Helper function to calculate the Euclidean distance between the two locations
     * Comparing the square values to save the sqrt() operation
     * sqrt(u) <= v is equivalent to u <= v^2
     *
     * @param loc1 - one of the locations
     * @param loc2 - the other location
     * @return the square of the distance between the two points
     */
    private double areLocationsWithinDistance(Location loc1, Location loc2) {
        return Math.pow(loc1.getX() - loc2.getX(), 2) + Math.pow(loc1.getY() - loc2.getY(), 2);
    }

    /**
     * Helper function to get a possible physical location that is already in the graph for the received location
     * @param receivedLocation - the location we want to find in the graph
     * @return the existing location object if found, null if not found
     */
    private Location getPossibleLocation(Location receivedLocation) {
        List<Location> nearbyLocations = getNearbyPhysicalLocations(receivedLocation);
        double distanceGlobal = DISTANCE_THRESHOLD_SQUARED;
        Location foundLocation = null;

        // Iterate over nearby physical locations to find the one within 50cm distance
        for (Location physicalLocation : nearbyLocations) {
            double distance = areLocationsWithinDistance(receivedLocation, physicalLocation);
            if (distance <= distanceGlobal) {
                distanceGlobal = distance;
                foundLocation = physicalLocation;
            }
        }
        // If no physical location within 50cm distance is found, return the sent location itself
        if(distanceGlobal < DISTANCE_THRESHOLD_SQUARED) {
            return foundLocation;
        }
        else {
            return null;
        }
    }

    /**
     * Helper function to get the physical location corresponding to the received location
     * @param receivedLocation - the new location that has to be checked if already exists in the graph
     * @return the already existing location object if found, the received location object if not
     */
    private Location getPhysicalLocation(Location receivedLocation) {
        Location physicalLocation = getPossibleLocation(receivedLocation);
        if (physicalLocation != null) {
            return physicalLocation;
        }
        else {
            return receivedLocation;
        }
    }

    /**
     * Public getter for the corresponding physical location of the received location
     * @param receivedLocation - the location we have to find in the graph
     * @return the matching location object that is present in the graph, null if the location is not found in the graph
     */
    public Location getCorrespondingLocation(Location receivedLocation) {
        Location foundLocation = getPossibleLocation(receivedLocation);
        if(foundLocation != null) {
            return foundLocation;
        }
        else {
            System.out.println("Error: Location not found");
            return null;
        }
    }

    /**
     * Add an edge between two locations
     * @param source - the source of the directed edge (one end)
     * @param destination - the destination of the directed edge (the other end)
     * @param weight - the length of the edge
     */
    public void addEdge(Location source, Location destination, int weight) {
        Location physSource = null;
        Location physDest = null;
        try {
            physSource = getPhysicalLocation(source);
            physDest = getPhysicalLocation(destination);
        } catch (Exception e) {
            System.out.println("Error during addEdge, get physical locations: " + e.getMessage());
            return;
        }
        final Location finalPhysDest = physDest;

        try {
            adjacencyList.compute(physSource, (sourceKey, destMap) -> {
                if (destMap == null) {
                    destMap = new ConcurrentHashMap<>();
                }
                destMap.compute(finalPhysDest, (destKey, weights) -> {
                    if (weights == null) {
                        weights = new CopyOnWriteArrayList<>();
                    }
                    weights.add(weight);
                    return weights;
                });
                return destMap;
            });
        } catch (Exception e) {
            System.out.println("Error during addEdge, update adjacencyList: " + e.getMessage());
            return;
        }

        try {
            //Update the hash grid with the new locations
            addToHashGrid(physSource);
            addToHashGrid(physDest);
        } catch (Exception e) {
            System.out.println("Error during addEdge, add hashgrid: " + e.getMessage());
        }
    }

    /**
     * Calculate the length of the shortest path between two locations (Dijkstra algorithm)
     * @param start - one of the locations
     * @param end - the other location
     * @return the length of the shortest path between the locations; -1 if the path is not found
     */
    public long computeShortestPath(Location start, Location end) {
        try{
            PriorityQueue<Node> queue = new PriorityQueue<>(Comparator.comparingLong(node -> node.distance));
            Map<Location, Long> distances = new HashMap<>();
            queue.add(new Node(start, 0));
            distances.put(start, 0L);

            while (!queue.isEmpty()) {
                Node current = queue.poll();
                if (current.location.equals(end)) {
                    return current.distance;
                }
                for (Map.Entry<Location, List<Integer>> neighborEntry : adjacencyList.getOrDefault(current.location, Collections.emptyMap()).entrySet()) {
                    Location neighbor = neighborEntry.getKey();
                    List<Integer> edgeWeights = neighborEntry.getValue();
                    long avgLength = (long) edgeWeights.stream().mapToInt(Integer::intValue).average().orElse(0.0);
                    long newDist = current.distance + avgLength;
                    if (newDist < distances.getOrDefault(neighbor, Long.MAX_VALUE)) {
                        distances.put(neighbor, newDist);
                        queue.add(new Node(neighbor, newDist));
                    }
                }
            }
        }
        catch (Exception e) {
            System.out.println("Error in computeShortestPath: " + e.getMessage());
        }

        System.out.println("Error: Path not found");
        return -1;
    }

    /**
     * Calculate the total length of paths from a starting location to every other location (Dijkstra algorithm)
     * @param start - the start of the pathfinding
     * @return the total length of the paths to every location from the start point; -1 on error
     */
    public long computeTotalLength(Location start) {
        try
        {
            PriorityQueue<Node> queue = new PriorityQueue<>(Comparator.comparingLong(node -> node.distance));
            Map<Location, Long> distances = new HashMap<>();
            queue.add(new Node(start, 0));
            distances.put(start, 0L);
            long totalLength = 0;

            Set<Location> visited = new HashSet<>(); // Track visited nodes

            while (!queue.isEmpty()) {
                Node current = queue.poll();
                Location currentNode = current.location;

                // Check if node has been visited before
                if (visited.contains(currentNode)) {
                    continue; // Skip processing if already visited
                }

                // Mark node as visited
                visited.add(currentNode);

                totalLength += current.distance;
                for (Map.Entry<Location, List<Integer>> neighborEntry : adjacencyList.getOrDefault(currentNode, Collections.emptyMap()).entrySet()) {
                    Location neighbor = neighborEntry.getKey();
                    List<Integer> edgeWeights = neighborEntry.getValue();
                    long avgLength = (long) edgeWeights.stream().mapToInt(Integer::intValue).average().orElse(0.0);
                    long newDist = current.distance + avgLength;
                    if (newDist < distances.getOrDefault(neighbor, Long.MAX_VALUE)) {
                        distances.put(neighbor, newDist);
                        queue.add(new Node(neighbor, newDist));
                    }
                }
            }
            return totalLength;
        }
        catch (Exception e) {
            System.out.println("Error in computeTotalLength: " + e.getMessage());
        }

        return -1;
    }

    /**
     * Reset the graph structure
     */
    public void reset() {
        adjacencyList.clear();
        hashGrid.clear();
    }

    private static class Node {
        Location location;
        long distance;

        Node(Location location, long distance) {
            this.location = location;
            this.distance = distance;
        }
    }
}
