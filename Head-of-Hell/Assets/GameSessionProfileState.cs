public static class GameSessionProfileState
{
    public static string ActiveP1ProfileId;
    public static string ActiveP1ProfileName;
    public static string ActiveP2ProfileId;
    public static string ActiveP2ProfileName;
    public static string ActiveP3ProfileId;
    public static string ActiveP3ProfileName;
    public static string ActiveP4ProfileId;
    public static string ActiveP4ProfileName;

    public static string PendingP1ProfileId;
    public static string PendingP1ProfileName;
    public static string PendingP2ProfileId;
    public static string PendingP2ProfileName;
    public static string PendingP3ProfileId;
    public static string PendingP3ProfileName;
    public static string PendingP4ProfileId;
    public static string PendingP4ProfileName;

    public static void BeginMatchFromPending()
    {
        ActiveP1ProfileId = PendingP1ProfileId;
        ActiveP1ProfileName = PendingP1ProfileName;
        ActiveP2ProfileId = PendingP2ProfileId;
        ActiveP2ProfileName = PendingP2ProfileName;
        ActiveP3ProfileId = PendingP3ProfileId;
        ActiveP3ProfileName = PendingP3ProfileName;
        ActiveP4ProfileId = PendingP4ProfileId;
        ActiveP4ProfileName = PendingP4ProfileName;
    }

    public static void CopyActiveToPending()
    {
        PendingP1ProfileId = ActiveP1ProfileId;
        PendingP1ProfileName = ActiveP1ProfileName;
        PendingP2ProfileId = ActiveP2ProfileId;
        PendingP2ProfileName = ActiveP2ProfileName;
        PendingP3ProfileId = ActiveP3ProfileId;
        PendingP3ProfileName = ActiveP3ProfileName;
        PendingP4ProfileId = ActiveP4ProfileId;
        PendingP4ProfileName = ActiveP4ProfileName;
    }
}
