// GameStats.cs  
// E. Rivers 1015561 Sec C  
// March 25, 2025  
// Internal model needed to facilitate read/write operations to gamestats.json.  
// This class stores cumulative statistics for all players who attempt the current daily word.  
// These values are updated after each completed game session and are used to compute the  
// Statistics response message sent to clients upon request.  

namespace WordleGameServer.Services
{
    public class GameStats
    {
        public int TotalDailyPlayers { get; set; } = 0;
        public int NumberOfWinners { get; set; } = 0;
        public int TotalGuessesByWinners { get; set; } = 0;
        public Dictionary<int, int> GuessDistribution { get; set; } = new();
        public DateTime Date { get; set; } = DateTime.Today;
    }
}
