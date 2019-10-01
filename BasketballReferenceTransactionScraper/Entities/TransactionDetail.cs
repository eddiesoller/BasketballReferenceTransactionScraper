using System;

namespace BasketballReferenceTransactionScraper.Entities
{
    /// <summary>
    /// Data container for a transaction's details.
    /// </summary>
    public class TransactionDetail
    {
        /// <summary>
        /// The transaction's unique identifier.
        /// </summary>
        public Guid TransactionId { get; set; }

        /// <summary>
        /// The date the transaction took place.
        /// </summary>
        public DateTime TransactionDate { get; set; }

        /// <summary>
        /// The type of transaction.
        /// </summary>
        public TransactionType.Type TransactionType { get; set; }

        /// <summary>
        /// The description of the transaction.
        /// </summary>
        public string TransactionDescription { get; set; }

        /// <summary>
        /// Whether the transaction has been independtly verified as correct.
        /// </summary>
        public bool Verified { get; set; }
    }
}