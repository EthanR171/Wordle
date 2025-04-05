// April 3, 2025
// WordleGameClient - Console application for Wordle game

using System;
using System.Linq;
using System.Threading.Tasks;
using Grpc.Net.Client;
using Grpc.Core;
using WordleGameServer.Protos;

namespace WordleGameClient
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

            try
            {
                using var channel = GrpcChannel.ForAddress("https://localhost:7043");

                var callInvoker = channel.CreateCallInvoker();
                var method = new Method<GuessRequest, GuessResponse>(
                    MethodType.DuplexStreaming,
                    "wordlegameserver.DailyWordle",
                    "Play",
                    Marshallers.Create(
                        msg => Google.Protobuf.MessageExtensions.ToByteArray(msg),
                        GuessRequest.Parser.ParseFrom),
                    Marshallers.Create(
                        msg => Google.Protobuf.MessageExtensions.ToByteArray(msg),
                        GuessResponse.Parser.ParseFrom)
                );

                DisplayWelcomeMessage();

                // Start the game
                var stream = callInvoker.AsyncDuplexStreamingCall(method, null, new CallOptions());
                int guessCount = 0;
                bool gameWon = false;
                bool gameOver = false;

                // Game loop
                while (!gameOver && guessCount < 6)
                {
                    // Get user input
                    Console.Write($"({guessCount + 1}): ");
                    string guess = Console.ReadLine()?.Trim().ToLower() ?? "";

                    if (guess.Length != 5)
                    {
                        Console.WriteLine("Please enter a 5-letter word.");
                        continue;
                    }

                    // Send guess to server
                    await stream.RequestStream.WriteAsync(new GuessRequest { Word = guess });

                    // Get response
                    if (await stream.ResponseStream.MoveNext())
                    {
                        var response = stream.ResponseStream.Current;

                        if (response.Results.Count > 0) // Checking if the word is valid
                        {
                            guessCount++;

                            // Display result
                            string result = "";
                            foreach (var letterResult in response.Results)
                            {
                                switch (letterResult.Status)
                                {
                                    case LetterStatus.CorrectPos:
                                        result += "*";
                                        break;
                                    case LetterStatus.WrongPos:
                                        result += "?";
                                        break;
                                    case LetterStatus.NotInWord:
                                        result += "x";
                                        break;
                                    default:
                                        result += " ";
                                        break;
                                }
                            }
                            Console.WriteLine(result);
                            Console.WriteLine();

                            // Display letter lists
                            Console.WriteLine($"Included: {string.Join(",", response.IncludedLetters)}");
                            Console.WriteLine($"Available: {string.Join(",", response.UnusedLetters)}");
                            Console.WriteLine($"Excluded: {string.Join(",", response.ExcludedLetters)}");
                            Console.WriteLine();

                            // Check game state
                            gameWon = response.IsCorrect;
                            gameOver = response.IsGameOver;

                            if (gameOver)
                            {
                                if (gameWon)
                                {
                                    Console.WriteLine("You win!");
                                }
                                else
                                {
                                    Console.WriteLine("Game over!");
                                }
                            }
                        }
                        else
                        {
                            Console.WriteLine("Invalid word. Try again.");
                        }
                    }
                    else
                    {
                        Console.WriteLine("No response from server. Game ended unexpectedly.");
                        break;
                    }
                }

                await stream.RequestStream.CompleteAsync();

                // Get and display statistics
                try
                {
                    var statsMethod = new Method<Empty, Statistics>(
                        MethodType.Unary,
                        "wordlegameserver.DailyWordle",
                        "GetStats",
                        Marshallers.Create(
                            msg => Google.Protobuf.MessageExtensions.ToByteArray(msg),
                            Empty.Parser.ParseFrom),
                        Marshallers.Create(
                            msg => Google.Protobuf.MessageExtensions.ToByteArray(msg),
                            Statistics.Parser.ParseFrom)
                    );

                    var statsCall = callInvoker.AsyncUnaryCall(statsMethod, null, new CallOptions(), new Empty());
                    var stats = await statsCall.ResponseAsync;

                    Console.WriteLine();
                    Console.WriteLine("Statistics");
                    Console.WriteLine("----------");
                    Console.WriteLine();
                    Console.WriteLine($"Players: {stats.NumPlayers}");
                    Console.WriteLine($"Winners: {stats.WinnersPercentage:F1}%");
                    Console.WriteLine($"Average Guesses: {stats.AverageGuesses:F1}");

                    if (stats.WinnerGuessDistribution.Count > 0)
                    {
                        Console.WriteLine("\nGuess Distribution:");
                        foreach (var entry in stats.WinnerGuessDistribution.OrderBy(kvp => kvp.Key))
                        {
                            Console.WriteLine($"{entry.Key} guesses: {entry.Value} player(s)");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error retrieving statistics: {ex.Message}");

                    if (ex.InnerException != null)
                    {
                        Console.WriteLine($"Inner error: {ex.InnerException.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");

                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner error: {ex.InnerException.Message}");
                }

                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }

            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }

        private static void DisplayWelcomeMessage()
        {
            Console.WriteLine("+-------------------+");
            Console.WriteLine("| W O R D L E   D   |");
            Console.WriteLine("+-------------------+");
            Console.WriteLine();
            Console.WriteLine("You have 6 chances to guess a 5-letter word.");
            Console.WriteLine("Each guess must be a 'playable' 5 letter word.");
            Console.WriteLine("After a guess the game will display a series of");
            Console.WriteLine("characters to show you how good your guess was.");
            Console.WriteLine("x - means the letter above is not in the word.");
            Console.WriteLine("? - means the letter should be in another spot.");
            Console.WriteLine("* - means the letter is correct in this spot.");
            Console.WriteLine();
        }
    }
}