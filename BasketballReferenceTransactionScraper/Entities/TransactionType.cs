using System.Collections.Generic;

namespace BasketballReferenceTransactionScraper.Entities
{
    /// <summary>
    /// The type of transaction.
    /// </summary>
    public static class TransactionType
    {
        /// <summary>
        /// The type of transaction.
        /// </summary>
        public enum Type
        {
            /// <summary>
            /// Two or more teams trading assets.
            /// </summary>
            Trade,

            /// <summary>
            /// A team signing a free agent player.
            /// </summary>
            Signing,

            /// <summary>
            /// A team drafting a player.
            /// </summary>
            Draft,

            /// <summary>
            /// A player being re-assigned.
            /// </summary>
            Reassignment,

            /// <summary>
            /// A player's contract being converted.
            /// </summary>
            Conversion,

            /// <summary>
            /// A player being suspended.
            /// </summary>
            Suspension,

            /// <summary>
            /// A team signing a player from waivers.
            /// </summary>
            Waiver,

            /// <summary>
            /// A team releasing a player.
            /// </summary>
            Release,

            /// <summary>
            /// A player/coach/executive resigning.
            /// </summary>
            Resignation,

            /// <summary>
            /// A player/coach/executive retiring.
            /// </summary>
            Retirement
        }

        /// <summary>
        /// Keywords used by Basketball Reference to describe the transaction types.
        /// </summary>
        public static List<string> Keywords { get; } = new List<string>
        {
            "traded",
            "trade",
            "sold",
            "signed",
            "re-signed",
            "drafted",
            "selected",
            "hired",
            "appointed",
            "reassigned",
            "assigned",
            "recalled",
            "converted",
            "suspended",
            "waived",
            "claimed",
            "released",
            "release",
            "expires",
            "resigns",
            "retired",
            "retirement",
            "fired"
        };
    }
}