using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace EfficientServer
{
    internal class Graph
    {
        private ConcurrentDictionary<(int, int), LocationNode> nodes = new ConcurrentDictionary<(int, int), LocationNode>();
        private const int LocationThresholdSquared = 500 * 500; // 50 cm in mm

        public void AddWalk(Walk walk)
        {
            for (int i = 0; i < walk.Locations.Count - 1; i++)
            {
                LocationNode start = GetOrCreateNode(walk.Locations[i]);
                LocationNode end = GetOrCreateNode(walk.Locations[i + 1]);
                uint length = (uint)walk.Lengths[i];

                var edge = start.Edges.Find(e => e.Destination == end);
                if (edge == null)
                {
                    edge = new Edge(end);
                    start.Edges.Add(edge);
                }
                edge.AverageWeight = length;
            }
        }

        private (int, int) SnapToGrid(Location loc)
        {
            int snapX = ((loc.X + 500 / 2) / 500) * 500;
            int snapY = ((loc.Y + 500 / 2) / 500) * 500;
            return (snapX, snapY);
        }        

        private LocationNode GetOrCreateNode(Location loc)
        {   
            var key = SnapToGrid(loc);
            return nodes.GetOrAdd(key, _ => new LocationNode
            {
                Location = new Location { X = key.Item1, Y = key.Item2 },
                Edges = new List<Edge>()
            });            
        }

        private LocationNode GetNode(Location loc)
        {
            var key = SnapToGrid(loc);
            return nodes.TryGetValue(key, out var node) ? node : null;
        }
        
        public ulong ComputeShortestPathDijkstraImproved(Location from, Location to)
        {
            var sourceNode = GetNode(from);
            var targetNode = GetNode(to);
            if (sourceNode == null || targetNode == null)
            {
                throw new ArgumentException("Source or target node not found in the graph.");
            }

            var distances = new Dictionary<LocationNode, uint>();
            var previous = new Dictionary<LocationNode, LocationNode>();
            var priorityQueue = new PriorityQueue<LocationNode, uint>();

            foreach (var node in nodes.Values)
            {
                distances[node] = uint.MaxValue;
                previous[node] = null;
                priorityQueue.Enqueue(node, int.MaxValue);
            }

            distances[sourceNode] = 0;
            priorityQueue.Enqueue(sourceNode, 0);

            while (priorityQueue.Count != 0)
            {
                priorityQueue.TryDequeue(out var current, out var currentDist);

                if (current == targetNode)
                {
                    uint totalPath = 0;
                    while (previous[current] != null)
                    {
                        var edge = previous[current].Edges.Find(e => e.Destination == current);
                        totalPath += edge.AverageWeight;
                        current = previous[current];
                    }
                    return totalPath;
                }

                if (currentDist == uint.MaxValue)
                {
                    break; // All remaining vertices are inaccessible
                }

                foreach (var edge in current.Edges)
                {
                    var neighbor = edge.Destination;
                    uint alt = currentDist + edge.AverageWeight;
                    if (alt < distances[neighbor])
                    {
                        distances[neighbor] = alt;
                        previous[neighbor] = current;
                        priorityQueue.Enqueue(neighbor, alt);
                    }
                }
            }

            return 0;
        }

        public ulong ComputePathsFromImproved(Location origin)
        {
            var sourceNode = GetNode(origin);
            var distances = new Dictionary<LocationNode, uint>();
            var priorityQueue = new PriorityQueue<LocationNode, uint>();

            // Initialize distances and enqueue all nodes with their initial priorities
            foreach (var node in nodes.Values)
            {
                if (node == sourceNode)
                {
                    distances[node] = 0;
                    priorityQueue.Enqueue(node, 0);
                }
                else
                {
                    distances[node] = int.MaxValue;
                    priorityQueue.Enqueue(node, int.MaxValue);
                }
            }

            while (priorityQueue.Count > 0)
            {
                priorityQueue.TryDequeue(out var currentNode, out var currentDistance);

                if (currentDistance == int.MaxValue)
                    break;

                foreach (var edge in currentNode.Edges)
                {
                    uint alt = currentDistance + edge.AverageWeight;
                    if (alt < distances[edge.Destination])
                    {
                        distances[edge.Destination] = alt;
                        priorityQueue.Enqueue(edge.Destination, alt);
                    }
                }
            }

            ulong totalLength = 0;
            foreach (var length in distances.Values)
            {
                if (length != int.MaxValue)
                {
                    totalLength += (ulong)length;
                }
            }

            return totalLength;
        }


        public void Reset()
        {
            nodes.Clear();
        }
    }

    public class LocationNode
    {
        public Location Location;
        public List<Edge> Edges;
    }

    public class Edge
    {
        public LocationNode Destination;
        public uint Weights;
        private byte weightCount;

        public uint AverageWeight
        {
            get { return Weights; }
            set
            {
                Interlocked.Add(ref Weights, value);
                if(weightCount < 2)
                {
                    weightCount++;
                }
                Interlocked.Exchange(ref Weights, Weights / weightCount);
            }
        }

        public Edge(LocationNode destination)
        {
            Destination = destination;
            weightCount = 0;
            Weights = 0;
        }
    }
}
