using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Data;
using System.Data.SqlClient;
using System.Collections.Generic;
using NLog;
using HtmlAgilityPack;
using BasketballReferenceTransactionScraper.Entities;

namespace BasketballReferenceTransactionScraper
{
    /// <summary>
    /// Main program.
    /// </summary>
    public static class Program
    {
        private static readonly Logger m_Logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Entry point.
        /// </summary>
        /// <param name="args">command line arguments.</param>
        private static void Main(string[] args)
        {
            m_Logger.Info("Begin");
            try
            {
                using SqlConnection conn = new SqlConnection(Settings.Default.ConnectionString);
                m_Logger.Info("Opening Database Connection");
                conn.Open();
                SyncNewTransactions(conn);
            }
            catch (Exception ex)
            {
                m_Logger.Fatal(ex.Message);
            }
            m_Logger.Info("End");
        }

        private static void SyncNewTransactions(SqlConnection conn)
        {
            DateTime startDate = Helpers.GetLatestDatabaseTransactionDate(conn);
            m_Logger.Info($"Latest Database Transaction Date: {startDate}");
            int startSeasonYear = Helpers.GetSeasonYear(startDate);
            int endUrlYear = Helpers.GetSeasonYear(DateTime.Now);
            TimeSpan syncDuration;

            for (int seasonYear = startSeasonYear; seasonYear <= endUrlYear; seasonYear++)
            {
                DateTime scrapeStartTime = DateTime.Now;

                HtmlNodeCollection scrapedYearTransactions = ScrapeYearTransactions(seasonYear);
                Dictionary<TransactionDetail, List<TransactionAsset>> transactions = ParseYearTransactions(scrapedYearTransactions, seasonYear, startDate, conn);
                InsertYearTransactions(transactions, conn);

                syncDuration = DateTime.Now - scrapeStartTime;
                Helpers.CrawlDelay(syncDuration);
            }
        }

        private static HtmlNodeCollection ScrapeYearTransactions(int seasonYear)
        {
            string url = $"https://www.basketball-reference.com/leagues/NBA_{seasonYear.ToString()}_transactions.html";
            HtmlWeb web = new HtmlWeb();
            HtmlDocument doc = web.Load(url);
            HtmlNodeCollection yearTransactions = doc.DocumentNode.SelectNodes("//*[@class='page_index']/li");

            m_Logger.Info($"Scraped {seasonYear} transactions");

            return yearTransactions;
        }

        private static Dictionary<TransactionDetail, List<TransactionAsset>> ParseYearTransactions(HtmlNodeCollection transactionNodes, int seasonYear, DateTime startDate, SqlConnection conn)
        {
            m_Logger.Info($"Begin parsing {seasonYear} season transactions");
            Dictionary<TransactionDetail, List<TransactionAsset>> transactions = new Dictionary<TransactionDetail, List<TransactionAsset>>();

            foreach (HtmlNode dayNode in transactionNodes)
            {
                try
                {
                    DateTime transactionDate = ParseTransactionDate(dayNode, seasonYear);

                    foreach (HtmlNode transactionNode in dayNode.Descendants("p"))
                    {
                        if (string.IsNullOrWhiteSpace(transactionNode.InnerText) == false)
                        {
                            string transactionDescription = transactionNode.InnerText;

                            if (transactionDate >= DateTime.Now.Date)
                                m_Logger.Info("Skipping transaction where date is equal to or greater than today");
                            else if (Helpers.IsNewTransaction(transactionDate, startDate, transactionDescription, conn))
                            {
                                m_Logger.Info($"Parsing new transaction: {transactionDescription}");
                                Guid transactionId = Guid.NewGuid();
                                TransactionType.Type transactionType = ParseTransactionType(transactionNode);

                                TransactionDetail transactionDetail = new TransactionDetail
                                {
                                    TransactionId = transactionId,
                                    TransactionDate = transactionDate,
                                    TransactionType = transactionType,
                                    TransactionDescription = transactionDescription
                                };

                                List<TransactionAsset> transactionAssets = ParseTransaction(transactionNode, transactionId);

                                transactions.Add(transactionDetail, transactionAssets);
                            }
                            else
                                m_Logger.Info("Skipping previously synced transaction");
                        }
                    }
                }
                catch (Exception ex)
                {
                    m_Logger.Error(ex.Message);
                }
            }

            m_Logger.Info($"End parsing {seasonYear} season transactions");

            return transactions;
        }

        private static DateTime ParseTransactionDate(HtmlNode node, int seasonYear)
        {
            DateTime transactionDate;
            string transactionDateString = node.FirstChild.InnerText;

            if (DateTime.TryParse(transactionDateString, out DateTime dateValue))
                transactionDate = dateValue;
            else
                transactionDate = Helpers.GuessTransactionDate(transactionDateString, seasonYear);

            return transactionDate;
        }

