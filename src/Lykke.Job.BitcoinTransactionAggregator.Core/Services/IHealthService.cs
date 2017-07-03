using System;

namespace Lykke.Job.BitcoinTransactionAggregator.Core.Services
{
    public interface IHealthService
    {
        // NOTE: These are example properties
        DateTime LastFooStartedMoment { get; }
        TimeSpan LastFooDuration { get; }
        TimeSpan MaxHealthyFooDuration { get; }

        // NOTE: This method probably would stay in the real job, but will be modified
        string GetHealthViolationMessage();

        // NOTE: These are example methods
        void TraceFooStarted();
        void TraceFooCompleted();
        void TraceFooFailed();
        void TraceBooStarted();
        void TraceBooCompleted();
        void TraceBooFailed();
    }
}