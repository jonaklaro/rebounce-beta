using System.Collections.Generic;

public static class GameData
{
    public static List<JoinedPlayerData> JoinedPlayers = new();
    public static string selectedLevel;
    public static Dictionary<int, PlayerMetrics> playerMetrics = new();
    public static MatchMetrics currentMatch = null;

}