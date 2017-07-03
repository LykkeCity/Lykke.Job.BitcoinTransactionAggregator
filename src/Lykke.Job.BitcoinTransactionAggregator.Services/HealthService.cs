using System;
using Lykke.Job.BitcoinTransactionAggregator.Core.Services;

namespace Lykke.Job.BitcoinTransactionAggregator.Services
{
    public class HealthService : IHealthService
    {
        // NOTE: These are example properties
        public DateTime LastFooStartedMoment { get; private set; }
        public TimeSpan LastFooDuration { get; private set; }
        public TimeSpan MaxHealthyFooDuration { get; }

        // NOTE: These are example properties
        private bool WasLastFooFailed { get; set; }
        private bool WasLastFooCompleted { get; set; }
        private bool WasClientsFooEverStarted { get; set; }

        // NOTE: When you change parameters, don't forget to look in to JobModule

        public HealthService(TimeSpan maxHealthyFooDuration)
        {
            MaxHealthyFooDuration = maxHealthyFooDuration;
        }

        // NOTE: This method probably would stay in the real job, but will be modified
        public string GetHealthViolationMessage()
        {
            if (WasLastFooFailed)
            {
                return "Last foo was failed";
            }

            if (!WasLastFooCompleted && !WasLastFooFailed && !WasClientsFooEverStarted)
            {
                return "Waiting for first foo execution started";
            }

            if (!WasLastFooCompleted && !WasLastFooFailed && WasClientsFooEverStarted)
            {
                return $"Waiting {DateTime.UtcNow - LastFooStartedMoment} for first foo execution completed";
            }

            if (LastFooDuration > MaxHealthyFooDuration)
            {
                return $"Last foo was lasted for {LastFooDuration}, which is too long";
            }
            return null;
        }

        // NOTE: These are example methods
        public void TraceFooStarted()
        {
            LastFooStartedMoment = DateTime.UtcNow;
            WasClientsFooEverStarted = true;
        }

        public void TraceFooCompleted()
        {
            LastFooDuration = DateTime.UtcNow - LastFooStartedMoment;
            WasLastFooCompleted = true;
            WasLastFooFailed = false;
        }

        public void TraceFooFailed()
        {
            WasLastFooCompleted = false;
            WasLastFooFailed = true;
        }

        public void TraceBooStarted()
        {
            // TODO: See Foo
        }

        public void TraceBooCompleted()
        {
            // TODO: See Foo
        }

        public void TraceBooFailed()
        {
            // TODO: See Foo
        }
    }
}