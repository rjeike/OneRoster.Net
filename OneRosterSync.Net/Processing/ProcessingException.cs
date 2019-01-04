using System;
using Microsoft.Extensions.Logging;
using OneRosterSync.Net.Models;

namespace OneRosterSync.Net.Processing
{
    public class ProcessingException : Exception
    {
        public ProcessingStage ProcessingStage { get; }

        private static string BuildMessage(ProcessingStage processingStage, string description) => $"[{processingStage}] {description}";

        /// <summary>
        /// ProcessingException Constructor
        /// </summary>
        /// <param name="logger">Logger to use to report this Exception.  Caller should use Logger.Here()</param>
        /// <param name="processingStage">Current ProcessingStage where this error occurred.</param>
        /// <param name="description">Error message for operator</param>
        /// <param name="innerException">Exception caught this is based on</param>
        public ProcessingException(ILogger logger, ProcessingStage processingStage, string description, Exception innerException = null)
            : base(BuildMessage(processingStage, description), innerException)
        {
            ProcessingStage = processingStage;

            logger.LogError(this.Message, innerException);
        }
    }
}