        private static TransactionType.Type ParseTransactionType(HtmlNode node)
        {
            TransactionType.Type transactionType;
            string typeText = FindTransactionTypeText(node);

            switch (typeText)
            {
                case "traded":
                case "trade":
                case "sold":
                    transactionType = TransactionType.Type.Trade;
                    break;
                case "signed":
                case "re-signed":
                case "hired":
                    transactionType = TransactionType.Type.Signing;
                    break;
                case "drafted":
                case "selected":
                    transactionType = TransactionType.Type.Draft;
                    break;
                case "reassigned":
                case "assigned":
                case "recalled":
                case "appointed":
                    transactionType = TransactionType.Type.Reassignment;
                    break;
                case "converted":
                    transactionType = TransactionType.Type.Conversion;
                    break;
                case "suspended":
                    transactionType = TransactionType.Type.Suspension;
                    break;
                case "waived":
                case "claimed":
                    transactionType = TransactionType.Type.Waiver;
                    break;
                case "released":
                case "release":
                case "expires":
                case "fired":
                    transactionType = TransactionType.Type.Release;
                    break;
                case "resigns":
                    transactionType = TransactionType.Type.Resignation;
                    break;
                case "retired":
                case "retirement":
                    transactionType = TransactionType.Type.Retirement;
                    break;
                default:
                    throw new Exception($"Unexpected transaction type: {typeText}");
            }

            return transactionType;
        }

        private static string FindTransactionTypeText(HtmlNode node)
        {
            string typeText = string.Empty;

            foreach (HtmlNode textNode in node.Elements("#text"))
            {
                string text = textNode.InnerText.ToLower().Trim().Split(' ')[0];

                foreach (string str in Helpers.StripPunctionation(textNode.InnerText.ToLower().Trim()).Split(' '))
                {
                    if (TransactionType.Keywords.Contains(str))
                    {
                        typeText = str;
                        break;
                    }
                }

                if (string.IsNullOrWhiteSpace(typeText) == false)
                    break;
            }

            if (string.IsNullOrWhiteSpace(typeText))
                throw new Exception($"Failed to find transaction type text: {node.InnerText}");

            return typeText;
        }

        private static List<TransactionAsset> ParseTransaction(HtmlNode transaction, Guid transactionId)
        {
            List<TransactionAsset> transactionAssets = new List<TransactionAsset>();
            List<HtmlNode> assets = new List<HtmlNode>();
            List<HtmlNode> fromTeams = new List<HtmlNode>();
            List<HtmlNode> toTeams = new List<HtmlNode>();

            foreach (HtmlNode node in transaction.Descendants())
            {
                if (node.Name == "a")
                {
                    switch (node.Attributes[0].Name.ToLower())
                    {
                        case "data-attr-from":
                            fromTeams.Add(node);
                            break;
                        case "data-attr-to":
                            toTeams.Add(node);
                            break;
                        case "href":
                            assets.Add(node);
                            break;
                        default:
                            m_Logger.Error($"Unexpected asset node attribute name: {node.Attributes[0].Name}");
                            break;
                    }
                }
                else
                {
                    string nodeText = node.InnerText.ToLower();

                    if (nodeText.Contains("future draft pick") || nodeText.Contains("round draft pick") || nodeText.Contains(" sold ") || nodeText.Contains(" cash"))
                    {
                        assets.Add(node);
                    }
                }
            }

            transactionAssets.AddRange(CreateTransactionAssets(transactionId, fromTeams, toTeams, assets));

            return transactionAssets;
        }

        private static List<TransactionAsset> CreateTransactionAssets(Guid transactionId, List<HtmlNode> fromTeams, List<HtmlNode> toTeams, List<HtmlNode> assetNodes)
        {
            List<TransactionAsset> transactionAssets = new List<TransactionAsset>();

            foreach (HtmlNode assetNode in assetNodes)
            {
                Tuple<string, string> originDestination = GetOriginDestination(fromTeams, toTeams, assetNode);
                List<string> assets = ParseAssets(assetNode);
                AssetType assetType = GetAssetType(assetNode);

                foreach (string asset in assets)
                {
                    m_Logger.Debug($"ASSET: {asset}");
                    m_Logger.Debug($"ORIGIN: {originDestination.Item1}");
                    m_Logger.Debug($"DESTINATION: {originDestination.Item2}");

                    transactionAssets.Add(new TransactionAsset
                    {
                        TransactionId = transactionId,
                        Origin = originDestination.Item1,
                        Destination = originDestination.Item2,
                        Asset = asset,
                        AssetType = assetType
                    });
                }
            }

            return transactionAssets;
        }

