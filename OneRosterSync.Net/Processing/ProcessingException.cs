using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace OneRosterSync.Net.Processing
{
    public enum ProcessingStage
    {
        Load = 1,
        Analyze = 2,
        Apply = 3,
    }

    public class ProcessingException : Exception
    {
        public ProcessingStage ProcessingStage { get; private set; }

        private static string BuildMessage(ProcessingStage processingStage, string description) => $"[{processingStage}] {description}";

        public ProcessingException(ILogger logger, ProcessingStage processingStage, string description, Exception innerException = null)
            : base(BuildMessage(processingStage, description), innerException)
        {
            logger.LogError(this.Message, innerException);

            ProcessingStage = processingStage;
        }
    }
}
