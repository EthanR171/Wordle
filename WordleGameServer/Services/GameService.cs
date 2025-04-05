// GameService.cs
// E. Rivers 1015561 Sec C
// M. Seghal [     ] Sec C
// March 25, 2025
// This module implements the rpc's of the DailyWordle service contract.
using Grpc.Core;
using System.Runtime.InteropServices.Marshalling;
using System.Text.Json;
using WordleGameServer.Clients;
using WordleGameServer.Protos;

namespace WordleGameServer.Services
{
    public class GameService : DailyWordle.DailyWordleBase
    {
        /// <summary>
        /// Implementation of the Play rpc which uses a bidirectional service stream 
        /// to accept guess requests and to return responses. This is also an async method so that 
        /// the call is non-blocking for the client. It repeatedly performs three main tasks: 
        /// 1. reads a request from the request stream (from the client), 
        /// 2. prepares and writes a response to the response stream (to the client),
        /// 3. decides whether it's time to termiate the stream and thus the RPC call.
        /// </summary>
        /// <param name="requestStream">A reference to a ready-to-use request stream object to receive a series of 
        /// word guesses from the client</param>
        /// <param name="responseStream">A reference to a ready-to-use response stream object to pass responses 
        /// from the service to the client</param>
        /// <param name="context">Call context for the rpc (Metadata and advanced debug info)</param>
        /// <returns>A non-generic Task object which means it's essentially a void method</returns>
        public override async Task Play(IAsyncStreamReader<GuessRequest> requestStream, IServerStreamWriter<GuessResponse> responseStream, ServerCallContext context)
        {
            // SESSION VARIABLES
            string wordToGuess = DailyWordServiceClient.GetWord().ToLower();
            const uint GUESS_LIMIT = 6;
            const uint ARRAY_SIZE = 5; // for when we have to add results to letter results (words are 5 letters only)
            bool gameWon = false;
            uint turnsUsed = 0;

            HashSet<char> included = new HashSet<char>(); // contains correct AND misplaced letters (see game output example for clarification)
            HashSet<char> excluded = new HashSet<char>(); // contais letters that are not in the word at all
            HashSet<char> available = new HashSet<char>(); // contains letters not played yet
            for (char c = 'a'; c <= 'z'; c++)
                available.Add(c); // initialize the available letters set with all lowercase letters

            // GAME STATS
            string statsFile = Path.Combine(AppContext.BaseDirectory, "gamestats.json");

            GameStats stats = new();
            bool statsLoaded = false;

            if (!statsLoaded)
            {
                statsMutex.WaitOne();
                try
                {
                    if (File.Exists(statsFile))
                    {
                        string json = File.ReadAllText(statsFile);
                        stats = JsonSerializer.Deserialize<GameStats>(json) ?? new GameStats();

                        // Check if stats are from a previous day and reset if needed
                        if (stats.Date.Date != DateTime.Today)
                        {
                            Console.WriteLine("New day detected. Resetting statistics.");
                            stats = new GameStats(); // This will initialize with today's date

                            // update the file immediatlry
                            string updatedJson = JsonSerializer.Serialize(stats);
                            File.WriteAllText(statsFile, updatedJson);
                        }
                    }
                    else
                    {
                        stats = new GameStats();
                    }
                    statsLoaded = true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading game statistics: {ex.Message}");
                    stats = new GameStats();
                }
                finally
                {
                    statsMutex.ReleaseMutex();
                }
            }



            // GAMEPLAY LOOP
            while (!gameWon && await requestStream.MoveNext() && turnsUsed < GUESS_LIMIT)
            {
                List<LetterResult> letterResults = [];
                string wordPlayed = requestStream.Current.Word.ToLower();

                // If a guess does not represent a valid word, it will be rejected, and the guess won’t count as one of the six guesses.
                if (!DailyWordServiceClient.ValidateWord(wordPlayed))
                {
                    GuessResponse response = new()
                    {
                        IsCorrect = false,
                        IsGameOver = false
                    };

                    response.Results.AddRange([]);
                    response.UnusedLetters.AddRange(available.Select(c => c.ToString()));
                    response.IncludedLetters.AddRange(included.Select(c => c.ToString()));
                    response.ExcludedLetters.AddRange(excluded.Select(c => c.ToString()));

                    await responseStream.WriteAsync(response);
                    continue;
                }

                turnsUsed++;

                // check if the user is just goated and got it on the first try
                if (wordPlayed == wordToGuess)
                {
                    gameWon = true;
                    for (int i = 0; i < ARRAY_SIZE; ++i)
                    {
                        letterResults.Add(
                                new LetterResult
                                {
                                    Letter = wordPlayed[i].ToString(),
                                    Status = LetterStatus.CorrectPos
                                }
                            );
                    }
                }
                else
                {
                    // will be used in conjuction with countFrequency() to aid in determining
                    // if a letter was guessed more times than it appears in the target word.
                    Dictionary<char, int> matches = new();
                    for (char c = 'a'; c <= 'z'; c++)
                        matches[c] = 0;

                    CountFrequency(wordToGuess, matches); // count the frequency of letters in the target word

                    // first pass to determine correct positions
                    for (int i = 0; i < ARRAY_SIZE; ++i)
                    {
                        char letter = wordPlayed[i];
                        available.Remove(letter);

                        if (letter == wordToGuess[i])
                        {
                            matches[letter]--;
                            included.Add(letter);
                            letterResults.Add(new LetterResult
                            {
                                Letter = letter.ToString(),
                                Status = LetterStatus.CorrectPos
                            });
                        }
                        else
                        {
                            letterResults.Add(new LetterResult
                            {
                                Letter = letter.ToString(),
                                Status = LetterStatus.Unknown
                            });
                        }
                    }

                    // second pass to determine misplaced letters and excluded letters
                    for (int i = 0; i < ARRAY_SIZE; ++i)
                    {
                        // skip if already determined to be correct in the first pass
                        if (letterResults[i].Status != LetterStatus.Unknown) continue;

                        char letter = wordPlayed[i];

                        if (wordToGuess.Contains(letter) && matches[letter] > 0)
                        {
                            matches[letter]--;
                            included.Add(letter);
                            letterResults[i].Status = LetterStatus.WrongPos;
                        }
                        else
                        {
                            excluded.Add(letter);
                            letterResults[i].Status = LetterStatus.NotInWord;
                        }
                    }
                }

                // prepare the response to send back to the client
                GuessResponse responseToSend = new()
                {
                    IsCorrect = gameWon,
                    IsGameOver = gameWon || (turnsUsed >= GUESS_LIMIT)
                };

                responseToSend.Results.AddRange(letterResults);
                responseToSend.UnusedLetters.AddRange(available.Select(c => c.ToString()));
                responseToSend.IncludedLetters.AddRange(included.Select(c => c.ToString()));
                responseToSend.ExcludedLetters.AddRange(excluded.Select(c => c.ToString()));

                await responseStream.WriteAsync(responseToSend);
            }

            // UPDATE GAME STATISTICS
            if (statsLoaded)
            {
                stats.TotalDailyPlayers++;

                if (gameWon)
                {
                    stats.NumberOfWinners++;
                    stats.TotalGuessesByWinners += (int)turnsUsed;

                    if (stats.GuessDistribution.ContainsKey((int)turnsUsed))
                        stats.GuessDistribution[(int)turnsUsed]++;
                    else
                        stats.GuessDistribution[(int)turnsUsed] = 1;
                }

                UseResource(stats, statsFile); // update stats file in a thread-safe manner
            }

        }