        private static Tuple<string, string> GetOriginDestination(List<HtmlNode> fromTeams, List<HtmlNode> toTeams, HtmlNode asset)
        {
            HtmlNode fromTeam = GetFromTeam(fromTeams, asset);
            HtmlNode toTeam = GetToTeam(toTeams, fromTeam);

            string origin = fromTeam == null ? "FA" : fromTeam.Attributes[0].Value;
            string destination = toTeam == null ? "FA" : toTeam.Attributes[0].Value;

            if (fromTeam != null && toTeam != null)
            {
                if (asset.InnerText.ToLower().Contains(" sold ") || asset.InnerStartIndex > toTeam.InnerStartIndex)
                {
                    origin = toTeam.Attributes[0].Value;
                    destination = fromTeam.Attributes[0].Value;
                }
            }

            return Tuple.Create(origin, destination);
        }

        private static HtmlNode GetFromTeam(List<HtmlNode> fromTeams, HtmlNode asset)
        {
            HtmlNode team = null;

            if (fromTeams.Any())
            {
                team = fromTeams[0];

                if (fromTeams.Count > 1)
                {
                    foreach (HtmlNode fromTeam in fromTeams)
                    {
                        if (fromTeam.InnerStartIndex < asset.InnerStartIndex)
                            team = fromTeam;
                    }
                }
            }

            return team;
        }

        private static HtmlNode GetToTeam(List<HtmlNode> toTeams, HtmlNode fromTeam)
        {
            HtmlNode team = null;

            if (toTeams.Any())
            {
                List<HtmlNode> reverseToTeams = toTeams.AsEnumerable().Reverse().ToList();

                team = reverseToTeams[0];

                if (toTeams.Count > 1 && fromTeam != null)
                {
                    foreach (HtmlNode toTeam in reverseToTeams)
                    {
                        if (toTeam.InnerStartIndex > fromTeam.InnerStartIndex)
                            team = toTeam;
                    }
                }
            }

            return team;
        }

        private static List<string> ParseAssets(HtmlNode assetNode)
        {
            List<string> assets = new List<string>();

            if (assetNode.HasAttributes)
                assets.Add(assetNode.Attributes[0].Value);
            else
            {
                string assetText = assetNode.InnerText.ToLower();

                if (assetText.Contains(" sold ") || assetText.Contains(" cash"))
                    assets.AddRange(ParseCashConsiderations(assetNode));

                if (assetText.Contains("future draft pick") || assetText.Contains("round draft pick"))
                    assets.AddRange(ParseDraftPick(assetNode));
            }

            return assets;
        }

        private static List<string> ParseCashConsiderations(HtmlNode node)
        {
            List<string> assets = new List<string>();
            string nodeText = node.InnerText.ToLower();

            if (nodeText.Contains(" cash") && nodeText.Contains("cornelius") == false)
                assets.Add("Cash Considerations");
            else if (nodeText.Contains(" sold "))
            {
                if (nodeText.Contains("not"))
                {

                }

                if (nodeText.Contains("was"))
                {
                    if (nodeText.Contains("was later"))
                    {
                        //Don't add
                    }
                    else
                    {
                        //Don't add
                    }
                }

                if (nodeText.Contains("future draft pick") || nodeText.Contains("round draft pick"))
                {

                }

                assets.Add("Cash Considerations");
            }

            return assets;
        }

        private static List<HtmlNode> GetDraftNodes(HtmlNode transaction)
        {
            List<HtmlNode> draftNodes = new List<HtmlNode>();

            foreach (HtmlNode node in transaction.Descendants())
            {
                string nodeText = node.InnerText.ToLower();

                if (nodeText.Contains("future draft pick") || nodeText.Contains(" round draft pick "))
                //&& !nodeText.Contains("report says") 
                //&& !nodeText.Contains("penalized") 
                //&& !nodeText.Contains("was for") 
                //&& !nodeText.Contains("contingent") 
                //&& !nodeText.Contains("not to select")
                //&& !nodeText.Contains("protection was removed")
                //&& !nodeText.Contains("swap")
                //&& !nodeText.Contains("did not"))
                {
                    if (nodeText.Contains(" not conveyed "))
                    {

                        foreach (HtmlNode draftNode in draftNodes)
                        {
                            // if draftnode matches non conveyed, remove
                        }
                    }
                    else if (nodeText.Contains(" future ") || nodeText.Contains(" was later "))
                    {
                        if (nodeText.Contains(" cash ") || nodeText.Contains(" sold "))
                        {

                        }
                        else
                        {
                            draftNodes.Add(node);
                        }
                    }
                    else if (node.NextSibling == null || (node.NextSibling.HasAttributes && node.NextSibling.Attributes[0].Name.ToLower() == "href") == false)
                    {

                    }
                    else
                    {

                    }
                }
            }

            return draftNodes;
        }

