// DatabaseManager.cs
using Microsoft.Data.Sqlite;


namespace BARaidRank
{

    public class OpponentInfo
    {
        public long BestRankingPoint { get; set; }
        public string Nickname { get; set; }
        public int Tier { get; set; }
        public long AccountId { get; set; }
        public int Rank { get; set; }
        public int RepresentCharacterUniqueId { get; set; }
    }

    public class EliminateOpponentInfo
    {
        public string Nickname { get; set; }
        public int Tier { get; set; }
        public int RepresentCharacterUniqueId { get; set; }
        public long AccountId { get; set; }
        public int Rank { get; set; }
        public Dictionary<string, long> BossGroupToRankingPoint { get; set; }
    }

    public static class DatabaseManager
    {
        private static readonly string dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RaidDatabase.db");

        public static void EnsureTableExists(string tableName)
        {
            using (var connection = new SqliteConnection($"Data Source={dbPath}"))
            {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = $@"
                    CREATE TABLE IF NOT EXISTS ""{tableName}"" (
                        Rank INTEGER PRIMARY KEY,
                        BestRankingPoint BIGINT,
                        Nickname TEXT,
                        Tier INTEGER,
                        RepresentCharacterUniqueId INTEGER,
                        AccountId BIGINT
                    );
                ";
                command.ExecuteNonQuery();
            }
        }

        public static void SaveRaidRankings(string tableName, List<OpponentInfo> rankings)
        {
            using (var connection = new SqliteConnection($"Data Source={dbPath}"))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    foreach (var rankInfo in rankings)
                    {
                        var command = connection.CreateCommand();
                        command.CommandText = $@"
                            INSERT OR REPLACE INTO ""{tableName}"" 
                            (Rank, BestRankingPoint, Nickname, Tier, RepresentCharacterUniqueId, AccountId) 
                            VALUES ($rank, $point, $name, $tier, $charId, $accountId);
                        ";
                        command.Parameters.AddWithValue("$rank", rankInfo.Rank);
                        command.Parameters.AddWithValue("$point", rankInfo.BestRankingPoint);
                        command.Parameters.AddWithValue("$name", rankInfo.Nickname);
                        command.Parameters.AddWithValue("$tier", rankInfo.Tier);
                        command.Parameters.AddWithValue("$charId", rankInfo.RepresentCharacterUniqueId);
                        command.Parameters.AddWithValue("$accountId", rankInfo.AccountId);
                        command.ExecuteNonQuery();
                    }
                    transaction.Commit();
                }
            }
            Console.WriteLine($"Successfully saved/updated {rankings.Count} records to table '{tableName}'.");
        }

        public static string ExtractArmorType(string bossGroup)
        {
            if (string.IsNullOrEmpty(bossGroup)) return "UnknownArmor";
            string[] parts = bossGroup.Split('_');
            string potentialArmor = parts.LastOrDefault();
            switch(potentialArmor)
            {
                case "LightArmor":
                case "HeavyArmor":
                case "Unarmed":
                case "ElasticArmor":
                    return potentialArmor;
                default:
                    if (bossGroup.Contains("LightArmor")) return "LightArmor";
                    if (bossGroup.Contains("HeavyArmor")) return "HeavyArmor";
                    if (bossGroup.Contains("Unarmed")) return "Unarmed";
                    if (bossGroup.Contains("ElasticArmor")) return "ElasticArmor";
                    return "UnknownArmor"; 
            }
        }


        public static void EnsureEliminateTableExists(string tableName, List<string> bossArmorTypes)
        {
            using (var connection = new SqliteConnection($"Data Source={dbPath}"))
            {
                connection.Open();
                var command = connection.CreateCommand();
                
                string columns = @"
                    Rank INTEGER PRIMARY KEY,
                    Nickname TEXT,
                    Tier INTEGER,
                    RepresentCharacterUniqueId INTEGER,
                    AccountId BIGINT
                ";

                foreach (var armorType in bossArmorTypes.Distinct())
                {
                    string sanitizedArmorTypeColumn = armorType.Replace(" ", "_").Replace("-", "_");
                    columns += $",\n \"{sanitizedArmorTypeColumn}\" BIGINT"; 
                }

                command.CommandText = $@"CREATE TABLE IF NOT EXISTS ""{tableName}"" ({columns});";
                command.ExecuteNonQuery();
            }
        }
        public static void SaveEliminateRaidRankings(string tableName, List<EliminateOpponentInfo> rankings, List<string> bossArmorTypes)
        {
            using (var connection = new SqliteConnection($"Data Source={dbPath}"))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    foreach (var rankInfo in rankings)
                    {
                        var command = connection.CreateCommand();
                        
                        string columnNames = "Rank, Nickname, Tier, RepresentCharacterUniqueId, AccountId";
                        string valuePlaceholders = "$rank, $nickname, $tier, $charId, $accountId";

                        command.Parameters.AddWithValue("$rank", rankInfo.Rank);
                        command.Parameters.AddWithValue("$nickname", rankInfo.Nickname);
                        command.Parameters.AddWithValue("$tier", rankInfo.Tier);
                        command.Parameters.AddWithValue("$charId", rankInfo.RepresentCharacterUniqueId);
                        command.Parameters.AddWithValue("$accountId", rankInfo.AccountId);

                        foreach (var armorType in bossArmorTypes.Distinct())
                        {
                            string sanitizedArmorTypeColumn = armorType.Replace(" ", "_").Replace("-", "_");
                            

                            columnNames += $", \"{sanitizedArmorTypeColumn}\""; 
                            

                            string paramName = $"${sanitizedArmorTypeColumn.ToLower()}"; 
                            valuePlaceholders += $", {paramName}";

                            long score = 0;
                            var bossScoreEntry = rankInfo.BossGroupToRankingPoint
                                .FirstOrDefault(kvp => ExtractArmorType(kvp.Key) == armorType);
                            if (!string.IsNullOrEmpty(bossScoreEntry.Key))
                            {
                                score = bossScoreEntry.Value;
                            }
                            command.Parameters.AddWithValue(paramName, score);
                        }
                        
                        command.CommandText = $@"
                            INSERT OR REPLACE INTO ""{tableName}"" ({columnNames}) 
                            VALUES ({valuePlaceholders});
                        ";
                        command.ExecuteNonQuery();
                    }
                    transaction.Commit();
                }
            }
            Console.WriteLine($"Successfully saved/updated {rankings.Count} records to table '{tableName}'.");
        }
    }
}