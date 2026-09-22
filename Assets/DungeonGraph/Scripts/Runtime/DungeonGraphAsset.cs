using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DungeonGraph
{
    /// <summary>
    /// The subset of generation parameters that are shared in code across every style but whose
    /// ideal value plausibly differs per style and per graph — Organic and Grid each keep their
    /// own values here, per <see cref="DungeonGraphAsset"/>, rather than sharing one global
    /// EditorPrefs-backed value across every graph in the project.
    /// </summary>
    [Serializable]
    public class GenerationStyleTuning
    {
        public float idealDistance = 20f;
        public float chaosFactor = 0f;
        public bool forceMode = false;
        public int maxRoomRegenerations = 3;
    }

    [CreateAssetMenu(menuName = "Dungeon Graph/New Graph")]
    public class DungeonGraphAsset : ScriptableObject
    {
        [SerializeReference]
        private List<DungeonGraphNode> m_nodes;
        [SerializeField]
        private List<DungeonGraphConnection> m_connections;

        [SerializeField]
        private GenerationStyleTuning m_organicTuning = new GenerationStyleTuning();
        // Backing field kept named after the style's internal identifier (GenerationStyle.FloodFill /
        // FloodFillGeneration) even though the Dungeon Tools panel displays this style as "Grid".
        [SerializeField]
        private GenerationStyleTuning m_floodFillTuning = new GenerationStyleTuning();

        public List<DungeonGraphNode> Nodes => m_nodes;
        public List<DungeonGraphConnection> Connections => m_connections;

        /// <summary>
        /// The Ideal Distance / Chaos Factor / Force Mode / Max Room Regenerations values this
        /// graph remembers for <paramref name="style"/>. Never null, even for a graph asset
        /// serialized before this field existed.
        /// </summary>
        public GenerationStyleTuning GetTuning(GenerationStyle style)
        {
            switch (style)
            {
                case GenerationStyle.FloodFill:
                    return m_floodFillTuning ??= new GenerationStyleTuning();
                case GenerationStyle.Organic:
                default:
                    return m_organicTuning ??= new GenerationStyleTuning();
            }
        }

        private Dictionary<string, DungeonGraphNode> m_NodeDictionary;
        public DungeonGraphAsset()
        {
            m_nodes = new List<DungeonGraphNode>();
            m_connections = new List<DungeonGraphConnection>();
            //m_NodeDictionary = new Dictionary<string, DungeonGraphNode>();
        }

        public void Init()
        {
            m_NodeDictionary = new Dictionary<string, DungeonGraphNode>();
            foreach (DungeonGraphNode node in Nodes)
            {
                m_NodeDictionary.Add(node.id, node);
            }
        }

        // Returns start node
        // Multiple start nodes may result in unexpected behavior
        public DungeonGraphNode GetStartNode()
        {
            StartNode[] startNodes = Nodes.OfType<StartNode>().ToArray();
            if (startNodes.Length == 0)
            {
                Debug.LogError("There is no start node in this graph");
                return null;
            }
            return startNodes[0];
        }

        public DungeonGraphNode GetNode(string nextNodeId)
        {
            if (m_NodeDictionary.TryGetValue(nextNodeId, out DungeonGraphNode node))
            {
                return node;
            }
            return null;
        }

        public DungeonGraphNode GetNodeFromOutput(string outputNodeId, int index)
        {
            foreach (DungeonGraphConnection connection in m_connections)
            {
                if (connection.outputPort.nodeId == outputNodeId && connection.outputPort.portIndex == index)
                {
                    string nodeId = connection.inputPort.nodeId;
                    DungeonGraphNode inputNode = m_NodeDictionary[nodeId];
                    return inputNode;
                }
            }
            return null;
        }
    }
}

