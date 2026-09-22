using UnityEngine;

namespace DungeonGraph
{
    [NodeInfo("End", "Rooms/End", hasFlowInput: true, hasFlowOutput: false)]
    public class EndNode : DungeonGraphNode
    {
        public override string OnProcess(DungeonGraphAsset currentGraph)
        {
            Debug.Log("END NODE");
            return base.OnProcess(currentGraph);
        }
    }
}
