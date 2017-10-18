using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Common.Log;
using Lykke.AzureRepositories;
using Lykke.Common.Entities.Pay;
using Lykke.Common.Entities.Wallets;
using Lykke.Core;
using Lykke.Job.BitcoinTransactionAggregator.Core;
using Lykke.Job.BitcoinTransactionAggregator.Core.Services;
using Lykke.Pay.Service.Wallets.Client;
using Lykke.Pay.Service.Wallets.Client.Models;
using NBitcoin;
using NBitcoin.RPC;
using Newtonsoft.Json;

namespace Lykke.Job.BitcoinTransactionAggregator.Services
{
    // NOTE: This is job service class example
    public class BitcoitBlocksHandler : IBitcoitBlocksHandler
    {
        private static readonly string ComponentName = "Lykke.Job.BitcoinTransactionAggregator";
        private readonly AppSettings.BitcoinTransactionAggregatorSettings _settings;
        private readonly IBitcoinAggRepository _bitcoinAggRepository;
        private readonly RPCClient _rpcClient;
        private readonly ILog _log;
        private readonly IPayWalletservice _payWalletService;
        private readonly IBitcoinBroadcast _bitcoinBroadcast;
        private readonly IMerchantWalletRepository _merchantWalletRepository;
        private readonly IMerchantWalletHistoryRepository _merchantWalletHistoryRepository;

        public BitcoitBlocksHandler(AppSettings.BitcoinTransactionAggregatorSettings settings, IBitcoinAggRepository bitcoinAggRepository,
            RPCClient rpcClient, IPayWalletservice payWalletService, IBitcoinBroadcast bitcoinBroadcast, ILog log,
            IMerchantWalletRepository merchantWalletRepository, IMerchantWalletHistoryRepository merchantWalletHistoryRepository)
        {
            _settings = settings;
            _bitcoinAggRepository = bitcoinAggRepository;
            _rpcClient = rpcClient;
            _log = log;
            _payWalletService = payWalletService;
            _bitcoinBroadcast = bitcoinBroadcast;
            _merchantWalletRepository = merchantWalletRepository;
            _merchantWalletHistoryRepository = merchantWalletHistoryRepository;
        }
        public async Task ProcessAsync()
        {

            await _log.WriteInfoAsync(ComponentName, "Process started", null,
                $"ProcessAsync rised");
            //IBitcoinAggRepository bitcoinRepo =
            //    new BitcoinAggRepository(
            //        new AzureTableStorage<BitcoinAggEntity>(
            //            generalSettings.LykkePayJobBitcointHandle.Db.MerchantWalletConnectionString, "BitcoinAgg",
            //            null),
            //        new AzureTableStorage<BitcoinHeightEntity>(
            //            generalSettings.LykkePayJobBitcointHandle.Db.MerchantWalletConnectionString, "BitcoinHeight",
            //            null));
            //IMerchantWalletRepository merchantWalletRepository =
            //    new MerchantWalletRepository(new AzureTableStorage<MerchantWalletEntity>(
            //        generalSettings.LykkePayJobBitcointHandle.Db.MerchantWalletConnectionString, "MerchantWallets",
            //        null));
            //IMerchantWalletHistoryRepository merchantWalletHistoryRepository =
            //    new MerchantWalletHistoryRepository(new AzureTableStorage<MerchantWalletHistoryEntity>(
            //        generalSettings.LykkePayJobBitcointHandle.Db.MerchantWalletConnectionString,
            //        "MerchantWalletsHistory", null));

            //var client =
            //    new rpcClient(
            //        new NetworkCredential(generalSettings.LykkePayJobBitcointHandle.Rpc.UserName,
            //            generalSettings.LykkePayJobBitcointHandle.Rpc.Password),
            //        new Uri(generalSettings.LykkePayJobBitcointHandle.Rpc.Url));


            int blockNumner = await _bitcoinAggRepository.GetNextBlockId();
           // blockNumner = 1209936;
            await _log.WriteInfoAsync(ComponentName, "Process started", null,
                $"Bitcoint height {blockNumner}");
            int blockHeight = await _rpcClient.GetBlockCountAsync();
            while (blockNumner <= blockHeight - (_settings.NumberOfConfirm - 1))
            {
                List<WalletModel> wallets = new List<WalletModel>();
                var block = await _rpcClient.GetBlockAsync(blockNumner);
                var inTransactions = new List<String>();
                foreach (var transaction in block.Transactions)
                {
                    foreach (var txIn in transaction.Inputs)
                    {

                        var prevTx = txIn.PrevOut.Hash;
                        if (prevTx.ToString() == "0000000000000000000000000000000000000000000000000000000000000000")
                        {
                            continue;
                        }
                        uint prevN = txIn.PrevOut.N;
                        var pTx = (await _rpcClient.GetRawTransactionAsync(prevTx)).Outputs[prevN];
                        var address = pTx.ScriptPubKey.GetDestinationAddress(_rpcClient.Network)?.ToString();
                        if (string.IsNullOrEmpty(address))
                        {
                            continue;
                        }
                        inTransactions.Add(address);

                        var oTx = (from t in transaction.Outputs
                                   let otAddress = t.ScriptPubKey.GetDestinationAddress(_rpcClient.Network)?.ToString()
                                   where otAddress != null && otAddress.Equals(address)
                                   select t).FirstOrDefault();
                        if (oTx == null)
                        {
                            continue;
                        }

                        var delta = (double)(oTx.Value - pTx.Value).ToDecimal(MoneyUnit.BTC);
                        wallets.Add(new WalletModel { Address = address, AmountChange = delta, TransactionId = transaction.GetHash().ToString() });

                    }

                    foreach (var txOut in transaction.Outputs)
                    {
                        var outAddress = txOut.ScriptPubKey.GetDestinationAddress(_rpcClient.Network)?.ToString();
                        if (inTransactions.Any(itx => itx.Equals(outAddress)))
                        {
                            continue;
                        }


                        var delta = (double)txOut.Value.ToDecimal(MoneyUnit.BTC);

                        wallets.Add(new WalletModel { Address = outAddress, AmountChange = delta, TransactionId = transaction.GetHash().ToString() });
                    }
                }

                await _log.WriteInfoAsync(ComponentName, "Update wallets", null,
                    $"Wallets count: {wallets.Count}");
                await UpdateWallets(wallets, blockNumner, _settings.EncriptionPassword);
                await _log.WriteInfoAsync(ComponentName, "Update blok number", null,
                    "Update blok number");
                blockNumner++;
                await _bitcoinAggRepository.SetNextBlockId(blockNumner);
                blockHeight = await _rpcClient.GetBlockCountAsync();
                await UpdateCountDown(blockHeight - blockNumner);
            }
        }

