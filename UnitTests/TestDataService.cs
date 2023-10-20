using NUnit.Framework;
using System.Diagnostics;

namespace ProactiveCaching.Tests
{
    public class TestDataService
    {
        private int _callCount = 0;

        public async Task<string> GetDataAsync()
        {
            _callCount++;
            TestContext.WriteLine("GetDataAsync: " + DateTime.Now.ToString("mm:ss.ff") + ", Callcount: " + _callCount);
            return await Task.FromResult($"{{ \"numberOfTimesServiceWasCalled\": {_callCount} }}");
        }
    }
}