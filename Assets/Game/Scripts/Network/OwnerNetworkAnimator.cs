using Unity.Netcode.Components;

/// <summary>
/// A NetworkAnimator where the owner's animator is the one copied to everyone
/// else. Players drive their own Speed parameter, so their animation must come
/// from their own machine too.
///
/// Setup: use in place of NetworkAnimator on the Player's Character child.
/// </summary>
public class OwnerNetworkAnimator : NetworkAnimator
{
    protected override bool OnIsServerAuthoritative() => false;
}
