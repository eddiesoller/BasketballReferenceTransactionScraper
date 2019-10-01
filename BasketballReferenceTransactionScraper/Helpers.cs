using System;
using System.Text;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Globalization;

namespace BasketballReferenceTransactionScraper
{
    /// <summary>
    /// Helper methods.
    /// </summary>
    public static class Helpers
    {
        /// <summary>
        /// Gets the latest transaction date from the database.
        /// </summary>
        /// <param name="conn">the connection to the database</param>
        /// <returns>the latest transaction date in the database</returns>
        public static DateTime GetLatestDatabaseTransactionDate(SqlConnection conn)
        {
            DateTime latestDatabaseTrasactionDate = new DateTime(1949, 7, 1);

            using (SqlCommand cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT MAX(Date) FROM Transaction";

                object obj = cmd.ExecuteScalar();

                if (obj != DBNull.Value || obj != null)
                    latestDatabaseTrasactionDate = (DateTime)obj;
            }

            return latestDatabaseTrasactionDate;
        }

        /// <summary>
        /// Gets year to represent the NBA season.
        /// </summary>
        /// <param name="date">the date to get season year for</param>
        /// <returns>the season year as an int</returns>
        public static int GetSeasonYear(DateTime date)
        {
            int seasonYear = date.Year;

            if (date.Month > 6)
                seasonYear += 1;

            return seasonYear;
        }

        /// <summary>
        /// Executes a thread sleep to prevent scraping website too often.
        /// </summary>
        /// <param name="scrapeTime">the amount of time to sleep</param>
        public static void CrawlDelay(TimeSpan scrapeTime)
        {
            if (scrapeTime < TimeSpan.FromSeconds(3))
                System.Threading.Thread.Sleep(3000 - scrapeTime.Milliseconds);
        }

        /// <summary>
        /// Guesses the transaction date when a full date is not defined.
        /// </summary>
        /// <param name="transactionDateString">the incomplete transaction date</param>
        /// <param name="season">the season year</param>
        /// <returns>the guessed transaction date</returns>
        public static DateTime GuessTransactionDate(string transactionDateString, int season)
        {
            DateTime transactionDate;

            if (transactionDateString.Split(' ').Length == 3 && transactionDateString.Split(',').Length == 2)
            {
                int month = DateTime.TryParseExact(transactionDateString.Split(' ')[0], "MMMM", CultureInfo.CurrentCulture, DateTimeStyles.None, out DateTime monthValue) ? monthValue.Month : 1;
                int day = int.TryParse(transactionDateString.Split(' ')[1].Split(',')[0], out int dayValue) ? dayValue : 1;
                int year = int.TryParse(transactionDateString.Split(' ')[2], out int yearValue) ? yearValue : season;

                transactionDate = new DateTime(year, month, day);
            }
            else
                transactionDate = new DateTime(season, 1, 1);

            return transactionDate;
        }

        /// <summary>
        /// Checks if a transaction has already been synced to the database.
        /// </summary>
        /// <param name="transactionDate">the date of the transaction</param>
        /// <param name="latestTransactionDate">the latest synced transaction date</param>
        /// <param name="transactionDescription">the description of the transaction</param>
        /// <param name="conn">the connection to the database</param>
        /// <returns>true if the transaction has not been synced, false if not</returns>
        public static bool IsNewTransaction(DateTime transactionDate, DateTime latestTransactionDate, string transactionDescription, SqlConnection conn)
        {
            bool isNew = true;

            if (transactionDate < latestTransactionDate)
            {
                bool transactionExists = CheckIfTransactionExists(transactionDescription, conn);
                isNew = !transactionExists;
                return isNew;
            }

            return isNew;
        }

        /// <summary>
        /// Checks if a transaction already exists in the database.
        /// </summary>
        /// <param name="transactionDescription">the description of the transaction</param>
        /// <param name="conn">the connection to the database</param>
        /// <returns>true if the transaction exists, false if not</returns>
        private static bool CheckIfTransactionExists(string transactionDescription, SqlConnection conn)
        {
            bool transactionExists = false;

            using (SqlCommand cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT COUNT(1) FROM TransactionDetail WHERE TransactionDescription = @pDesc";
                cmd.Parameters.Add("@pDesc", System.Data.SqlDbType.VarChar).Value = transactionDescription;

                if (Convert.ToInt16(cmd.ExecuteScalar()) > 0)
                    transactionExists = true;
            }

            return transactionExists;
        }

        /// <summary>
        /// Removes punctuation (excluding dashes) from a string.
        /// </summary>
        /// <param name="str">the string to remove punctuation from</param>
        /// <returns>a new string without punctuation</returns>
        public static string StripPunctionation(string str)
        {
            string strippedString;
            var sb = new StringBuilder();

            foreach (char c in str)
            {
                if (!char.IsPunctuation(c))
                    sb.Append(c);
                else if (c.Equals('-'))
                    sb.Append(c);
            }

            strippedString = sb.ToString();

            return strippedString;
        }

        /// <summary>
        /// Gets the string ordinal indexes of draft picks in a string.
        /// </summary>
        /// <param name="draftText">the string containing draft picks</param>
        /// <returns>a list of ordinal indexes</returns>
        public static List<int> GetDraftPickIndexes(this string draftText)
        {
            List<int> indexes = new List<int>();
            string value = "draft pick";

            for (int index = 0; ; index += value.Length)
            {
                index = draftText.IndexOf(value, index);

                if (index == -1)
                    return indexes;

                indexes.Add(index + value.Length);
            }
        }

        /// <summary>
        /// Adds an ordinal to an integer.
        /// </summary>
        /// <param name="num">the integer to add an ordinal to</param>
        /// <returns>an ordinalized string</returns>
        public static string AddOrdinal(int num)
        {
            if (num <= 0) return num.ToString();

            switch (num % 100)
            {
                case 11:
                case 12:
                case 13:
                    return num + "th";
            }

            switch (num % 10)
            {
                case 1:
                    return num + "st";
                case 2:
                    return num + "nd";
                case 3:
                    return num + "rd";
                default:
                    return num + "th";
            }
        }
    }
}