        /// <summary>
        /// Helper method to count the frequency of letters in a given word. 
        /// This is used to ensure that we do not track letters more than they 
        /// actually appear in the target word. Wordle typically allows a letter 
        /// to be guessed only as many times as it appears in the target word.
        /// </summary>
        /// <param name="word"></param>
        /// <param name="matches"></param>
        private void CountFrequency(string word, Dictionary<char, int> matches)
        {
            for (int i = 0; i < word.Length; ++i)
            {
                char letter = word[i];
                if (matches.ContainsKey(letter))
                    matches[letter]++;
            }
        }

        private static readonly Mutex statsMutex = new(); // shared between threads
        /// <summary>
        /// Helper method to update the game statistics in a thread-safe manner.
        /// Prevents Deadlocks by using a Mutex to ensure that only one thread can access the file at a time.
        /// </summary>
        /// <param name="stats"></param>
        /// <param name="statsFile"></param>
        private static void UseResource(GameStats stats, string statsFile)
        {
            statsMutex.WaitOne();
            try
            {
                Console.WriteLine($"Attempting to save stats to: {Path.GetFullPath(statsFile)}");

                Directory.CreateDirectory(Path.GetDirectoryName(statsFile));

                string updatedJson = JsonSerializer.Serialize(stats);
                File.WriteAllText(statsFile, updatedJson);

                Console.WriteLine("Stats saved successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error writing to stats file: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
            finally
            {
                statsMutex.ReleaseMutex();
            }
        }

        /// <summary>
        /// Implementation of the GetStats rpc.
        /// This method returns the current game statistics for the daily word in a single response,
        /// including the number of players who have played, the percentage of those who guessed the word
        /// correctly, and the average number of guesses used by winners.
        /// </summary>
        /// <param name="empty">An Empty message object (no data is needed from the client)</param>
        /// <param name="context">Call context for the rpc (includes metadata, deadlines, etc.)</param>
        /// <returns>A Task containing a Statistics response message</returns>
        public override Task<Statistics> GetStats(Empty empty, ServerCallContext context)
        {
            string statsFile = Path.Combine(AppContext.BaseDirectory, "gamestats.json");

            Statistics response = new();

            try
            {
                statsMutex.WaitOne();

                if (File.Exists(statsFile))
                {
                    string json = File.ReadAllText(statsFile);
                    GameStats stats = JsonSerializer.Deserialize<GameStats>(json) ?? new GameStats();

                    // Reset if stats are from a previous day
                    if (stats.Date.Date != DateTime.Today)
                    {
                        Console.WriteLine("New day detected. Resetting statistics.");
                        stats = new GameStats();
  
                        string updatedJson = JsonSerializer.Serialize(stats);
                        File.WriteAllText(statsFile, updatedJson);
                    }

                    response.NumPlayers = stats.TotalDailyPlayers;

                    response.WinnersPercentage = stats.TotalDailyPlayers > 0
                        ? (double)stats.NumberOfWinners / stats.TotalDailyPlayers * 100
                        : 0;

                    response.AverageGuesses = stats.NumberOfWinners > 0
                        ? (double)stats.TotalGuessesByWinners / stats.NumberOfWinners
                        : 0;

                    foreach (var entry in stats.GuessDistribution)
                    {
                        response.WinnerGuessDistribution.Add(entry.Key, entry.Value);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading stats file: {ex.Message}");
            }
            finally
            {
                statsMutex.ReleaseMutex();
            }

            return Task.FromResult(response);
        }

    }
}
