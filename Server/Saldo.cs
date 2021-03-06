﻿using Newtonsoft.Json;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using True_Mining_Desktop.Core;
using True_Mining_Desktop.Janelas;
using True_Mining_Desktop.PoolAPI;

namespace True_Mining_Desktop.Server
{
    public class Saldo
    {
        private System.Timers.Timer timerUpdateDashboard = new System.Timers.Timer(1000);

        public Saldo()
        {
            Task.Run(() =>
            {
                Server.SoftwareParameters.Update(new Uri("https://truemining.online/TrueMiningDesktop.json"));

                while (User.Settings.loadingSettings) { Thread.Sleep(500); }

                timerUpdateDashboard.Elapsed += timerUpdateDashboard_Elapsed;

                timerUpdateDashboard.Start();
            });
        }

        private void timerUpdateDashboard_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            UpdateDashboardInfo();
        }

        public void UpdateDashboardInfo()
        {
            if (Tools.WalletAddressIsValid(User.Settings.User.Payment_Wallet))
            {
                Application.Current.Dispatcher.Invoke((Action)delegate
                {
                    if (isUpdatingBalances)
                    {
                        Pages.Dashboard.loadingVisualElement.Visibility = Visibility.Visible;
                        Pages.Dashboard.DashboardContent.IsEnabled = false;
                    }
                    else
                    {
                        Pages.Dashboard.loadingVisualElement.Visibility = Visibility.Hidden;
                        Pages.Dashboard.DashboardContent.IsEnabled = true;
                    }

                    if (lastUpdated.Ticks < DateTime.Now.Ticks && Pages.Dashboard.IsLoaded)
                    {
                        lastUpdated = DateTime.Now.AddMinutes(10);
                        UpdateBalances();
                    }

                    Pages.Dashboard.LabelNextPayout = ((int)23 - (int)DateTime.UtcNow.Hour) + " hours, " + ((int)59 - (int)DateTime.UtcNow.Minute) + " minutes";
                    Pages.Dashboard.LabelAccumulatedBalance = Math.Round(AccumulatedBalance_Points, 0) + " points ⇒ ≈ " + Math.Round(AccumulatedBalance_Coins, 4) + ' ' + User.Settings.User.Payment_Coin;
                    Pages.Dashboard.WarningWrapVisibility = System.Windows.Visibility.Hidden;
                    Pages.Dashboard.LabelWarning = null;
                });
            }
            else
            {
                Application.Current.Dispatcher.Invoke((Action)delegate
                {
                    Pages.Dashboard.loadingVisualElement.Visibility = Visibility.Hidden;
                    Pages.Dashboard.DashboardContent.IsEnabled = true;

                    Pages.Dashboard.LabelNextPayout = ((int)23 - (int)DateTime.UtcNow.Hour) + " hours, " + ((int)59 - (int)DateTime.UtcNow.Minute) + " minutes";
                    Pages.Dashboard.LabelAccumulatedBalance = "??? points ⇒ ≈ ??? COINs";
                    Pages.Dashboard.WarningWrapVisibility = System.Windows.Visibility.Visible;
                    Pages.Dashboard.LabelWarning = "you need to enter your wallet address on the home screen so we can view your balances";
                });
            }
        }

        public bool isUpdatingBalances;

        public int hoursRound = DateTime.UtcNow.Hour + 1;

        public double AccumulatedBalance_Points = 0;
        public double AccumulatedBalance_Coins = 0;

        public double conversionHashrateToPoints;
        public double exchangeRatePontosToMiningCoin;

        private static DateTime lastUpdated = DateTime.Now.AddMinutes(-10);

