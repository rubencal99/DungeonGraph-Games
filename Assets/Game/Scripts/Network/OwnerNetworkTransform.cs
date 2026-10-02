using Unity.Netcode.Components;

/// <summary>
/// A NetworkTransform where the owner moves the object and everyone else
/// follows, instead of the host deciding. Used on players, so your own movement
/// never waits on a round trip to the host.
///
/// Setup: use in place of NetworkTransform on the Player prefab.
/// </summary>
public class OwnerNetworkTransform : NetworkTransform
{
    protected override bool OnIsServerAuthoritative() => false;
}
