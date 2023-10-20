using Moq;
using NUnit.Framework;
using ProactiveCaching;
using System.Diagnostics;
using System.Text.Json;

namespace ProactiveCaching.Tests
{
    [TestFixture]
    public class Tests
    {
        [Test]
        public async Task RefreshImmediately_IfStartTimeBeforeNow()
        {
            // Arrange
            using var proactiveCache= new ProactiveCache<string>(
                new TestDataService().GetDataAsync,
                // Expected refresh timings:
                //                      Milliseconds   Value
                //                             0         1
                //                          1000         2
                //                          3000         3
                refreshInterval: TimeSpan.FromMilliseconds(2000),
                refreshStart: DateTime.Now.AddMilliseconds(-1000)
            );

            // Act & Assert
            Assert.AreEqual(1, GetData(proactiveCache));                         // At    0ms, we expect value = 1
            await Task.Delay(500); Assert.AreEqual(1, GetData(proactiveCache));  // At  500ms, we expect value = 1
            await Task.Delay(1000); Assert.AreEqual(2, GetData(proactiveCache)); // At 1500ms, we expect value = 2
            await Task.Delay(2000); Assert.AreEqual(3, GetData(proactiveCache)); // At 3500ms, we expect value = 3
        }

        [Test]
        public async Task DoNotRefreshImmediately_IfStartTimeAfterNow()
        {
            // Arrange
            using var proactiveCache= new ProactiveCache<string>(
                new TestDataService().GetDataAsync,
                // Expected refresh timings:
                //                      Milliseconds   Value
                //                             0         1
                //                          1000         2
                //                          3000         3
                //                          5000         4
                refreshInterval: TimeSpan.FromSeconds(2),
                refreshStart: DateTime.Now.AddSeconds(1)
            );

            // Act & Assert            
            Assert.AreEqual(1, GetData(proactiveCache));                         // At    0ms, we expect value = 1
            await Task.Delay(1500); Assert.AreEqual(2, GetData(proactiveCache)); // At 1500ms, we expect value = 2
            await Task.Delay(1000); Assert.AreEqual(2, GetData(proactiveCache)); // At 2500ms, we expect value = 2
            await Task.Delay(1000); Assert.AreEqual(3, GetData(proactiveCache)); // At 3500ms, we expect value = 3
        }

        [Test]
        public void Handle_LongRunningDataFetchGracefully()
        {
            // Arrange
            async Task<string> SlowGetDataAsync()
            {
                await Task.Delay(5000); // Simulate a slow data fetch
                return await new TestDataService().GetDataAsync();
            }

            using var proactiveCache= new ProactiveCache<string>(
                SlowGetDataAsync,
                refreshInterval: TimeSpan.FromSeconds(1)
            );

            // Act & Assert
            Assert.AreEqual(1, GetData(proactiveCache));

            // Ensure next immediate fetch is still fast and returns the cached value
            var startTime = DateTime.Now;
            var duration = DateTime.Now - startTime;

            Assert.AreEqual(1, GetData(proactiveCache));
            Assert.IsTrue(duration.TotalMilliseconds < 1000); // The fetch should be almost instantaneous
        }

        [Test]
        public async Task ForceImmediateRefresh()
        {
            // Arrange
            using var proactiveCache= new ProactiveCache<string>(
                new TestDataService().GetDataAsync,
                refreshInterval: TimeSpan.FromSeconds(5) // Set a longer refresh period for this test
            );

            // Act & Assert
            Assert.AreEqual(1, GetData(proactiveCache));

            await proactiveCache.ForceRefreshAsync();
            Assert.AreEqual(2, GetData(proactiveCache)); // Data should be updated immediately
        }

        [Test]
        public void Handle_DataFetchFailuresGracefully()
        {
            // Arrange
            async Task<string> FailingGetDataAsync()
            {
                await Task.CompletedTask; // Disables warning CS1998
                throw new InvalidOperationException("Simulated data fetch failure.");
            }

            // Act & Assert
            try
            {
                using var proactiveCache = new ProactiveCache<string>(
                        FailingGetDataAsync,
                        refreshInterval: TimeSpan.FromSeconds(1));
                Assert.ThrowsAsync<InvalidOperationException>(() => proactiveCache.GetDataAsync()); // Initial fetch should throw
            }
            catch (Exception ex)
            {
                Assert.IsTrue(ex is InvalidOperationException);
            }
        }

        [Test]
        public async Task LoggerCapturesFailures()
        {
            // Arrange
            var capturedMessages = new List<string>();
            var logger = new Mock<IProactiveLogger>();
            logger.Setup(l => l.LogError(It.IsAny<Exception>(), It.IsAny<string>()))
                  .Callback((Exception ex, string msg) => capturedMessages.Add(msg));

            async Task<string> FailingGetDataAsync()
            {
                await Task.CompletedTask; // Disables warning CS1998
                throw new InvalidOperationException("Simulated data fetch failure.");
            }

            using var proactiveCache= new ProactiveCache<string>(
                FailingGetDataAsync,
                refreshInterval: TimeSpan.FromSeconds(1),
                logger: logger.Object
            );

            // Act
            try
            {
                await proactiveCache.GetDataAsync();
            }
            catch (InvalidOperationException) { } // Catch the expected exception

            // Assert
            Assert.IsTrue(capturedMessages.Any(msg => msg.Contains("Simulated data fetch failure.")));
        }

