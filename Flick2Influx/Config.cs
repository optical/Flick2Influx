using System;
using CommandLine;

namespace Flick2Influx {
	public class Config {
		[Option('u', "username", Required = true, HelpText = "Flick Electric Username")]
		public string Username { get; set; }

		[Option('p', "password", Required = true, HelpText = "Flick Electric Password")]
		public string Password { get; set; }

		[Option("influx-uri", Required = true, HelpText = "Uri of the influx server to post the current price to")]
		public Uri InfluxUri { get; set; }

		[Option("influx-database", Required = true, HelpText = "The database on the influx server to record stats in")]
		public string InfluxDatabase { get; set; }

		[Option("influx-username", HelpText = "Optional - The username to use when writing stats to influx")]
		public string InfluxUsername { get; set; }

		[Option("influx-password", HelpText = "Optional - The password corresponding to the influx username when recording stats")]
		public string InfluxPassword { get; set; }

		[Option('m', "mode", Required = true, HelpText = "The mode of operation. Values are either \"price\" for current pricing, \"usage-simple\" for usage data without pricing, or \"usage-detailed\" for a breakdown of usage, including the price paid")]
		public string Mode { get; set; }

		[Option("look-back-days", HelpText = "For the historic usage mode. How many days from now to look back and pump into influx")]
		public int LookBackDays { get; set; }
	}
}
