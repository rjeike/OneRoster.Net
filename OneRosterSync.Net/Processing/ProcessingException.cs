using System;
using Microsoft.Extensions.Logging;
using OneRosterSync.Net.Models;

namespace OneRosterSync.Net.Processing
{
    public class ProcessingException : Exception
    {
        /// <summary>
        /// ProcessingException Constructor
        /// </summary>
        /// <param name="logger">Logger to use to report this Exception.  Caller should use Logger.Here()</param>
        /// <param name="message">Error message for operator</param>
        /// <param name="innerException">Exception caught this is based on</param>
        public ProcessingException(ILogger logger, string message, Exception innerException = null)
            : base(message, innerException)
        {
            logger.LogError(this.Message, innerException);
        }
    }
}