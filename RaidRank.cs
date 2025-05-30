using Newtonsoft.Json.Linq;
using NetworkProtocol;
using RestSharp;

namespace BARaidRank
{
    public class RaidRank
    {
        private static Timer _timer3AM;
        private static bool _isRestartingAt3AM = false;
        private static DateTime GetTaipeiStandardTime()
        {
            // Get the current time in Taipei timezone
            TimeZoneInfo TaipeiTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Taipei Standard Time");
            DateTime TaipeiTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TaipeiTimeZone);
            return TaipeiTime;
        }

        private static void CheckAndPauseAt3AM(object state)
        {
            if (_isRestartingAt3AM) return;
            DateTime NowTaipei = GetTaipeiStandardTime();
            if (NowTaipei.Hour == 2 && NowTaipei.Minute == 55)
            {
                _isRestartingAt3AM = true;
                Console.WriteLine("Pausing the raid rank process at 3 AM Taipei time...");

                DateTime TargetTime = NowTaipei.Date.AddHours(3);
                TimeSpan DelayToPause = TargetTime - NowTaipei;

                if (DelayToPause.TotalMilliseconds > 0)
                {
                    Console.WriteLine($"Delay {DelayToPause.TotalSeconds:F0} seconds until 3 AM Taipei time.");

                    Thread.Sleep(DelayToPause);
                }




            }
        }

        public static void Start3AMCheckTimer()
        {
            if (_timer3AM == null && !_isRestartingAt3AM)
            {
                _timer3AM = new Timer(CheckAndPauseAt3AM, null, TimeSpan.Zero, TimeSpan.FromMinutes(1));
                Console.WriteLine("3 AM check timer started.");
            }
        }

        public static void Stop3AMCheckTimer()
        {
            _timer3AM?.Change(Timeout.Infinite, Timeout.Infinite);
            _timer3AM?.Dispose();
            _timer3AM = null;
            Console.WriteLine("3 AM check timer stopped.");

        }

        private static void Stop3AMCheckTimerInternal()
        {
            _timer3AM?.Change(Timeout.Infinite, Timeout.Infinite); //
            _timer3AM?.Dispose(); //
            _timer3AM = null; //
            Console.WriteLine("3 AM check timer stopped internally.");
        }


