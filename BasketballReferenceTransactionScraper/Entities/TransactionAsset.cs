using System;

namespace BasketballReferenceTransactionScraper.Entities
{
    /// <summary>
    /// Data container for asset information in a transaction.
    /// </summary>
    public class TransactionAsset
    {
        /// <summary>
        /// The transaction's unique identifier.
        /// </summary>
        public Guid TransactionId { get; set; }

        /// <summary>
        /// The asset's origin.
        /// </summary>
        public string Origin { get; set; }

        /// <summary>
        /// The asset's destination.
        /// </summary>
        public string Destination { get; set; }

        /// <summary>
        /// The asset.
        /// </summary>
        public string Asset { get; set; }

        /// <summary>
        /// The type of asset.
        /// </summary>
        public AssetType AssetType { get; set; }
    }
}