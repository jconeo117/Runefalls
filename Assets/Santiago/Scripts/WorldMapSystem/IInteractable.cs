/// <summary>
/// Contract for any world object that can be hovered and clicked by the player.
/// Implement this on components like DungeonInteractable, TownInteractable, etc.
/// </summary>
public interface IInteractable
{
    /// <summary>Called when the player's raycast first enters this object.</summary>
    void OnHoverEnter();

    /// <summary>Called every frame the player's raycast stays on this object.</summary>
    void OnHoverStay();

    /// <summary>Called when the player's raycast leaves this object.</summary>
    void OnHoverExit();

    /// <summary>Called when the player clicks while hovering this object and is within range.</summary>
    void OnInteract();

    /// <summary>True if the player is currently close enough to interact.</summary>
    bool IsInRange { get; }
}
