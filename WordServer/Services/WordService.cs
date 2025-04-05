using Grpc.Core;
using Google.Protobuf.WellKnownTypes;
using System.Text.Json;
using WordServer;

namespace WordServer.Services
{
    //Exposes a gRPC service called DailyWord
    public class WordService : DailyWord.DailyWordBase
    {
        private static readonly string WordListPath = "wordle.json";
        private static readonly Dictionary<DateTime, string> _dailyCache = new();

        //GetWord
        public override Task<WordReply> GetWord(Empty request, ServerCallContext context)
        {
            string todayWord;
            var today = DateTime.Today;

            if (_dailyCache.ContainsKey(today))
            {
                todayWord = _dailyCache[today];
            }
            else
            {
                string[] words = JsonSerializer.Deserialize<string[]>(File.ReadAllText(WordListPath))!;
                var rand = new Random(today.Year * 10000 + today.Month * 100 + today.Day);
                todayWord = words[rand.Next(words.Length)];
                _dailyCache[today] = todayWord;
            }

            return Task.FromResult(new WordReply { Word = todayWord });
        }

        //ValidateWord
        public override Task<ValidationReply> ValidateWord(WordRequest request, ServerCallContext context)
        {
            string[] words = JsonSerializer.Deserialize<string[]>(File.ReadAllText(WordListPath))!;
            bool isValid = words.Contains(request.Word.ToLower());

            return Task.FromResult(new ValidationReply { IsValid = isValid });
        }
    }
}