        private async Task UpdateWallets(List<WalletModel> wallets, int blockNumner, string password)
        {
            await _log.WriteInfoAsync(ComponentName, "Update blok number", null,
                "Get our wallets");
            var ourResponse = await _payWalletService.GetLykkeWalletsWithHttpMessagesAsync(wallets.Select(w => w.Address).ToList());
            var our = ourResponse?.Body as WalletResponseModel;
            await _log.WriteInfoAsync(ComponentName, "Update blok number", null,
                our == null ? "Error with LykkeWalletsWithHttpMessagesAsync" : $"Got  {our.Wallets.Count} wallets");
            if (our == null) return;

            var ourWallets = (from w in wallets
                              join ow in our.Wallets on w.Address equals ow.WalletAddress
                              select w).ToList();

            var rowWallets = (await _merchantWalletRepository.GetAllAddressAsync()).ToList();

            await _log.WriteInfoAsync(ComponentName, "Update blok number", null,
                "store transactions");
            foreach (var ourWallet in ourWallets)
            {

                await _bitcoinAggRepository.SetTransactionAsync(new BitcoinAggEntity
                {
                    WalletAddress = ourWallet.Address,
                    TransactionId = ourWallet.TransactionId,
                    Amount = ourWallet.AmountChange,
                    BlockNumber = blockNumner
                });


                var wallet = rowWallets.First(w => w.WalletAddress.Equals(ourWallet.Address));


                var wallInt = JsonConvert.DeserializeObject<AssertPrivKeyPair>(DecryptData(wallet.Data, password));
                wallInt.Amount += ourWallet.AmountChange;

                var encriptedData = EncryptData(JsonConvert.SerializeObject(wallInt), password);
                await _merchantWalletRepository.SaveNewAddressAsync(new MerchantWalletEntity
                {
                    MerchantId = wallet.MerchantId,
                    WalletAddress = wallInt.Address,
                    Data = encriptedData
                });

                await _merchantWalletHistoryRepository.SaveNewChangeRequestAsync(wallInt.Address, ourWallet.AmountChange, "NA", "Aggregator");
            }
            await _log.WriteInfoAsync(ComponentName, "Update blok number", null,
                "broadcast if need");
            if (_settings.NeedBroadcast && ourWallets.Count > 0)
            {
                await _bitcoinBroadcast.BroadcastMessage(new WalletMqModel { Wallets = ourWallets });
            }
        }


        private async Task UpdateCountDown(int i)
        {
            await _log.WriteInfoAsync(ComponentName, "Handle new block", "On block handled",
                $"Need to handle {i} block(s)");
        }


        protected string EncryptData(string data, string password)
        {

            byte[] result;
            using (var aes = Aes.Create())
            using (var md5 = MD5.Create())
            using (var sha256 = SHA256.Create())
            {
                aes.Key = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                aes.IV = md5.ComputeHash(Encoding.UTF8.GetBytes(password));

                using (var encryptor = aes.CreateEncryptor(aes.Key, aes.IV))
                using (var resultStream = new MemoryStream())
                {
                    using (var aesStream = new CryptoStream(resultStream, encryptor, CryptoStreamMode.Write))
                    using (var plainStream = new MemoryStream(Encoding.UTF8.GetBytes(data)))
                    {
                        plainStream.CopyTo(aesStream);
                    }

                    result = resultStream.ToArray();
                }
            }

            return Convert.ToBase64String(result);
        }


        protected string DecryptData(string data, string password)
        {

            byte[] result;
            using (var aes = Aes.Create())
            using (var md5 = MD5.Create())
            using (var sha256 = SHA256.Create())
            {
                aes.Key = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                aes.IV = md5.ComputeHash(Encoding.UTF8.GetBytes(password));

                using (var decryptor = aes.CreateDecryptor(aes.Key, aes.IV))
                using (var resultStream = new MemoryStream())
                {
                    using (var aesStream = new CryptoStream(resultStream, decryptor, CryptoStreamMode.Write))
                    using (var plainStream = new MemoryStream(Convert.FromBase64String(data)))
                    {
                        plainStream.CopyTo(aesStream);
                    }

                    result = resultStream.ToArray();
                }
            }

            return Encoding.UTF8.GetString(result);
        }


    }


}
