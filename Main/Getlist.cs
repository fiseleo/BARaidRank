using System;
using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Threading;

namespace mxdat
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

    public class Getlist
    {
        public static SeasonData GetClosestSeason()
        {
            string rootPath = AppDomain.CurrentDomain.BaseDirectory;
            string sourcePath = Path.Combine(rootPath, "extracted_excels");
            string eliminatorFileName = "EliminateRaidSeasonManageExcelTable.json";
            string raidSeasonFileName = "RaidSeasonManageExcelTable.json";

            string sourceEliminatorFilePath = Path.Combine(sourcePath, eliminatorFileName);
            string sourceRaidSeasonFilePath = Path.Combine(sourcePath, raidSeasonFileName);

            // Read JSON files as strings
            string eliminateRaidSeasonJson = File.ReadAllText(sourceEliminatorFilePath);
            string raidSeasonJson = File.ReadAllText(sourceRaidSeasonFilePath);

            // Deserialize JSON data into SeasonDataList
            var eliminateRaidSeasonsWrapper = JsonConvert.DeserializeObject<SeasonDataList>(eliminateRaidSeasonJson);
            var raidSeasonsWrapper = JsonConvert.DeserializeObject<SeasonDataList>(raidSeasonJson);

            // Assign SourceFile property for each season in the lists
            foreach (var season in eliminateRaidSeasonsWrapper.DataList)
            {
                season.SourceFile = eliminatorFileName;
            }

            foreach (var season in raidSeasonsWrapper.DataList)
            {
                season.SourceFile = raidSeasonFileName;
            }

            // Combine both lists
            var combinedSeasons = new List<SeasonData>();
            combinedSeasons.AddRange(eliminateRaidSeasonsWrapper.DataList);
            combinedSeasons.AddRange(raidSeasonsWrapper.DataList);

            // Handle OpenRaidBossGroup differences
            foreach (var season in combinedSeasons)
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

            // Find the closest season to the current time
            var now = DateTime.Now;
            SeasonData closestSeason = null;
            TimeSpan minTimeSpan = TimeSpan.MaxValue;

            foreach (var season in combinedSeasons)
            {
                var timeSpan = (season.SeasonStartData - now).Duration();
                if (timeSpan < minTimeSpan)
                {
                    minTimeSpan = timeSpan;
                    closestSeason = season;
                }
            }

            return closestSeason;
        }


        public static void GetlistMain(string[] args)
        {
            while (true)
            {
                var closestSeason = GetClosestSeason();
                var now = DateTime.Now;

                // Output the closest OpenRaidBossGroup to the Console if the start date and time is now
                if (closestSeason != null && now >= closestSeason.SeasonStartData && now < closestSeason.SeasonEndData)
                {
                    if (closestSeason.OpenRaidBossGroup != null && closestSeason.OpenRaidBossGroup.Count > 0)
                    {
                        Console.WriteLine("現在開放的是:");
                        foreach (var bossGroup in closestSeason.OpenRaidBossGroup)
                        {
                            Console.WriteLine(bossGroup);
                        }

                        if (closestSeason.SourceFile == "EliminateRaidSeasonManageExcelTable.json")
                        {
                            Console.WriteLine("Executing EliminateRaidOpponentList...");
                            EliminateRaidOpponentList.EliminateRaidOpponentListMain(args, closestSeason.SeasonEndData, closestSeason.SettlementEndDate);
                        }
                        else if (closestSeason.SourceFile == "RaidSeasonManageExcelTable.json")
                        {
                            Console.WriteLine("Executing RaidOpponentList...");
                            RaidOpponentList.RaidOpponentListMain(args, closestSeason.SeasonEndData, closestSeason.SettlementEndDate);
                        }
                        break;
                    }
                    else
                    {
                        Console.WriteLine("沒有開放。");
                    }
                }
                else
                {
                    Console.WriteLine("沒有開放。");
                    Console.WriteLine("按1 執行 RaidOpponentList.RaidOpponentListMain");
                    Console.WriteLine("按2 執行 EliminateRaidOpponentList.EliminateRaidOpponentListMain");
                    Console.WriteLine("等待 1 分鐘後繼續執行 Decryptmxdat.DecryptMain");

                    bool keyPressed = false;

                    for (int i = 0; i < 60; i++)
                    {
                        if (Console.KeyAvailable)
                        {
                            var key = Console.ReadKey(intercept: true).Key;
                            if (key == ConsoleKey.D1 || key == ConsoleKey.NumPad1)
                            {
                                Console.WriteLine("Executing RaidOpponentList...");
                                RaidOpponentList.RaidOpponentListMain(args, closestSeason?.SeasonEndData ?? DateTime.Now, closestSeason?.SettlementEndDate ?? DateTime.Now);
                                keyPressed = true;
                                break;
                            }
                            else if (key == ConsoleKey.D2 || key == ConsoleKey.NumPad2)
                            {
                                Console.WriteLine("Executing EliminateRaidOpponentList...");
                                EliminateRaidOpponentList.EliminateRaidOpponentListMain(args, closestSeason?.SeasonEndData ?? DateTime.Now, closestSeason?.SettlementEndDate ?? DateTime.Now);
                                keyPressed = true;
                                break;
                            }
                        }
                        Thread.Sleep(1000); // Sleep for 1 second
                    }

                    if (!keyPressed)
                    {
                        Console.WriteLine("1 分鐘過去了，繼續執行 Decryptmxdat.DecryptMain...");
                        Decryptmxdat.DecryptMain(args);
                    }
                }
            }
        }
    }
}
