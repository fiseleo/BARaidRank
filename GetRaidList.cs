using Newtonsoft.Json;
using RestSharp;

namespace BARaidRank
{
    public class SeasonDataList
    {
        [JsonProperty("DataList")]
        public List<SeasonData> DataList { get; set; }
    }
    public class SeasonData
    {
        public int SeasonId { get; set; }
        public int SeasonDisplay { get; set; }
        public DateTime SeasonStartData { get; set; }
        public DateTime SeasonEndData { get; set; }
        public DateTime SettlementEndDate { get; set; }
        public List<string> OpenRaidBossGroup { get; set; }
        public string OpenRaidBossGroup01 { get; set; }
        public string OpenRaidBossGroup02 { get; set; }
        public string OpenRaidBossGroup03 { get; set; }
        public string SourceFile { get; set; }
    }

    public class GetRaidList
    {
        static string rootDirectory = AppDomain.CurrentDomain.BaseDirectory;
        static string EliminateRaidListFilePath = Path.Combine(rootDirectory, "EliminateRaidSeasonManageExcelTable.json");
        static string RaidListFilePath = Path.Combine(rootDirectory, "RaidSeasonManageExcelTable.json");

        public static void DownloadRaidList()
        {
            // This method would typically download the raid list from a server or API.

            string RaidDownloadUrl = "https://raw.githubusercontent.com/electricgoat/ba-data/refs/heads/global/Excel/RaidSeasonManageExcelTable.json";

            var RaidClient = new RestClient(RaidDownloadUrl);
            var RaidRequest = new RestRequest();
            RaidRequest.Method = Method.Get;
            var RaidResponse = RaidClient.Execute(RaidRequest);
            if (RaidResponse.IsSuccessful)
            {
                string jsonContest = RaidResponse.Content;
                File.WriteAllText(RaidListFilePath, jsonContest);
                Console.WriteLine($"Raid list downloaded successfully to: {RaidListFilePath}");

            }
            else
            {
                Console.WriteLine($"Error downloading data: {RaidResponse.ErrorMessage}");
            }

            // Download the Eliminate Raid list
            string EliminateRaidDownloadUrl = "https://raw.githubusercontent.com/electricgoat/ba-data/refs/heads/global/Excel/EliminateRaidSeasonManageExcelTable.json";
            var EliminateClient = new RestClient(EliminateRaidDownloadUrl);
            var EliminateRequest = new RestRequest();
            EliminateRequest.Method = Method.Get;
            var EliminateResponse = EliminateClient.Execute(EliminateRequest);
            if (EliminateResponse.IsSuccessful)
            {
                string jsonEliminateContest = EliminateResponse.Content;
                File.WriteAllText(EliminateRaidListFilePath, jsonEliminateContest);
                Console.WriteLine($"Eliminate Raid list downloaded successfully to: {EliminateRaidListFilePath}");
            }
            else
            {
                Console.WriteLine($"Error downloading eliminate data: {EliminateResponse.ErrorMessage}");
            }

        }
        public static SeasonData GetCurrentSeason()
        {
            // Read the JSON file 
            string EliminateRaidListJson = File.ReadAllText(EliminateRaidListFilePath);
            string RaidListJson = File.ReadAllText(RaidListFilePath);

            // Deserialize the JSON data into SeasonDataList objects
            var EliminateRaidSeasonsWrapper = JsonConvert.DeserializeObject<SeasonDataList>(EliminateRaidListJson);
            var RaidSeasonsWrapper = JsonConvert.DeserializeObject<SeasonDataList>(RaidListJson);

            foreach (var season in EliminateRaidSeasonsWrapper.DataList)
            {
                season.SourceFile = EliminateRaidListFilePath;
            }
            foreach (var season in RaidSeasonsWrapper.DataList)
            {
                season.SourceFile = RaidListFilePath;
            }

            // Combine the two lists into one
            var CombinedSeasons = new List<SeasonData>();
            CombinedSeasons.AddRange(EliminateRaidSeasonsWrapper.DataList);
            CombinedSeasons.AddRange(RaidSeasonsWrapper.DataList);

            // Handle OpenRaidBossGroup differences
            foreach (var season in CombinedSeasons)
            {
                if (season.OpenRaidBossGroup == null)
                {
                    season.OpenRaidBossGroup = new List<string>();
                }
                if (!string.IsNullOrEmpty(season.OpenRaidBossGroup01))
                {
                    season.OpenRaidBossGroup.Add(season.OpenRaidBossGroup01);
                }
                if (!string.IsNullOrEmpty(season.OpenRaidBossGroup02))
                {
                    season.OpenRaidBossGroup.Add(season.OpenRaidBossGroup02);
                }
                if (!string.IsNullOrEmpty(season.OpenRaidBossGroup03))
                {
                    season.OpenRaidBossGroup.Add(season.OpenRaidBossGroup03);
                }
            }

            var now = DateTime.Now;
            SeasonData closestSeason = null;
            TimeSpan minTimeSpan = TimeSpan.MaxValue;

            foreach (var season in CombinedSeasons)
            {
                var TimeSpan = (season.SeasonStartData - now).Duration();
                if (TimeSpan < minTimeSpan)
                {
                    minTimeSpan = TimeSpan;
                    closestSeason = season;
                }
            }

            return closestSeason;

        }