        [Test]
        public async Task Cache_RefreshesAfterExpiry()
        {
            // Arrange
            using (var proactiveCache = new ProactiveCache<string>(
                new TestDataService().GetDataAsync,
                refreshInterval: TimeSpan.FromSeconds(1)))
            {

                // Act & Assert
                Assert.AreEqual(1, GetData(proactiveCache));

                await Task.Delay(1100);  // Waiting a little more than refresh period

                Assert.AreEqual(2, GetData(proactiveCache)); // Cache should have been refreshed
            }
        }

        [Test]
        public async Task Handle_MultipleConcurrentRequestsGracefully()
        {
            // Arrange
            using var proactiveCache= new ProactiveCache<string>(
                new TestDataService().GetDataAsync,
                refreshInterval: TimeSpan.FromMinutes(5) // A longer period to make sure it's not refreshed during the test
            );

            // Act & Assert
            var tasks = new List<Task>();
            for (int i = 0; i < 100; i++)
            {
                tasks.Add(Task.Run(async () => {
                    await Task.CompletedTask; // Disables warning CS1998
                    Assert.AreEqual(1, GetData(proactiveCache)); // All tasks should get the same data
                }));
            }

            await Task.WhenAll(tasks);
        }

        [Test]
        public async Task LoggerHandlesDifferentLogLevels()
        {
            // Arrange
            var capturedMessages = new List<string>();
            var logger = new Mock<IProactiveLogger>();

            logger.Setup(l => l.LogInformation(It.IsAny<string>()))
                  .Callback((string msg) => capturedMessages.Add($"INFO: {msg}"));

            logger.Setup(l => l.LogDebug(It.IsAny<string>()))
                  .Callback((string msg) => capturedMessages.Add($"DEBUG: {msg}"));

            using var proactiveCache= new ProactiveCache<string>(
                new TestDataService().GetDataAsync,
                refreshInterval: TimeSpan.FromSeconds(1),
                logger: logger.Object
            );

            // Act
            await proactiveCache.GetDataAsync();

            // Depending on how your logging is set up in ProactiveCache, the below lines are examples. Adjust accordingly.
            logger.Object.LogInformation("This is an info message.");
            logger.Object.LogDebug("This is a debug message.");

            // Assert
            Assert.Contains("INFO: This is an info message.", capturedMessages);
            Assert.Contains("DEBUG: This is a debug message.", capturedMessages);
        }

        [Test]
        public void TriggerRefresh_UpdatesCacheImmediately()
        {
            // Arrange
            using var proactiveCache= new ProactiveCache<string>(
                new TestDataService().GetDataAsync,
                refreshInterval: TimeSpan.FromMinutes(10) // A longer period to make sure it doesn't auto-refresh during the test
            );

            // Act & Assert
            Assert.AreEqual(1, GetData(proactiveCache));

            proactiveCache.ForceRefreshAsync().Wait();  // Triggering a force refresh

            Assert.AreEqual(2, GetData(proactiveCache)); // Cache should have been refreshed immediately
        }

        [Test]
        public async Task CacheReturns_SameValueForDurationOfRefreshPeriod()
        {
            // Arrange
            using var proactiveCache= new ProactiveCache<string>(
                new TestDataService().GetDataAsync,
                refreshInterval: TimeSpan.FromSeconds(2)
            );

            // Act & Assert
            Assert.AreEqual(1, GetData(proactiveCache));

            await Task.Delay(1000);  // Waiting for 1 second

            Assert.AreEqual(1, GetData(proactiveCache)); // Cache should still have the same data

            await Task.Delay(1500);  // Waiting for 1.5 seconds more

            Assert.AreEqual(2, GetData(proactiveCache)); // Cache should have refreshed now
        }

        [Test]
        public async Task ExceptionsHandledGracefully()
        {
            // Arrange
            async Task<string> FaultyGetDataAsync()
            {
                await Task.CompletedTask; // Disables warning CS1998
                throw new Exception("Data fetch failed!");
            }

            using var proactiveCache= new ProactiveCache<string>(
                FaultyGetDataAsync,
                refreshInterval: TimeSpan.FromSeconds(1)
            );

            // Act & Assert
            try
            {
                var data = await proactiveCache.GetDataAsync();
                Assert.Fail("Expected an exception to be thrown!"); // We expect the above line to throw, so if it doesn't, the test fails
            }
            catch (Exception ex)
            {
                Assert.IsTrue(ex.Message.Contains("Data fetch failed!"));
            }
        }

        private static Int32 GetData(ProactiveCache<string> proactiveCache, string propertyName = "numberOfTimesServiceWasCalled")
        {
            var data = JsonSerializer.Deserialize<Dictionary<string, Object>>(proactiveCache.GetDataAsync().Result) ?? throw new ArgumentNullException();
            return ((System.Text.Json.JsonElement)data[propertyName]).GetInt32();
        }
    }
}
