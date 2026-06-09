using UnityEngine;

/// <summary>
/// BackgroundController — Attach to any GameObject in every scene EXCEPT HubScene.
///
/// Either assign the background manually OR tag it "SceneBackground" and
/// this script will find it automatically.
///
/// When backgrounds are OFF: hides the background and sets camera color to #F5F3EE.
/// When backgrounds are ON: shows normally.
/// </summary>
public class BackgroundController : MonoBehaviour
{
    [Tooltip("Optional: assign your background GameObject directly. " +
             "If left empty, the script finds it by tag 'SceneBackground'.")]
    public GameObject background;

    private static readonly Color kOffColor = new Color(245f / 255f, 243f / 255f, 238f / 255f, 1f);

    void Start()
    {
        // Auto-find background by tag if not assigned in Inspector
        if (background == null)
        {
            var found = GameObject.FindWithTag("SceneBackground");
            if (found != null) background = found;
        }

        Apply();
    }

    public void Apply()
    {
        bool show = GameManager.IsBackgroundEnabled();

        if (background != null)
            background.SetActive(show);

        Camera cam = Camera.main;
        if (cam != null)
            cam.backgroundColor = show ? Color.black : kOffColor;
    }
}