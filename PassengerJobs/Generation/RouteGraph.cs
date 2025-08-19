using DV;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PassengerJobs.Generation
{
    public class RouteGraph
    {
        public RouteType RouteType;
        public int MinLength = 1;
        public int MaxLength = 5;

        private readonly Dictionary<string, Node> _nodes = new();
        private readonly Dictionary<string, IList<string[]>> _routeCache = new();

        public RouteGraph(RouteType routeType)
        {
            RouteType = routeType;
        }

        public void Reset()
        {
            _nodes.Clear();
            _routeCache.Clear();
        }

        public void CreateNode(string id, bool isTerminus)
        {
            _nodes.Add(id, new Node(id, isTerminus));
        }
        
        public void CreateLink(string source, string target, LinkSide exitSide)
        {
            if (_nodes.TryGetValue(source, out Node from) && _nodes.TryGetValue(target, out Node to))
            {
                from.AddLink(to, exitSide);
            }
            else
            {
                PJMain.Warning($"Could not find a station for route {source} => {target}");
            }
        }

        public IEnumerable<string> GetRoute(string startId)
        {
            var results = GetAllRoutes(startId);
            
            return results.GetRandomElement();
        }

        public IList<string[]> GetAllRoutes(string startId)
        {
            if (_routeCache.TryGetValue(startId, out var cached))
            {
                return cached;
            }

            var startNode = _nodes[startId];
            var routeStack = new Stack<Node>();
            var results = new List<string[]>();

            Walk(startNode, MinLength, MaxLength, LinkSide.Any, routeStack, results);

            _routeCache.Add(startId, results);
            return results;
        }

        private void Walk(Node current, int minLength, int maxLength, LinkSide entrySide, Stack<Node> route, List<string[]> results)
        {
            if (route.Count > (maxLength + 1))
            {
                return;
            }

            route.Push(current);

            if ((route.Count > minLength) && current.IsTerminus)
            {
                if (route.Any(n => !n.IsTerminus))
                {
                    // stack contains route in reverse order
                    var withoutStart = route.Take(route.Count - 1).Select(n => n.Id).ToArray();
                    Array.Reverse(withoutStart);

                    results.Add(withoutStart);
                }
                route.Pop();
                return;
            }

            IEnumerable<Node> targets;
            if ((entrySide == LinkSide.Any) || current.IsSingleEntry)
            {
                targets = current.TargetsA.Concat(current.TargetsB);
            }
            else
            {
                targets = (entrySide == LinkSide.B) ? current.TargetsA : current.TargetsB;
            }

            foreach (var next in targets)
            {
                if (route.Contains(next)) continue;

                LinkSide targetEntrySide = next.IsSingleEntry ? LinkSide.Any : next.GetEntrySide(current);
                 
                Walk(next, minLength, maxLength, targetEntrySide, route, results);
            }

            route.Pop();
        }

        public void WriteDebugFile()
        {
            string path = Path.Combine(PJMain.ModEntry.Path, "debug_routes.json");

            var routeList = _nodes.Values.Where(n => n.IsTerminus).ToDictionary(n => n.Id, n => GetAllRoutes(n.Id));

            string serialized = JsonConvert.SerializeObject(routeList);
            File.WriteAllText(path, serialized);
        }

        public enum LinkSide
        {
            Any,
            A,
            B
        }

        private static LinkSide GetOppositeSide(LinkSide source)
        {
            return source switch
            {
                LinkSide.A => LinkSide.B,
                LinkSide.B => LinkSide.A,
                _ => LinkSide.Any,
            };
        }

        private class Node
        {
            public readonly string Id;
            public readonly bool IsTerminus;

            public readonly List<Node> TargetsA = new();
            public readonly List<Node> TargetsB = new();

            public bool IsSingleEntry => TargetsB.Count == 0;

            public Node(string id, bool isTerminus)
            {
                Id = id;
                IsTerminus = isTerminus;
            }

            public void AddLink(Node target, LinkSide side)
            {
                if (side == LinkSide.B)
                {
                    TargetsB.Add(target);
                }
                else
                {
                    TargetsA.Add(target);
                }
            }

            public LinkSide GetEntrySide(Node source)
            {
                if (!IsSingleEntry)
                {
                    if (TargetsA.Contains(source)) return LinkSide.A;
                    if (TargetsB.Contains(source)) return LinkSide.B;
                }
                return LinkSide.Any;
            }
        }
    }
}