        public static void GetRaidListMain()
        {
            DownloadRaidList();
            while (true)
            {
                var closestSeason = GetCurrentSeason();
                var now = DateTime.Now;
                if (closestSeason != null && now >= closestSeason.SeasonStartData && now < closestSeason.SeasonEndData)
                {
                    if (closestSeason.OpenRaidBossGroup != null && closestSeason.OpenRaidBossGroup.Count > 0)
                    {
                        Console.WriteLine("Current Raid Season:");
                        foreach (var bossGroup in closestSeason.OpenRaidBossGroup)
                        {
                            Console.WriteLine(bossGroup);
                        }
                        if (closestSeason.SourceFile == "EliminateRaidSeasonManageExcelTable.json")
                        {
                            Console.WriteLine("Executing EliminateRaidOpponentList...");
                            //TODO: Implement EliminateRaidOpponentList logic here
                        }
                        else if (closestSeason.SourceFile == "RaidSeasonManageExcelTable.json")
                        {
                            Console.WriteLine("Current Raid Season detected:");
                            string bossName = closestSeason.OpenRaidBossGroup.FirstOrDefault() ?? "N/A";
                            Console.WriteLine($"SeasonDisplay: {closestSeason.SeasonDisplay}, Boss: {bossName}");
                            Console.WriteLine("Starting Raid Opponent List fetch and save process...");
                            RaidRank.RaidRankMain(closestSeason);

                        }
                        break;

                    }
                    else
                    {
                        Console.WriteLine("No active raid bosses for the current season.");
                    }
                }
                else
                {
                    Console.WriteLine("No active raid bosses for the current season.");
                    Console.WriteLine("Press 1 Start Raid List");
                    Console.WriteLine("Press 2 Start Eliminate Raid List");
                    Console.WriteLine("Wait for 1 minute to Restart Application");

                    bool KeyPressed = false;
                    for (int i = 0; i < 60; i++)
                    {
                        {
                            var key = Console.ReadKey(true).Key;
                            if (key == ConsoleKey.D1 || key == ConsoleKey.NumPad1)
                            {
                                string RaidListJson = File.ReadAllText(RaidListFilePath);
                                var RaidSeasonsWrapper = JsonConvert.DeserializeObject<SeasonDataList>(RaidListJson);
                                var latestRaidSeason = RaidSeasonsWrapper.DataList
                                               .OrderByDescending(s => s.SeasonDisplay)
                                               .FirstOrDefault();

                                if (latestRaidSeason != null)
                                {
                                    if (latestRaidSeason.OpenRaidBossGroup == null) latestRaidSeason.OpenRaidBossGroup = new List<string>();
                                    if (!string.IsNullOrEmpty(latestRaidSeason.OpenRaidBossGroup01)) latestRaidSeason.OpenRaidBossGroup.Add(latestRaidSeason.OpenRaidBossGroup01);
                                    if (!string.IsNullOrEmpty(latestRaidSeason.OpenRaidBossGroup02)) latestRaidSeason.OpenRaidBossGroup.Add(latestRaidSeason.OpenRaidBossGroup02);
                                    if (!string.IsNullOrEmpty(latestRaidSeason.OpenRaidBossGroup03)) latestRaidSeason.OpenRaidBossGroup.Add(latestRaidSeason.OpenRaidBossGroup03);

                                    string bossName = latestRaidSeason.OpenRaidBossGroup.FirstOrDefault() ?? "N/A";
                                    Console.WriteLine($"Starting with SeasonDisplay: {latestRaidSeason.SeasonDisplay}, Boss: {bossName}");

                                    // 4. 傳遞正確的賽季物件來執行
                                    RaidRank.RaidRankMain(latestRaidSeason);
                                }
                                else
                                {
                                    Console.WriteLine("Could not find any season in RaidSeasonManageExcelTable.json");
                                }
                                KeyPressed = true;
                                break;

                            }
                            else if (key == ConsoleKey.D2 || key == ConsoleKey.NumPad2)
                            {
                                string EliminateRaidListJson = File.ReadAllText(EliminateRaidListFilePath);
                                var EliminateRaidSeasonsWrapper = JsonConvert.DeserializeObject<SeasonDataList>(EliminateRaidListJson);
                                var latestRaidSeason = EliminateRaidSeasonsWrapper.DataList
                                               .OrderByDescending(s => s.SeasonDisplay)
                                               .FirstOrDefault();
                                if (latestRaidSeason != null)
                                {
                                    if (latestRaidSeason.OpenRaidBossGroup == null) latestRaidSeason.OpenRaidBossGroup = new List<string>();
                                    if (!string.IsNullOrEmpty(latestRaidSeason.OpenRaidBossGroup01)) latestRaidSeason.OpenRaidBossGroup.Add(latestRaidSeason.OpenRaidBossGroup01);
                                    if (!string.IsNullOrEmpty(latestRaidSeason.OpenRaidBossGroup02)) latestRaidSeason.OpenRaidBossGroup.Add(latestRaidSeason.OpenRaidBossGroup02);
                                    if (!string.IsNullOrEmpty(latestRaidSeason.OpenRaidBossGroup03)) latestRaidSeason.OpenRaidBossGroup.Add(latestRaidSeason.OpenRaidBossGroup03);

                                    string bossName = latestRaidSeason.OpenRaidBossGroup.FirstOrDefault() ?? "N/A";
                                    Console.WriteLine($"Starting with SeasonDisplay: {latestRaidSeason.SeasonDisplay}");
                                    Console.WriteLine($"Boss 1: {latestRaidSeason.OpenRaidBossGroup01 ?? "N/A"}");
                                    Console.WriteLine($"Boss 2: {latestRaidSeason.OpenRaidBossGroup02 ?? "N/A"}");
                                    Console.WriteLine($"Boss 3: {latestRaidSeason.OpenRaidBossGroup03 ?? "N/A"}");

                                    
                                    EliminateRaidRank.EliminateRaidRankMain(latestRaidSeason);
                                }
                                else
                                {
                                    Console.WriteLine("Could not find any season in EliminateRaidSeasonManageExcelTable.json");
                                }
                                KeyPressed = true;
                                break;
                            }
                        }
                        Thread.Sleep(1000); // Wait for 1 second
                    }
                    if (!KeyPressed)
                    {
                        Console.WriteLine("No key pressed, restarting application...");
                        BARaidRankMain.Main(); // Restart the application
                    }

                }

            }



        }


    }
}