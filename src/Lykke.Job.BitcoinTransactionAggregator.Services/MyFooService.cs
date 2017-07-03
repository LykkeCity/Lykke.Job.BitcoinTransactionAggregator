using System.Threading.Tasks;
using Lykke.Job.BitcoinTransactionAggregator.Core.Services;

namespace Lykke.Job.BitcoinTransactionAggregator.Services
{
    // NOTE: This is job service class example
    public class MyFooService : IMyFooService
    {
        public Task FooAsync()
        {
            return Task.FromResult(0);
        }
    }
}