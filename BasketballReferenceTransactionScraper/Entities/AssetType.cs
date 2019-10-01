namespace BasketballReferenceTransactionScraper.Entities
{
    /// <summary>
    /// The type of asset in a transaction.
    /// </summary>
    public enum AssetType
    {
        /// <summary>
        /// An NBA player.
        /// </summary>
        Player,

        /// <summary>
        /// A pick in an upcoming draft.
        /// </summary>
        DraftPick,

        /// <summary>
        /// A coach of an NBA team.
        /// </summary>
        Coach,

        /// <summary>
        /// An executive of an NBA team.
        /// </summary>
        Executive,

        /// <summary>
        /// Cash considerations.
        /// </summary>
        Cash,

        /// <summary>
        /// Any other asset type.
        /// </summary>
        Other
    }
}