        private static List<string> ParseDraftPick(HtmlNode node)
        {
            List<string> draftPicks = new List<string>();
            string nodeText = node.InnerText.ToLower();

            if (node.NextSibling == null || node.NextSibling.HasAttributes == false || node.NextSibling.Attributes[0].Name.ToLower() != "href")
            {
                List<int> pickIndexes = Helpers.GetDraftPickIndexes(nodeText);
                int prevIndex = 0;

                foreach (int pickIndex in pickIndexes)
                {
                    string pickText = nodeText[prevIndex..pickIndex];
                    prevIndex = pickIndex;
                    string yearNew = "Future";
                    string roundNew = string.Empty;

                    foreach (string val in Regex.Split(node.InnerText, @"\D+"))
                    {
                        if (val.Length == 1 && int.TryParse(val, out int roundInt))
                            roundNew = $" {Helpers.AddOrdinal(roundInt)} Round";
                        else if (val.Length == 4 && int.TryParse(val, out int yearInt))
                            yearNew = val;
                    }

                    draftPicks.Add($"{yearNew}{roundNew} Draft Pick");
                }
            }

            return draftPicks;
        }

        private static AssetType GetAssetType(HtmlNode assetNode)
        {
            AssetType assetType = new AssetType();

            switch (assetNode.HasAttributes)
            {
                case true:
                    string assetText = assetNode.Attributes[0].Value.ToLower();
                    if (assetText.Contains("/players/"))
                        assetType = AssetType.Player;
                    else if (assetText.Contains("/coaches/"))
                        assetType = AssetType.Coach;
                    else if (assetText.Contains("/executives/"))
                        assetType = AssetType.Executive;
                    else
                        throw new Exception("Unexpected Asset Type");
                    break;
                case false:
                    assetType = AssetType.Other;
                    break;
            }

            return assetType;
        }

        private static void InsertYearTransactions(Dictionary<TransactionDetail, List<TransactionAsset>> transactions, SqlConnection conn)
        {
            using SqlTransaction trans = conn.BeginTransaction();

            try
            {
                foreach (KeyValuePair<TransactionDetail, List<TransactionAsset>> transaction in transactions)
                {
                    InsertTransactionDetail(transaction.Key, trans);
                    InsertTransactionAssets(transaction.Value, trans);
                }
                trans.Commit();
            }
            catch (Exception ex)
            {
                trans.Rollback();
                m_Logger.Error(ex.Message);
            }
        }

        private static void InsertTransactionDetail(TransactionDetail transactionDetail, SqlTransaction trans)
        {
            StringBuilder query = new StringBuilder();
            using SqlCommand cmd = trans.Connection.CreateCommand();

            cmd.Transaction = trans;
            cmd.Parameters.Add("@pId", SqlDbType.UniqueIdentifier).Value = transactionDetail.TransactionId;
            cmd.Parameters.Add("@pDate", SqlDbType.DateTime).Value = transactionDetail.TransactionDate;
            cmd.Parameters.Add("@pDesc", SqlDbType.VarChar).Value = transactionDetail.TransactionDescription;
            cmd.CommandText = "INSERT INTO TransactionDetail (TransactionId, TransacationDate, TransactionDescription) VALUES (@pId, @pDate, @pDesc)";

            int insertedRows = Convert.ToInt32(cmd.ExecuteNonQuery());

            if (insertedRows != 1)
            {
                throw new Exception("Failed to insert transaction detail");
            }
        }

        private static void InsertTransactionAssets(List<TransactionAsset> transactionAssets, SqlTransaction trans)
        {
            StringBuilder query = new StringBuilder("INSERT INTO TransactionAsset (TransactionId, Origin, Destination, Asset VALUES (");
            using SqlCommand cmd = trans.Connection.CreateCommand();

            int i = 0;

            foreach (TransactionAsset transactionAsset in transactionAssets)
            {
                cmd.Parameters.Add($"@pId{i}", SqlDbType.UniqueIdentifier).Value = transactionAsset.TransactionId;
                cmd.Parameters.Add($"@pOrigin{i}", SqlDbType.VarChar, 3).Value = transactionAsset.Origin;
                cmd.Parameters.Add($"@pDest{i}", SqlDbType.VarChar, 3).Value = transactionAsset.Destination;
                cmd.Parameters.Add($"@pAsset{i}", SqlDbType.VarChar).Value = transactionAsset.Asset;
                query.Append($"(@pId{i}, @pOrigin{i}, @pDest{i}, @pAsset{i}), ");
            }

            query.Remove(query.Length - 2, 2);
            query.Append(")");
            cmd.CommandText = query.ToString();
            cmd.Transaction = trans;

            int insertedRows = Convert.ToInt32(cmd.ExecuteNonQuery());
            int assetCount = transactionAssets.Count;

            if (insertedRows != assetCount)
            {
                throw new Exception($"Failed to insert {assetCount - insertedRows}/{assetCount} asset(s)");
            }
        }

    }
}