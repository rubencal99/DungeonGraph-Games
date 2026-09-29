using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

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

    /// <summary>
    /// Every other Dungeon Tools panel setting, saved on the graph asset so the runtime
    /// <c>DungeonGenerator</c> builds exactly what the panel builds — EditorPrefs are per-machine
    /// and do not exist in a player build. Per-style values live in <see cref="GenerationStyleTuning"/>.
    /// </summary>
    [Serializable]
    public class DungeonGenerationSettings
    {
        /// <summary>
        /// False until the Dungeon Tools panel first saves this graph. The panel uses it to seed
        /// graphs created before settings moved here from the legacy EditorPrefs values.
        /// </summary>
        public bool initialized = false;

        public GenerationStyle style = GenerationStyle.Organic;

        /// <summary>Floor folder name, which is also the floor's Addressables label (e.g. "Floor_1").</summary>
        public string floorName = "";

        [Header("Corridors")]
        public TileBase corridorTile;
        public int corridorWidth = 2;
        public CorridorType corridorType = CorridorType.Direct;
        public int maxCorridorRegenerations = 3;

        [Header("Organic")]
        public float areaPlacementFactor = 2.0f;
        public float repulsionFactor = 1.0f;
        public int simulationIterations = 100;
        public float stiffnessFactor = 1.0f;
        public RepulsionScalingMode repulsionScalingMode = RepulsionScalingMode.Default;
        public float bendFactor = 0.0f;
        public bool allowRoomOverlap = false;
        public bool realTimeSimulation = false;
        public float simulationSpeed = 10f;

        [Header("Grid")]
        public int floodFillMaxCorridorLength = 40;
        public int floodFillMaxBacktrackAttempts = 6;
        public int floodFillSeed = 0;
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

        [SerializeField]
        private DungeonGenerationSettings m_settings = new DungeonGenerationSettings();

        public List<DungeonGraphNode> Nodes => m_nodes;
        public List<DungeonGraphConnection> Connections => m_connections;

        /// <summary>
        /// The Dungeon Tools panel settings saved with this graph. Never null, even for a graph
        /// asset serialized before this field existed.
        /// </summary>
        public DungeonGenerationSettings Settings => m_settings ??= new DungeonGenerationSettings();

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

