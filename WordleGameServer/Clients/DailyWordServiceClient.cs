// DailyWordServiceClient.cs
// E. Rivers 1015561 Sec C
// March 25, 2025
// This module represents the client code for the WordServer's DailyWord Service.
// The class is static so that it will be able to maintain the same connection to the 
// service as long as this server is running. The DailyWordle's Play rpc uses the
// ValidateWord and GetWord methods below to interact with the WordServer.

using Grpc.Net.Client;
using WordServer;
using Grpc.Core;

namespace WordleGameServer.Clients
{
    public static class DailyWordServiceClient
    {
        /// <summary>
        /// A reference to a proxy object for the WordService 
        /// </summary>
        public static DailyWord.DailyWordClient? _wordServer = null;

        /// <summary>
        /// Connects to word server and gets the daily word
        /// </summary>
        public static string GetWord()
        {
            ConnectToService();
            WordReply? wordOfTheDay = _wordServer?.GetWord(new Empty());
            return wordOfTheDay?.Word ?? "";
        }

        /// <summary>
        /// Connects to WordServer and validates the guessed word.
        /// </summary>
        public static bool ValidateWord(string word)
        {
            ConnectToService();
            ValidationReply? reply = _wordServer?.ValidateWord(new WordRequest { Word = word });
            return reply?.IsValid ?? false;
        }

    
        /// <summary>
        /// Attempts to connect to the WordServer if the proxy object reference is null
        /// </summary>
        private static void ConnectToService()
        {
            if (_wordServer is null)
            {
                var channel = GrpcChannel.ForAddress("https://localhost:7128"); // THIS HAS TO CHANGE BASED ON WHERE IST RUNNING
                _wordServer = new DailyWord.DailyWordClient(channel);
            }
        }
    }
}
