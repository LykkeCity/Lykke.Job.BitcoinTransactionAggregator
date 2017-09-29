using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Autofac;
using Common.Log;
using Lykke.Common.Entities.Wallets;
using Lykke.Job.BitcoinTransactionAggregator.Core;
using Lykke.Job.BitcoinTransactionAggregator.Core.Services;
using Lykke.RabbitMqBroker.Publisher;
using Lykke.RabbitMqBroker.Subscriber;
using Newtonsoft.Json;

namespace Lykke.Job.BitcoinTransactionAggregator.Services
{
    public class BitcoinBroadcast : IBitcoinBroadcast, IStartable
    {
        private readonly ILog _log;
        private readonly AppSettings.BitcoinTransactionAggregatorSettings _settings;
        private RabbitMqPublisher<WalletMqModel> _publisher;

        public BitcoinBroadcast(AppSettings.BitcoinTransactionAggregatorSettings settings, ILog log)
        {
            _settings = settings;
            _log = log;
        }

        public async Task BroadcastMessage(WalletMqModel wallets)
        {
            if (wallets != null)
            {
                await _publisher.ProduceAsync(wallets);
            }
        }

        public void Start()
        {

            var settings = RabbitMqSubscriptionSettings

                .CreateForPublisher(_settings.WalletBroadcastRabbit.ConnectionString, _settings.WalletBroadcastRabbit.ExchangeName);


            _publisher = new RabbitMqPublisher<WalletMqModel>(settings).SetPublishStrategy(new DefaultFanoutPublishStrategy(settings))
                .SetSerializer(new WalletBradcastSerializer())
                .DisableInMemoryQueuePersistence()
                .SetLogger(_log)
                .Start();
        }


    }

    public class WalletBradcastSerializer : IRabbitMqSerializer<WalletMqModel>
    {
        public byte[] Serialize(WalletMqModel model)
        {
            string json = JsonConvert.SerializeObject(model);
            return Encoding.UTF8.GetBytes(json);
        }
    }
}
