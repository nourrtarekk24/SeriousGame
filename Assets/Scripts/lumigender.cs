using UnityEngine;

/// <summary>
/// Attach this component to any scene that has both
/// Lumi characters. Assign lumiMale and lumiFemale
/// in the Inspector. Call ApplyGender() from Start()
/// or use the static helper directly.
/// </summary>
public class LumiGenderHelper : MonoBehaviour
{
    [Header("Lumi Characters")]
    public GameObject lumiMale;
    public GameObject lumiFemale;

    void Start()
    {
        Apply(lumiMale, lumiFemale);
    }

    /// <summary>
    /// Static utility — call from any manager's Start()
    /// instead of using this component if preferred.
    /// </summary>
    public static void Apply(
        GameObject male, GameObject female)
    {
        if (GameManager.Instance == null) return;

        bool isBoy = GameManager.Instance.lumiGender == 0;

        if (male != null) male.SetActive(isBoy);
        if (female != null) female.SetActive(!isBoy);
    }
}