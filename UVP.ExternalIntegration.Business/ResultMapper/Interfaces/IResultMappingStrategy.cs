namespace UVP.ExternalIntegration.Business.ResultMapper.Interfaces
{
    /// <summary>
    /// Defines system-specific behavior for result mapping operations.
    /// Each integration system implements this to customize field extraction and response handling.
    /// </summary>
    public interface IResultMappingStrategy
    {
        /// <summary>
        /// The system code this strategy handles (e.g., "CMTS", "EARTHMED")
        /// </summary>
        string SystemCode { get; }

        /// <summary>
        /// Determines if this system requires a 3rd cycle (ACKNOWLEDGE_RESPONSE)
        /// </summary>
        bool RequiresAcknowledgeCycle { get; }

        /// <summary>
        /// Number of clearance cycles for this system (2 for EARTHMED, 3 for CMTS)
        /// </summary>
        int ClearanceCycleCount { get; }

        /// <summary>
        /// Extracts the request identifier from the create clearance response
        /// </summary>
        string? ExtractRequestId(string responseBody);

        /// <summary>
        /// Extracts the response identifier from the status response
        /// </summary>
        string? ExtractResponseId(string responseBody);

        /// <summary>
        /// Determines if the status response contains multiple results that need individual processing
        /// </summary>
        bool IsMultiResultStatusResponse(string responseBody);

        /// <summary>
        /// Extracts individual results from a multi-result status response
        /// Returns empty list if single result or extraction fails
        /// </summary>
        Task<List<StatusResultItem>> ExtractStatusResultsAsync(string responseBody);

        /// <summary>
        /// Determines the final status code after status check (e.g., "CLEARED" or "DELIVERED")
        /// </summary>
        string GetStatusCompletionCode();
    }

    /// <summary>
    /// Represents an individual status result for multi-result responses
    /// </summary>
    public class StatusResultItem
    {
        public string Identifier { get; set; } = string.Empty;
        public long CandidateId { get; set; }
        public long DoaCandidateId { get; set; }
        public string? Status { get; set; }
        public DateTime? StatusDate { get; set; }
        public Dictionary<string, string> AdditionalFields { get; set; } = new();
    }
}
