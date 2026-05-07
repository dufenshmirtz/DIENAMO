using System.IO;
using UnityEngine;

public class ProfileManager : MonoBehaviour
{
    public static ProfileManager I { get; private set; }

    private const int NONE = -1;
    private const int GUEST = -2;

    [Header("Runtime")]
    [SerializeField] private ProfilesDatabase db = new ProfilesDatabase();
    [SerializeField] private int selectedIndexP1 = NONE;
    [SerializeField] private int selectedIndexP2 = NONE;
    [SerializeField] private int selectedIndexP3 = NONE;
    [SerializeField] private int selectedIndexP4 = NONE;

    [SerializeField] private ProfileData guestProfile;


    private string SavePath => Path.Combine(Application.persistentDataPath, "profiles.json");

    private void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);

        Load();
    }

    public (string id, string name) GetTelemetryIdentity(int playerNum)
    {
        EnsureGuest();

        int idx = GetSelectedIndex(playerNum);

        if (idx == GUEST)
            return ("GUEST", "Guest");

        if (idx >= 0)
        {
            var p = db.GetAt(idx);
            if (p == null) return ("GUEST", "Guest");

            return (p.id, p.profileName);
        }

        return ("GUEST", "Guest");
    }

    private void EnsureGuest()
    {
        if (guestProfile == null || guestProfile.id != "GUEST")
            guestProfile = ProfileData.CreateGuest();
    }

    public bool IsGuestSelected(int playerNum)
    {
        return GetSelectedIndex(playerNum) == GUEST;
    }

    // --- Public API (θα το καλέσει το UI στο επόμενο βήμα) ---

    public ProfilesDatabase GetDatabase() => db;

    public int GetSelectedIndex(int playerNum)
    {
        switch (playerNum)
        {
            case 1: return selectedIndexP1;
            case 2: return selectedIndexP2;
            case 3: return selectedIndexP3;
            case 4: return selectedIndexP4;
            default: return NONE;
        }
    }


    public ProfileData GetSelectedProfile(int playerNum)
    {
        EnsureGuest();
        int idx = GetSelectedIndex(playerNum);

        if (idx == GUEST)
            return guestProfile;

        if (idx >= 0)
            return db.GetAt(idx);

        return null;
    }

    public void SelectProfile(int playerNum, int index)
    {
        if (!db.HasAt(index)) return;

        switch (playerNum)
        {
            case 1:
                selectedIndexP1 = index;
                break;
            case 2:
                selectedIndexP2 = index;
                break;
            case 3:
                selectedIndexP3 = index;
                break;
            case 4:
                selectedIndexP4 = index;
                break;
            default:
                return;
        }

        Save();
    }

    public void SelectGuest(int playerNum)
    {
        switch (playerNum)
        {
            case 1:
                selectedIndexP1 = GUEST;
                break;
            case 2:
                selectedIndexP2 = GUEST;
                break;
            case 3:
                selectedIndexP3 = GUEST;
                break;
            case 4:
                selectedIndexP4 = GUEST;
                break;
            default:
                return;
        }

        EnsureGuest();
        Save();

    }

    public ProfileData CreateOrRenameProfileAt(int index, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName)) return null;

        var existing = db.GetAt(index);
        if (existing == null)
        {
            var p = new ProfileData(newName.Trim());
            db.SetAt(index, p);
        }
        else
        {
            existing.profileName = newName.Trim();
            db.SetAt(index, existing);
        }

        Save();
        return db.GetAt(index);
    }

    public void DeleteProfileAt(int index)
    {
        db.DeleteAt(index);

        if (selectedIndexP1 == index) selectedIndexP1 = -1;
        if (selectedIndexP2 == index) selectedIndexP2 = -1;
        if (selectedIndexP3 == index) selectedIndexP3 = -1;
        if (selectedIndexP4 == index) selectedIndexP4 = -1;
        Save();
    }

    // --- Persistence ---

    public void Save()
    {
        var json = JsonUtility.ToJson(db, prettyPrint: true);
        File.WriteAllText(SavePath, json);
    }

    public void Load()
    {
        if (!File.Exists(SavePath))
        {
            db = new ProfilesDatabase();
            return;
        }

        var json = File.ReadAllText(SavePath);
        db = JsonUtility.FromJson<ProfilesDatabase>(json) ?? new ProfilesDatabase();
    }
}
