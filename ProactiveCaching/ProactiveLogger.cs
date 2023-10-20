using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProactiveCaching
{
    public interface IProactiveLogger
    {
        void LogInformation(string message);
        void LogError(Exception exception, string message);
        void LogDebug(String message);
    }

    internal class SafeProactiveLogger : IProactiveLogger
    {
        private readonly IProactiveLogger _underlyingLogger;

        public SafeProactiveLogger(IProactiveLogger underlyingLogger)
        {
            _underlyingLogger = underlyingLogger ?? throw new ArgumentNullException(nameof(underlyingLogger));
        }

        public void LogInformation(string message)
        {
            try
            {
                _underlyingLogger.LogInformation(message);
            }
            catch
            {
                // Optionally, you could handle the exception here, 
                // for example by logging it to another logger, but that might be overkill.
            }
        }

        public void LogError(Exception exception, string message)
        {
            try
            {
                _underlyingLogger.LogError(exception, message);
            }
            catch
            {
                // Handle exception if necessary.
            }
        }

        public void LogDebug(string message)
        {
            try
            {
                _underlyingLogger.LogDebug(message);
            }
            catch
            {
                // Handle exception if necessary.
            }
        }
    }
}
