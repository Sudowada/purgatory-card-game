/// <summary>
/// The table — a "Play" interaction. Zooms the camera into
/// GameStateManager's cardTableViewPoint and switches to CardGame mode.
/// All proximity detection, the discoverability icon, and crosshair
/// prompt logic live in WorldInteractable — this class only defines
/// what happens on interact.
/// </summary>
public class TableInteractable : WorldInteractable
{
    protected override void Awake()
    {
        base.Awake();
        interactionType = InteractionType.Play;
    }

    protected override void OnInteract()
    {
        GameStateManager.Instance?.GoToCardGame();
    }
}