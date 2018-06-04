using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommandLine;
using FlickElectricSharp;
using InfluxDB.Collector;
using InfluxDB.Collector.Diagnostics;
using MoreLinq;

namespace Flick2Influx {
	public class Program {
		private const string PriceMode = "price";
		private const string UsageSimpleMode = "usage-simple";
		private const string UsageDetailedMode = "usage-detailed";

		public static void Main(string[] args) {
			CommandLine.Parser.Default.ParseArguments<Config>(args)
				.WithParsed(options => {
					var collectorConfig = new CollectorConfiguration()
						.WriteTo.InfluxDB(options.InfluxUri, options.InfluxDatabase, options.InfluxUsername, options.InfluxPassword);

					CollectorLog.RegisterErrorHandler((message, exception) => {
						Console.Error.WriteLine($"Error when recording influx stats: \"{message}\"");
						Console.Error.WriteLine(exception);
					});

					using (var influxCollector = collectorConfig.CreateCollector()) {
						if (options.Mode.Equals(PriceMode, StringComparison.InvariantCultureIgnoreCase)) {
							RecordCurrentPrice(options, influxCollector).GetAwaiter().GetResult();
						} else if (options.Mode.Equals(UsageSimpleMode, StringComparison.InvariantCultureIgnoreCase)) {
							RecordSimpleHistoricUsage(options, influxCollector).GetAwaiter().GetResult();
						} else if (options.Mode.Equals(UsageDetailedMode, StringComparison.InvariantCultureIgnoreCase)) {
							RecordDetailedHistoricUsage(options, influxCollector).GetAwaiter().GetResult();
						} else {
							Console.Error.WriteLine($"Unrecognized mode \"{options.Mode}\" A valid --mode of either \"{PriceMode}\", \"{UsageSimpleMode}\" or \"{UsageDetailedMode}\" must be specified");
							Environment.Exit(1);
						}
					}
				});
		}

		private static async Task RecordSimpleHistoricUsage(Config options, MetricsCollector influxCollector) {
			VerifyLookbackSet(options);

			var webClient = new FlickWebClient(options.Username, options.Password);
			var powerUsage = await webClient.GetPowerUsage(DateTime.Now.Subtract(TimeSpan.FromDays(options.LookBackDays)), DateTime.Now).ConfigureAwait(false);

			foreach (var usageBucket in powerUsage) {
				var fields = new Dictionary<string, object> {
					["usage"] = usageBucket.Value
				};

				influxCollector.Write("PowerUsage", fields, timestamp: usageBucket.StartedAt.ToUniversalTime());
			}

			Console.WriteLine($"Finished recording {powerUsage.Count} power usage buckets");
		}

		private static void VerifyLookbackSet(Config options) {
			if (options.LookBackDays <= 0) {
				Console.Error.WriteLine("ERROR: If --mode is set to a form of usage, --look-back-days must be specified");
				Environment.Exit(1);
			}
		}

		private static async Task RecordDetailedHistoricUsage(Config options, MetricsCollector influxCollector) {
			VerifyLookbackSet(options);

			var client = new FlickWebClient(options.Username, options.Password);
			for (var lookback = options.LookBackDays; lookback >= 0; lookback--) {
				var date = DateTime.Now.AddDays(-lookback);
				try {
					Console.WriteLine($"Fetching detailed power usage for {date.Date}");
					var powerUsage = await client.FetchDetailedUsageForDay(date).ConfigureAwait(false);

					foreach (var powerAndPriceInterval in powerUsage) {
						var fields = new Dictionary<string, object> {
							["price"] = powerAndPriceInterval.Price,
							["units"] = powerAndPriceInterval.Units,
							["total_cost"] = powerAndPriceInterval.Units * powerAndPriceInterval.Price
						};

						influxCollector.Write("DetailedPowerUsage", fields, timestamp: powerAndPriceInterval.Start.ToUniversalTime());
					}

				} catch (Exception exception) {
					Console.WriteLine($"Skipping {date} due to an exception {exception}");
				}
			}
		}

		private static async Task RecordCurrentPrice(Config options, MetricsCollector influxCollector) {
			var client = new FlickAndroidClient(options.Username, options.Password);
			var userInfo = await client.GetUserInfo();
			
			var forecastPrices = await client.GetPriceForecast(userInfo.AuthorizedDataContexts.SupplyNodes[0]);
			var currentPredictedPrice = forecastPrices.Prices.MinBy(price => price.StartsAt);

			var priceComponents = new Dictionary<string, object>();
			foreach (var priceComponent in currentPredictedPrice.Components) {
				priceComponents[$"{priceComponent.ChargeSetter}_{priceComponent.ChargeMethod}"] = priceComponent.Value;
			}

			influxCollector.Write("PredictedPrice.Components", priceComponents, timestamp: currentPredictedPrice.StartsAt.ToUniversalTime());

			var total = new Dictionary<string, object> {
				["total"] = currentPredictedPrice.Price.Value
			};

			influxCollector.Write("PredictedPrice.Total", total, timestamp: currentPredictedPrice.StartsAt.ToUniversalTime());

			Console.WriteLine("Finished recording current power price");
		}
	}
}
