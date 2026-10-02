using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Only the host thinks and walks for enemies. On everyone else's machine the
/// brain and legs are switched off, and the enemy just shows where the host
/// says it is (NetworkTransform), how it's animated (NetworkAnimator), and where
/// it's looking (AimNetwork).
///
/// Setup: add to the root of every enemy prefab, next to the NetworkObject.
/// </summary>
[RequireComponent(typeof(EnemyBrain), typeof(EnemyMotor))]
public class EnemyNetwork : NetworkBehaviour
{
    public override void OnNetworkSpawn()
    {
        GetComponent<EnemyBrain>().enabled = IsServer;
        GetComponent<EnemyMotor>().enabled = IsServer;
    }
}