        public void UpdateBalances()
        {
            Task.Run(() =>
            {
                hoursRound = DateTime.UtcNow.Hour + 1;

                isUpdatingBalances = true;

                Application.Current.Dispatcher.Invoke((Action)delegate
                {
                    if (isUpdatingBalances)
                    {
                        Pages.Dashboard.loadingVisualElement.Visibility = Visibility.Visible;
                        Pages.Dashboard.DashboardContent.IsEnabled = false;
                    }
                });

                while (!Tools.IsConnected()) { Thread.Sleep(5000); }
                try
                {
                    PoolAPI.XMR_nanopool.AvghashratelimitedAll = JsonConvert.DeserializeObject<PoolAPI.XMR_nanopool_avghashratelimited>(new WebClient().DownloadString("https://api.nanopool.org/v1/xmr/avghashratelimited/" + Server.SoftwareParameters.ServerConfig.Pools[0].wallet_TM + '/' + hoursRound));
                    PoolAPI.XMR_nanopool.AvghashratelimitedThisworker = JsonConvert.DeserializeObject<PoolAPI.XMR_nanopool_avghashratelimited>(new WebClient().DownloadString("https://api.nanopool.org/v1/xmr/avghashratelimited/" + Server.SoftwareParameters.ServerConfig.Pools[0].wallet_TM + '/' + User.Settings.User.Payment_Wallet + '/' + hoursRound));
                    PoolAPI.BitcoinPrice.FIAT_rates = JsonConvert.DeserializeObject<PoolAPI.Coins>(new WebClient().DownloadString("https://blockchain.info/ticker"));
                    PoolAPI.Crex24.XMRBTC_Orderbook = JsonConvert.DeserializeObject<Orderbook>(new WebClient().DownloadString(new Uri("https://api.crex24.com/v2/public/orderBook?instrument=XMR-BTC")));
                    PoolAPI.Crex24.MiningCoinBTC_Orderbook = JsonConvert.DeserializeObject<Orderbook>(new WebClient().DownloadString(new Uri("https://api.crex24.com/v2/public/orderBook?instrument=" + User.Settings.User.Payment_Coin + "-BTC")));
                    XMR_nanopool.approximated_earnings = JsonConvert.DeserializeObject<PoolAPI.approximated_earnings>(new WebClient().DownloadString(new Uri("https://api.nanopool.org/v1/xmr/approximated_earnings/35")));
                }
                catch { }

                AccumulatedBalance_Points = XMR_nanopool.AvghashratelimitedThisworker.data * hoursRound * 60 * 60 / 52.5 / 1200 / 8;
                conversionHashrateToPoints = 24 * 60 * 60 / 52.5 / 1200 / 8;

                double totalXMRmineradoTrueMining = XMR_nanopool.approximated_earnings.data.hour.coins * PoolAPI.XMR_nanopool.AvghashratelimitedAll.data * hoursRound * 60 * 60 / 52.5 / 1200 / 8;

                double XMRpraVirarBTC = totalXMRmineradoTrueMining;

                double XMRfinalPrice = 0;

                for (int i = 0; XMRpraVirarBTC > 0; i++)
                {
                    int I = i;
                    if (Crex24.XMRBTC_Orderbook.buyLevels[I].volume > XMRpraVirarBTC)
                    {
                        XMRpraVirarBTC -= Crex24.XMRBTC_Orderbook.buyLevels[I].volume;
                        XMRfinalPrice = Crex24.XMRBTC_Orderbook.buyLevels[I].price;
                    }
                    else
                    {
                        XMRpraVirarBTC -= Crex24.XMRBTC_Orderbook.buyLevels[I].volume;
                    }
                }

                double BTCpraVirarCOIN = totalXMRmineradoTrueMining * XMRfinalPrice;

                double COINfinalPrice = 0;

                for (int i = 0; BTCpraVirarCOIN > 0; i++)
                {
                    int I = i;
                    if (Crex24.MiningCoinBTC_Orderbook.sellLevels[I].volume > BTCpraVirarCOIN / Crex24.MiningCoinBTC_Orderbook.sellLevels[I].price)
                    {
                        BTCpraVirarCOIN -= Crex24.MiningCoinBTC_Orderbook.sellLevels[I].volume;
                        COINfinalPrice = Crex24.MiningCoinBTC_Orderbook.sellLevels[I].price;
                    }
                    else
                    {
                        BTCpraVirarCOIN -= Crex24.MiningCoinBTC_Orderbook.sellLevels[I].price * Crex24.MiningCoinBTC_Orderbook.sellLevels[I].volume;
                    }
                }

                exchangeRatePontosToMiningCoin = XMR_nanopool.approximated_earnings.data.hour.coins * XMRfinalPrice / COINfinalPrice * 4;

                AccumulatedBalance_Coins = Math.Round((exchangeRatePontosToMiningCoin * AccumulatedBalance_Points * (100 - 3) / 100), 4); //fee

                isUpdatingBalances = false;
            });
        }
    }
}