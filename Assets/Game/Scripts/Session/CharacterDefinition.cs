using UnityEngine;

/// <summary>
/// One pickable character. Looks only: everyone plays the same, so a character
/// is a name, a lobby portrait, and the animations the avatar wears.
///
/// Setup:
///   1. Make an Animator Override Controller from the player's base controller
///      (right-click it > Create > Animator Override Controller) and drag this
///      character's clips over the base ones.
///   2. Assets > Create > Game > Character Definition. Fill in the fields.
///   3. Add it to the GameCatalog's Characters list.
/// </summary>
[CreateAssetMenu(fileName = "Character_", menuName = "Game/Character Definition")]
public class CharacterDefinition : ScriptableObject
{
    public string DisplayName = "Character";

    [Tooltip("Shown on the lobby screen.")]
    public Sprite Portrait;

    [Tooltip("An Animator Override Controller built on the player's base controller, so the parameters " +
             "(Speed, etc.) stay the same and only the clips change.")]
    public RuntimeAnimatorController Animator;
}
