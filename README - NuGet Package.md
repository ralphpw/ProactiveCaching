<!-- This README is specifically for the NuGet package. -->
# ProactiveCaching

`ProactiveCaching` is a .NET library designed to provide proactive caching functionality. Its core philosophy is to prioritize user experience by ensuring data is always available instantly, with no wait time. Based on 7.0 / .NET Standard.


## Unique Selling Point

- **Proactive Refreshes**: Instead of waiting for a request to retrieve data, `ProactiveCaching` periodically refreshes the cache at specified intervals. This ensures that the data is always up-to-date and ready to be served.
  
- **Atomic Data Switch**: Data transitions from old to new occur atomically. When new data is ready, it replaces the old cache instantaneously, ensuring that clients always retrieve the latest available data without any delay.

- **Fault Tolerance**: If fetching the new data fails for any reason, `ProactiveCaching` ensures that the old data remains intact and available. This resilience guarantees that users are not left without data due to intermittent issues or failures.


## Additional Features

- **Flexible Data Sources:** Can work with any asynchronous data source.

- **Exception Handling:** Gracefully handles exceptions during data fetch operations.

- **Async-Aware:** Fully asynchronous API to work seamlessly in modern .NET applications.

Leverage `ProactiveCaching` to optimize your applications by always having fresh data at your fingertips, without compromising on speed or reliability.


## Getting Started

1. **Installation:** Install the `ProactiveCaching` NuGet package.
   
   ```sh
   Install-Package ProactiveCaching
   ```

2. **Basic Usage:**

   ```csharp
   var proactiveCache = new ProactiveCache<string>(
       async () => await GetDataFromDatabase(),
       refreshPeriod: TimeSpan.FromMinutes(5)
   );
   
   var data = await proactiveCache.GetDataAsync();
   ```

## Contributing

This project can be found on GitHub.

Please ensure to update tests as appropriate.

## License

[MIT](https://choosealicense.com/licenses/mit/)