        public static void RaidRankMain(SeasonData currentSeason)
        {

            // Start the 3 AM check timer
            if (!_isRestartingAt3AM)
            {
                Start3AMCheckTimer();
            }
            _isRestartingAt3AM = false; // Reset the flag to allow future restarts


            // Ensure the database table exists for the current season and boss
            string bossName = currentSeason.OpenRaidBossGroup.FirstOrDefault() ?? "UnknownBoss";
            string tableName = $"S{currentSeason.SeasonDisplay}_{bossName}";
            Console.WriteLine($"Target database table: {tableName}");
            DatabaseManager.EnsureTableExists(tableName);

            // Extract the mx.json file path
            string rootPath = AppDomain.CurrentDomain.BaseDirectory;
            string mxFilePath = Path.Combine(rootPath, "mx", "mx.json");
            PacketCryptManager instance = new PacketCryptManager();

            static string ExtractMxToken(string mxFilePath)
            {
                string jsonData = File.ReadAllText(mxFilePath);
                JObject jsonObject = JObject.Parse(jsonData);
                string mxToken = jsonObject["SessionKey"]["MxToken"].ToString();
                return mxToken;
            }

            static string ExtractAccountId(string mxFilePath)
            {
                string jsonData = File.ReadAllText(mxFilePath);
                JObject jsonObject = JObject.Parse(jsonData);
                string accountId = jsonObject["AccountId"].ToString();
                return accountId;
            }

            static string ExtractAccountServerId(string mxFilePath)
            {
                string jsonData = File.ReadAllText(mxFilePath);
                JObject jsonObject = JObject.Parse(jsonData);
                string serverId = jsonObject["SessionKey"]["AccountServerId"].ToString();
                return serverId;
            }

            string mxToken = ExtractMxToken(mxFilePath);
            long hash = 73083163508766;
            string AccountId = ExtractAccountId(mxFilePath);
            string AccountServerId = ExtractAccountServerId(mxFilePath);
            int RankValue = 15; // Starting rank value

            // Create the JSON request body
            string baseJson = "{{\"Protocol\": 17016, " +
                              "\"Rank\": {0}, " +
                              "\"Score\": null, " +
                              "\"IsUpper\": false, " +
                              "\"IsFirstRequest\": true, " +
                              "\"SearchType\": 1, " +
                              "\"ClientUpTime\": 4, " +
                              "\"Resendable\": true, " +
                              "\"Hash\": {1}, " +
                              "\"IsTest\": false, " +
                              "\"SessionKey\":{{" +
                              "\"AccountServerId\": {3}, " +
                              "\"MxToken\": \"{2}\"}}, " +
                              "\"AccountId\": \"{4}\"}}";

            while (true)
            {

                // If BARaidRankMain.Main() is called due to a restart at 3 o'clock,
                // At this point _isRestartingAt3AM should have been reset to false. 
                // However, if this while loop lasts for a long time for some reason, and goes beyond the next 2:55,
                // And CheckAndPauseAt3AMCallback is triggered again and sets _isRestartingAt3AM = true,
                // Then we should jump out of the loop here and let the program restart according to the 3AM logic.

                if (_isRestartingAt3AM)
                {
                    Console.WriteLine("Detected a restart at 3 AM, exiting the loop to allow the program to restart.");
                    Console.WriteLine("Restarting the raid rank process at 3 AM Taipei time...");
                    Thread.Sleep(TimeSpan.FromMinutes(15)); // Wait for 15 minutes before restarting

                    Console.WriteLine("Restarting the raid rank process...");
                    Stop3AMCheckTimerInternal();
                    BARaidRankMain.Main();
                    return; // Exit the loop to allow the program to restart
                }

                string json = string.Format(baseJson, RankValue, hash, mxToken, AccountServerId, AccountId);
                Console.WriteLine($"[{GetTaipeiStandardTime():yyyy-MM-dd HH:mm:ss}] Checking ranking {RankValue} ...");
                byte[] mx = instance.RequestToBinary(Protocol.Raid_OpponentList, json);
                string filePath = "mx.dat";
                File.WriteAllBytes(filePath, mx);


                var options = new RestClientOptions("https://nxm-tw-bagl.nexon.com:5000/api/gateway")
                {
                    Timeout = Timeout.InfiniteTimeSpan
                };
                var client = new RestClient(options);
                var request = new RestRequest();
                request.Method = Method.Post;
                request.AddHeader("mx", "1");
                request.AddFile("mx", filePath);
                RestResponse response = null;
                try
                {
                    response = client.Execute(request);
                    if (!response.IsSuccessful || string.IsNullOrWhiteSpace(response.Content))
                    {

                        Console.WriteLine("The request failed or the response was empty, retrying...");
                        Thread.Sleep(900);
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Request failed: {ex.Message}, retrying...");
                    Thread.Sleep(2000);
                    continue;
                }

                try
                {

                    JObject outerJson = JObject.Parse(response.Content);
                    string packetString = outerJson["packet"].ToString();


                    JObject packetJson = JObject.Parse(packetString);

                    var opponents = packetJson["OpponentUserDBs"];
                    if (opponents == null || !opponents.Any())
                    {
                        Console.WriteLine("Response does not contain 'OpponentUserDBs' or the list is empty, stopping.");
                        RankValue = 1;
                        hash = 73083163508766; // Reset hash and rank value for the next iteration
                        continue;
                    }

                    var rankingsToSave = new List<OpponentInfo>();
                    foreach (var opponent in opponents)
                    {
                        rankingsToSave.Add(new OpponentInfo
                        {
                            Rank = opponent.Value<int>("Rank"),
                            BestRankingPoint = opponent.Value<long>("BestRankingPoint"),
                            Nickname = opponent.Value<string>("Nickname"),
                            Tier = opponent.Value<int>("Tier"),
                            RepresentCharacterUniqueId = opponent.Value<int>("RepresentCharacterUniqueId"),
                            AccountId = opponent.Value<long>("AccountId")
                        });
                    }


                    DatabaseManager.SaveRaidRankings(tableName, rankingsToSave);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing or saving data: {ex.Message}");
                    Console.WriteLine($"Raw response content: {response.Content}");
                }


                RankValue = RankValue + 30;
                hash++;
                Thread.Sleep(900);

            }
        }
    }
}