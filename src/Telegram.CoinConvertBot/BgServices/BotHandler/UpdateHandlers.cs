using FreeSql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Serilog;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InlineQueryResults;
using Telegram.Bot.Types.InputFiles;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.CoinConvertBot.Domains.Tables;
using Telegram.CoinConvertBot.Helper;
using Telegram.CoinConvertBot.Models;
using TronNet;
using TronNet.Contracts;
using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Net.Http;
using System.Data;
using System.Text;


namespace Telegram.CoinConvertBot.BgServices.BotHandler;


public static class UpdateHandlers
{
    public static string? BotUserName = null!;
    public static IConfiguration configuration = null!;
    public static IFreeSql freeSql = null!;
    public static IServiceScopeFactory serviceScopeFactory = null!;
    public static long AdminUserId => configuration.GetValue<long>("BotConfig:AdminUserId");
    public static string AdminUserUrl => configuration.GetValue<string>("BotConfig:AdminUserUrl");
    public static decimal MinUSDT => configuration.GetValue("MinToken:USDT", 5m);
    public static decimal FeeRate => configuration.GetValue("FeeRate", 0.1m);
    public static decimal USDTFeeRate => configuration.GetValue("USDTFeeRate", 0.01m);
    /// <summary>
    /// 错误处理
    /// </summary>
    /// <param name="botClient"></param>
    /// <param name="exception"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
public static async Task<string> GetTransactionRecordsAsync()
{
    string outcomeAddress = "TXkRT6uxoMJksnMpahcs19bF7sJB7f2zdv";
    string outcomeUrl = $"https://apilist.tronscan.org/api/transaction?address={outcomeAddress}&token=TR7NHqjeKQxGTCi8q8ZY4pL8otSzgjLj6t&limit=50&page=1";

    using (var httpClient = new HttpClient())
    {
        var outcomeResponse = await httpClient.GetStringAsync(outcomeUrl);
        var outcomeTransactions = ParseTransactions(outcomeResponse, "TRX");
        return FormatTransactionRecords(outcomeTransactions);
    }
}

private static List<(DateTime timestamp, string token, decimal amount)> ParseTransactions(string jsonResponse, string token)
{
    var transactions = new List<(DateTime timestamp, string token, decimal amount)>();

    var json = JObject.Parse(jsonResponse);
    var dataArray = json["data"] as JArray;

    if (dataArray != null)
    {
        foreach (var data in dataArray)
        {
            // 添加检查发送方地址的条件
            if (data["ownerAddress"] != null && data["ownerAddress"].ToString() == "TXkRT6uxoMJksnMpahcs19bF7sJB7f2zdv" &&
                data["timestamp"] != null && data["contractData"] != null && data["contractData"]["amount"] != null)
            {
                var timestamp = DateTimeOffset.FromUnixTimeMilliseconds((long)data["timestamp"]).LocalDateTime;
                var amount = decimal.Parse(data["contractData"]["amount"].ToString()) / 1000000;

                if (amount > 1) // 添加条件，只添加金额大于1的交易记录
                {
                    transactions.Add((timestamp, token, amount));
                }
            }
        }
    }

    return transactions;
}

private static string FormatTransactionRecords(List<(DateTime timestamp, string token, decimal amount)> outcomeTransactions)
{
    var sb = new StringBuilder();
    int numOfRecords = 0;

    for (int i = 0; i < outcomeTransactions.Count && numOfRecords < 10; i++)
    {
        sb.AppendLine($"支出：{outcomeTransactions[i].timestamp:yyyy-MM-dd HH:mm:ss} 支出{outcomeTransactions[i].token} {outcomeTransactions[i].amount}");
        sb.AppendLine("————————————————");
        numOfRecords++;
    }

    return sb.ToString();
}    
private static async Task HandleTranslateCommandAsync(ITelegramBotClient botClient, Message message)
{
    // 从消息中提取目标语言和待翻译文本
    var match = Regex.Match(message.Text, @"转([\u4e00-\u9fa5]+)\s+(.+)");

    if (match.Success)
    {
        var targetLanguageName = match.Groups[1].Value;
        var textToTranslate = match.Groups[2].Value;

        if (LanguageCodes.TryGetValue(targetLanguageName, out string targetLanguageCode))
        {
            // 使用 GoogleTranslateFree 或其他翻译服务进行翻译
            var translatedText = await GoogleTranslateFree.TranslateAsync(textToTranslate, targetLanguageCode);
            await botClient.SendTextMessageAsync(message.Chat.Id, $"翻译结果：\n\n<code>{translatedText}</code>", parseMode: ParseMode.Html);
        }
        else
        {
            // 如果目标语言不在字典中，返回不支持的消息
            var supportedLanguages = string.Join("、", LanguageCodes.Keys);
            await botClient.SendTextMessageAsync(message.Chat.Id, $"暂不支持该语种转换，目前转换语言支持：{supportedLanguages}");
        }
    }
    else
    {
        // 如果消息格式不正确，返回错误消息
        await botClient.SendTextMessageAsync(message.Chat.Id, "无法识别的翻译命令，请确保您的输入格式正确，例如：<code>转英语 你好</code>", parseMode: ParseMode.Html);
    }
}
private static readonly Dictionary<string, string> LanguageCodes = new Dictionary<string, string>
{
    { "英语", "en" },
    { "日语", "ja" },
    { "韩语", "ko" },
    { "越南语", "vi" },
    { "高棉语", "km" },
    { "泰语", "th" },
    { "菲律宾语", "tl" },
    { "阿拉伯语", "ar" },
    { "老挝语", "lo" },
    { "马来西亚语", "ms" },
    { "西班牙语", "es" },
    { "印地语", "hi" },
    { "孟加拉文", "bn" },
    { "葡萄牙语", "pt" },
    { "俄罗斯语", "ru" },
    { "旁遮普文", "pa" },
    { "马拉地文", "mr" },
    { "泰米尔文", "ta" },
    { "土耳其语", "tr" },
    { "法语", "fr" },
    { "卡纳达文", "kn" },
    { "泰卢固文", "te" },
    { "乌尔都语", "ur" },
};    
public class GoogleTranslateFree
{
    private const string GoogleTranslateUrl = "https://translate.google.com/translate_a/single?client=gtx&sl=auto&tl={0}&dt=t&q={1}";
    
    public static async Task<string> TranslateAsync(string text, string targetLanguage)
    {
        using var httpClient = new HttpClient();

        var url = string.Format(GoogleTranslateUrl, Uri.EscapeDataString(targetLanguage), Uri.EscapeDataString(text));
        var response = await httpClient.GetAsync(url);
        var json = await response.Content.ReadAsStringAsync();
        var jsonArray = JArray.Parse(json);

        var translatedTextBuilder = new StringBuilder();
        foreach (var segment in jsonArray[0])
        {
            translatedTextBuilder.Append(segment[0].ToString());
        }

        return translatedTextBuilder.ToString();
    }
}   
public static async Task<decimal> GetTotalIncomeAsync(string address, bool isTrx)
{
    var apiUrl = $"https://api.trongrid.io/v1/accounts/{address}/transactions/trc20?only_confirmed=true&only_to=true&limit=200&contract_address=TR7NHqjeKQxGTCi8q8ZY4pL8otSzgjLj6t";
    using var httpClient = new HttpClient();

    decimal totalIncome = 0m;
    string fingerprint = null;

    while (true)
    {
        var currentUrl = apiUrl + (fingerprint != null ? $"&fingerprint={fingerprint}" : "");
        var response = await httpClient.GetAsync(currentUrl);
        var json = await response.Content.ReadAsStringAsync();
        var jsonDocument = JsonDocument.Parse(json);

        if (!jsonDocument.RootElement.TryGetProperty("data", out JsonElement dataElement))
        {
            break;
        }

        foreach (var transactionElement in dataElement.EnumerateArray())
        {
            if (!transactionElement.TryGetProperty("to", out var toAddressElement))
            {
                continue;
            }
            var toAddress = toAddressElement.GetString();

            if (toAddress != address)
            {
                continue;
            }

            if (!transactionElement.TryGetProperty("value", out var valueElement))
            {
                continue;
            }
            var value = valueElement.GetString();

            totalIncome += decimal.Parse(value) / 1_000_000; //假设USDT有6位小数

            if (transactionElement.TryGetProperty("transaction_hash", out var transactionIdElement))
            {
                fingerprint = transactionIdElement.GetString();
            }
        }

        if (!jsonDocument.RootElement.TryGetProperty("has_next", out JsonElement hasNextElement) || !hasNextElement.GetBoolean())
        {
            break;
        }
    }

    return totalIncome;
}
    
public static DateTime ConvertToBeijingTime(DateTime utcDateTime)
{
    var timeZone = TimeZoneInfo.FindSystemTimeZoneById("China Standard Time");
    return TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, timeZone);
}
    
public static async Task<DateTime> GetLastTransactionTimeAsync(string address)
{
    using var httpClient = new HttpClient();
    var response = await httpClient.GetAsync($"https://api.trongrid.io/v1/accounts/{address}/transactions");
    var json = await response.Content.ReadAsStringAsync();

    var jsonDocument = JsonDocument.Parse(json);
    var lastTimestamp = 0L;

    if (jsonDocument.RootElement.TryGetProperty("data", out JsonElement dataElement) && dataElement.GetArrayLength() > 0)
    {
        var lastElement = dataElement[0];

        if (lastElement.TryGetProperty("block_timestamp", out JsonElement lastTimeElement))
        {
            lastTimestamp = lastTimeElement.GetInt64();
        }
    }

    var utcDateTime = DateTimeOffset.FromUnixTimeMilliseconds(lastTimestamp).DateTime;
    return ConvertToBeijingTime(utcDateTime);
}
    
public static async Task<DateTime> GetAccountCreationTimeAsync(string address)
{
    using var httpClient = new HttpClient();
    var response = await httpClient.GetAsync($"https://api.trongrid.io/v1/accounts/{address}");
    var json = await response.Content.ReadAsStringAsync();

    var jsonDocument = JsonDocument.Parse(json);
    var creationTimestamp = 0L;

    if (jsonDocument.RootElement.TryGetProperty("data", out JsonElement dataElement) && dataElement.GetArrayLength() > 0)
    {
        var firstElement = dataElement[0];

        if (firstElement.TryGetProperty("create_time", out JsonElement createTimeElement))
        {
            creationTimestamp = createTimeElement.GetInt64();
        }
    }

    var utcDateTime = DateTimeOffset.FromUnixTimeMilliseconds(creationTimestamp).DateTime;
    return ConvertToBeijingTime(utcDateTime);
}  
   
public static async Task<(decimal UsdtBalance, decimal TrxBalance)> GetBalancesAsync(string address)
{
    using var httpClient = new HttpClient();
    var response = await httpClient.GetAsync($"https://api.trongrid.io/v1/accounts/{address}");
    var json = await response.Content.ReadAsStringAsync();

    // 打印API响应
    //Console.WriteLine("API response for GetBalancesAsync:");
    //Console.WriteLine(json);

    var jsonDocument = JsonDocument.Parse(json);

    var usdtBalance = 0m;
    var trxBalance = 0m;

    if (jsonDocument.RootElement.TryGetProperty("data", out JsonElement dataElement) && dataElement.GetArrayLength() > 0)
    {
        var firstElement = dataElement[0];

        if (firstElement.TryGetProperty("balance", out JsonElement trxBalanceElement))
        {
            trxBalance = trxBalanceElement.GetDecimal() / 1_000_000;
        }

        if (firstElement.TryGetProperty("trc20", out JsonElement trc20Element))
        {
            foreach (var token in trc20Element.EnumerateArray())
            {
                foreach (var property in token.EnumerateObject())
                {
                    if (property.Name == "TR7NHqjeKQxGTCi8q8ZY4pL8otSzgjLj6t") //这是USDT合约地址 可以换成任意合约地址
                    {
                        usdtBalance = decimal.Parse(property.Value.GetString()) / 1_000_000;
                        break;
                    }
                }
            }
        }
    }

    return (usdtBalance, trxBalance);
}
public static async Task<(double remainingBandwidth, double totalBandwidth, int transactions, int transactionsIn, int transactionsOut)> GetBandwidthAsync(string address)
{
    string url = $"https://apilist.tronscanapi.com/api/accountv2?address={address}";
    using var httpClient = new HttpClient();
    var result = await httpClient.GetStringAsync(url);

    // 解析返回的 JSON 数据
    var jsonResult = JObject.Parse(result);

    // 检查是否为空的 JSON 对象
    if (!jsonResult.HasValues)
    {
        // 如果为空，直接返回默认值
        return (0, 0, 0, 0, 0);
    }

    double freeNetRemaining = jsonResult["bandwidth"]["freeNetRemaining"].ToObject<double>();
    double freeNetLimit = jsonResult["bandwidth"]["freeNetLimit"].ToObject<double>();
    int transactions = jsonResult["transactions"].ToObject<int>();
    int transactionsIn = jsonResult["transactions_in"].ToObject<int>();
    int transactionsOut = jsonResult["transactions_out"].ToObject<int>();

    return (freeNetRemaining, freeNetLimit, transactions, transactionsIn, transactionsOut);
}
public static async Task<string> GetLastFiveTransactionsAsync(string tronAddress)
{
    int limit = 5;
    string tokenId = "TR7NHqjeKQxGTCi8q8ZY4pL8otSzgjLj6t"; // USDT合约地址
    string url = $"https://api.trongrid.io/v1/accounts/{tronAddress}/transactions/trc20?only_confirmed=true&limit={limit}&token_id={tokenId}";

    using (var httpClient = new HttpClient())
    {
        HttpResponseMessage response = await httpClient.GetAsync(url);
        string jsonString = await response.Content.ReadAsStringAsync();
        JObject jsonResponse = JObject.Parse(jsonString);

        JArray transactions = (JArray)jsonResponse["data"];
        StringBuilder transactionTextBuilder = new StringBuilder();

        foreach (var transaction in transactions)
        {
            // 获取交易时间，并转换为北京时间
            long blockTimestamp = (long)transaction["block_timestamp"];
            DateTime transactionTimeUtc = DateTimeOffset.FromUnixTimeMilliseconds(blockTimestamp).DateTime;
            DateTime transactionTimeBeijing = TimeZoneInfo.ConvertTime(transactionTimeUtc, TimeZoneInfo.FindSystemTimeZoneById("China Standard Time"));

            // 判断交易是收入还是支出
            string type;
            string fromAddress = (string)transaction["from"];
            if (tronAddress.Equals(fromAddress, StringComparison.OrdinalIgnoreCase))
            {
                type = "支出";
            }
            else
            {
                type = "收入";
            }

            // 获取交易金额，并转换为USDT
            string value = (string)transaction["value"];
            decimal usdtAmount = decimal.Parse(value) / 1_000_000;

            // 构建交易文本
            transactionTextBuilder.AppendLine($"<code>{transactionTimeBeijing:yyyy-MM-dd HH:mm:ss}   {type}{usdtAmount:N2} USDT</code>");
        }

        return transactionTextBuilder.ToString();
    }
}
public static async Task HandleQueryCommandAsync(ITelegramBotClient botClient, Message message)
{
    var text = message.Text;
    var match = Regex.Match(text, @"(T[A-Za-z0-9]{33})"); // 验证Tron地址格式
    if (!match.Success)
    {
        await botClient.SendTextMessageAsync(message.Chat.Id, "查询地址错误，请重新输入");
        return;
    }

    var tronAddress = match.Groups[1].Value;

    // 回复用户正在查询
    await botClient.SendTextMessageAsync(message.Chat.Id, "正在查询，请稍后...");

    // 同时启动所有任务
    var getUsdtTransferTotalTask = GetUsdtTransferTotalAsync(tronAddress, "TGUJoKVqzT7igyuwPfzyQPtcMFHu76QyaC");
    var getBalancesTask = GetBalancesAsync(tronAddress);
    var getAccountCreationTimeTask = GetAccountCreationTimeAsync(tronAddress);
    var getLastTransactionTimeTask = GetLastTransactionTimeAsync(tronAddress);
    var getUsdtTotalIncomeTask = GetTotalIncomeAsync(tronAddress, false);
    var getBandwidthTask = GetBandwidthAsync(tronAddress);
    var getLastFiveTransactionsTask = GetLastFiveTransactionsAsync(tronAddress);


    // 等待所有任务完成
    await Task.WhenAll(getUsdtTransferTotalTask, getBalancesTask, getAccountCreationTimeTask, getLastTransactionTimeTask, getUsdtTotalIncomeTask, getBandwidthTask, getLastFiveTransactionsTask);


    // 处理结果
    var (usdtTotal, transferCount) = getUsdtTransferTotalTask.Result;
    var (usdtBalance, trxBalance) = getBalancesTask.Result;
    var creationTime = getAccountCreationTimeTask.Result;
    var lastTransactionTime = getLastTransactionTimeTask.Result;
    var usdtTotalIncome = getUsdtTotalIncomeTask.Result;
    var (remainingBandwidth, totalBandwidth, transactions, transactionsIn, transactionsOut) = getBandwidthTask.Result;
    string lastFiveTransactions = getLastFiveTransactionsTask.Result;
    
    // 判断是否所有返回的数据都是0
if (usdtTotal == 0 && transferCount == 0 && usdtBalance == 0 && trxBalance == 0 && 
    usdtTotalIncome == 0 && remainingBandwidth == 0 && totalBandwidth == 0 && 
    transactions == 0 && transactionsIn == 0 && transactionsOut == 0)
{
    // 如果都是0，那么添加提醒用户的语句
    string warningText = "查询地址有误或地址未激活，请激活后重试！";
    await botClient.SendTextMessageAsync(message.Chat.Id, warningText);
    return;
}

    // 根据USDT余额判断用户标签
    string userLabel;
    if (usdtBalance < 100_000)
    {
        userLabel = "普通用户";
    }
    else if (usdtBalance >= 100_000 && usdtBalance < 1_000_000)
    {
        userLabel = "土豪大佬";
    }
    else
    {
        userLabel = "远古巨鲸";
    }

    string resultText;

// 判断 TRX 余额是否小于100
if (trxBalance < 100)
{
    resultText =  $"查询地址：<code>{tronAddress}</code>\n" +
    $"注册时间：<b>{creationTime:yyyy-MM-dd HH:mm:ss}</b>\n" +
    $"最后活跃：<b>{lastTransactionTime:yyyy-MM-dd HH:mm:ss}</b>\n" +
    $"————————<b>资源</b>————————\n"+
    $"用户标签：<b>{userLabel}</b>\n" +
    $"交易笔数：<b>{transactions} （ ↑{transactionsOut} _ ↓{transactionsIn} ）</b>\n" +    
    $"USDT收入：<b>{usdtTotalIncome.ToString("N2")}</b>\n" +
    $"USDT余额：<b>{usdtBalance.ToString("N2")}</b>\n" +
    $"TRX余额：<b>{trxBalance.ToString("N2")}   ( TRX能量不足，请立即兑换！)</b>\n" +
    $"免费带宽：<b>{remainingBandwidth.ToString("N0")}/{totalBandwidth.ToString("N0")}</b>\n" +
    $"累计兑换：<b>{usdtTotal.ToString("N2")} USDT</b>\n" +
    $"兑换次数：<b>{transferCount.ToString("N0")} 次</b>\n" +
    $"——————<b>最近交易</b>——————\n" +
    $"{lastFiveTransactions}\n";
}
else
{
    resultText =  $"查询地址：<code>{tronAddress}</code>\n" +
    $"注册时间：<b>{creationTime:yyyy-MM-dd HH:mm:ss}</b>\n" +
    $"最后活跃：<b>{lastTransactionTime:yyyy-MM-dd HH:mm:ss}</b>\n" +
    $"————————<b>资源</b>————————\n"+
    $"用户标签：<b>{userLabel}</b>\n" +
    $"交易笔数：<b>{transactions} （ ↑{transactionsOut} _ ↓{transactionsIn} ）</b>\n" +    
    $"USDT收入：<b>{usdtTotalIncome.ToString("N2")}</b>\n" +
    $"USDT余额：<b>{usdtBalance.ToString("N2")}</b>\n" +
    $"TRX余额：<b>{trxBalance.ToString("N2")}</b>\n" +
    $"免费带宽：<b>{remainingBandwidth.ToString("N0")}/{totalBandwidth.ToString("N0")}</b>\n" +
    $"累计兑换：<b>{usdtTotal.ToString("N2")} USDT</b>\n" +
    $"兑换次数：<b>{transferCount.ToString("N0")} 次</b>\n" +
    $"——————<b>最近交易</b>——————\n" +
    $"{lastFiveTransactions}\n";
}


        // 创建内联键盘
    string botUsername = "yifanfubot"; // 替换为你的机器人的用户名
    string startParameter = ""; // 如果你希望机器人在被添加到群组时收到一个特定的消息，可以设置这个参数
    string shareLink = $"https://t.me/{botUsername}?startgroup={startParameter}";

    var inlineKeyboard = new InlineKeyboardMarkup(new[]
    {
        new [] // 第一行按钮
        {
            InlineKeyboardButton.WithUrl("详细信息", $"https://tronscan.org/#/address/{tronAddress}"), // 链接到Tron地址的详细信息
            InlineKeyboardButton.WithUrl("链上天眼", $"https://www.oklink.com/cn/trx/address/{tronAddress}"), // 链接到欧意地址的详细信息
            InlineKeyboardButton.WithUrl("进群使用", shareLink) // 添加机器人到群组的链接
        }
    });

    // 发送GIF和带按钮的文本
    string gifUrl = "https://i.postimg.cc/Jzrm1m9c/277574078-352558983556639-7702866525169266409-n.png";
    await botClient.SendPhotoAsync(
        chatId: message.Chat.Id,
        photo: new InputOnlineFile(gifUrl),
        caption: resultText, // 将文本作为图片说明发送
        parseMode: ParseMode.Html,
        replyMarkup: inlineKeyboard // 添加内联键盘
    );
}


public static async Task<(decimal UsdtTotal, int TransferCount)> GetUsdtTransferTotalAsync(string fromAddress, string toAddress)
{
    // 假设USDT合约地址为: TR7NHqjeKQxGTCi8q8ZY4pL8otSzgjLj6t
    var apiUrl = $"https://api.trongrid.io/v1/accounts/{fromAddress}/transactions/trc20?only_confirmed=true&limit=200&contract_address=TR7NHqjeKQxGTCi8q8ZY4pL8otSzgjLj6t";
    using var httpClient = new HttpClient();

    var usdtTotal = 0m;
    var transferCount = 0;  // 添加计数器用于统计转账次数
    string fingerprint = null;

    while (true)
    {
        var currentUrl = apiUrl + (fingerprint != null ? $"&fingerprint={fingerprint}" : "");
        var response = await httpClient.GetAsync(currentUrl);
        var json = await response.Content.ReadAsStringAsync();
        var jsonDocument = JsonDocument.Parse(json);

        if (!jsonDocument.RootElement.TryGetProperty("data", out JsonElement dataElement))
        {
            break;
        }

        foreach (var transactionElement in dataElement.EnumerateArray())
        {
            if (transactionElement.TryGetProperty("to", out JsonElement toElement) && toElement.GetString() == toAddress)
            {
                var value = transactionElement.GetProperty("value").GetString();
                usdtTotal += decimal.Parse(value) / 1_000_000; // 假设USDT有6位小数
                transferCount++;  // 当找到符合条件的转账时，计数器加一
            }
            fingerprint = transactionElement.GetProperty("transaction_id").GetString();
        }

        if (!jsonDocument.RootElement.TryGetProperty("has_next", out JsonElement hasNextElement) || !hasNextElement.GetBoolean())
        {
            break;
        }
    }

    return (usdtTotal, transferCount);
}

    
private static readonly Dictionary<string, string> CurrencyFullNames = new Dictionary<string, string>
{
    { "USD", "美元" },
    { "JPY", "日元" },
    { "GBP", "英镑" },
    { "EUR", "欧元" },
    { "AUD", "澳元" },
    { "KRW", "韩元" },
    { "THB", "泰铢" },
    { "VND", "越南盾" },
    { "INR", "卢比" },
    { "SGD", "新币" },
    { "KHR", "瑞尔" },
    { "PHP", "披索" },
    { "AED", "迪拉姆" },
};    
static bool TryGetRateByCurrencyCode(Dictionary<string, (decimal, string)> rates, string currencyCode, out KeyValuePair<string, (decimal, string)> rate)
{
    foreach (var entry in rates)
    {
        if (entry.Key.Contains(currencyCode))
        {
            rate = entry;
            return true;
        }
    }

    rate = default;
    return false;
}
static async Task<Dictionary<string, (decimal, string)>> GetCurrencyRatesAsync()
{
    var apiUrl = "https://api.exchangerate-api.com/v4/latest/CNY"; // CNY为人民币代号

    using var httpClient = new HttpClient();
    var response = await httpClient.GetAsync(apiUrl);
    var json = await response.Content.ReadAsStringAsync();

    using var jsonDocument = JsonDocument.Parse(json);
    if (!jsonDocument.RootElement.TryGetProperty("rates", out JsonElement ratesElement))
    {
        throw new Exception("Rates property not found");
    }

  var rates = new Dictionary<string, (decimal, string)>
  {
      { "美元 (USD)", (ratesElement.GetProperty("USD").GetDecimal(), "$") },
      { "日元 (JPY)", (ratesElement.GetProperty("JPY").GetDecimal(), "¥") },
      { "英镑 (GBP)", (ratesElement.GetProperty("GBP").GetDecimal(), "£") },
      { "欧元 (EUR)", (ratesElement.GetProperty("EUR").GetDecimal(), "€") },
      { "澳元 (AUD)", (ratesElement.GetProperty("AUD").GetDecimal(), "A$") },
      { "韩元 (KRW)", (ratesElement.GetProperty("KRW").GetDecimal(), "₩") },
      { "泰铢 (THB)", (ratesElement.GetProperty("THB").GetDecimal(), "฿") },
      { "越南盾 (VND)", (ratesElement.GetProperty("VND").GetDecimal(), "₫") },
      { "印度卢比 (INR)", (ratesElement.GetProperty("INR").GetDecimal(), "₹") },
      { "新加坡新币 (SGD)", (ratesElement.GetProperty("SGD").GetDecimal(), "S$") },
      { "柬埔寨瑞尔 (KHR)", (ratesElement.GetProperty("KHR").GetDecimal(), "៛") },
      { "菲律宾披索 (PHP)", (ratesElement.GetProperty("PHP").GetDecimal(), "₱") },
      { "迪拜迪拉姆 (AED)", (ratesElement.GetProperty("AED").GetDecimal(), "د.إ") }
  };

    return rates;
} 
static async Task<Message> SendCryptoPricesAsync(ITelegramBotClient botClient, Message message)
{
    var cryptoSymbols = new[] { "bitcoin", "ethereum", "binancecoin","bitget-token", "tether","ripple", "cardano", "dogecoin","shiba-inu", "solana", "litecoin", "chainlink", "the-open-network" };
    var (prices, changes) = await GetCryptoPricesAsync(cryptoSymbols);

    var cryptoNames = new Dictionary<string, string>
    {
        { "bitcoin", "比特币" },
        { "ethereum", "以太坊" },
        { "binancecoin", "币安币" },
        { "bitget-token", "BGB" },
        { "tether", "USDT泰达币" },
        { "ripple", "瑞波币" },
        { "cardano", "艾达币" },
        { "dogecoin", "狗狗币" },
        { "shiba-inu", "shib" },
        { "solana", "Sol" },
        { "litecoin", "莱特币" },
        { "chainlink", "link" },
        { "the-open-network", "电报币" }
    };

    var text = "<b>币圈热门币种实时价格及涨跌幅:</b>\n\n";

    for (int i = 0; i < cryptoSymbols.Length; i++)
    {
        var cryptoName = cryptoNames[cryptoSymbols[i]];
        var changeText = changes[i] < 0 ? $"<b>-</b>{Math.Abs(changes[i]):0.##}%" : $"<b>+</b>{changes[i]:0.##}%";
        text += $"<code>{cryptoName}: ${prices[i]:0.######}  {changeText}</code>\n";
        // 添加分隔符
        if (i < cryptoSymbols.Length - 1) // 防止在最后一行也添加分隔符
        {
            text += "———————————————\n";
        }
    }

// 创建包含两行，每行两个按钮的虚拟键盘
var keyboard = new ReplyKeyboardMarkup(new[]
{
    new [] // 第一行
    {
        new KeyboardButton("\U0001F4B0U兑TRX"),
        new KeyboardButton("\U0001F570实时汇率"),
        new KeyboardButton("\U0001F4B9汇率换算"),
    },   
    new [] // 第二行
    {
        new KeyboardButton("\U0001F4B8币圈行情"),
        new KeyboardButton("\U0001F310外汇助手"),
        new KeyboardButton("\u260E联系管理"),
    }    
});

keyboard.ResizeKeyboard = true; // 将键盘高度设置为最低
keyboard.OneTimeKeyboard = false; // 添加这一行，确保虚拟键盘在用户与其交互后保持可见

return await botClient.SendTextMessageAsync(chatId: message.Chat.Id,
                                            text: text, // 你可以将 'text' 替换为需要发送的文本
                                            parseMode: ParseMode.Html,
                                            replyMarkup: keyboard);
}

static async Task<(decimal[], decimal[])> GetCryptoPricesAsync(string[] symbols)
{
    var apiUrl = "https://api.coingecko.com/api/v3/simple/price?ids=" + string.Join(",", symbols) + "&vs_currencies=usd&include_market_cap=false&include_24hr_vol=false&include_24hr_change=true&include_last_updated_at=false";

    using var httpClient = new HttpClient();
    var response = await httpClient.GetAsync(apiUrl);
    var json = await response.Content.ReadAsStringAsync();
    var prices = new decimal[symbols.Length];
    var changes = new decimal[symbols.Length];

    using var jsonDocument = JsonDocument.Parse(json);
    for (int i = 0; i < symbols.Length; i++)
    {
        if (jsonDocument.RootElement.TryGetProperty(symbols[i], out JsonElement symbolElement) &&
            symbolElement.TryGetProperty("usd", out JsonElement priceElement) &&
            symbolElement.TryGetProperty("usd_24h_change", out JsonElement changeElement))
        {
            prices[i] = priceElement.GetDecimal();
            changes[i] = changeElement.GetDecimal();
        }
        else
        {
            Log.Logger.Warning($"Price or change property not found for symbol {symbols[i]}");
            prices[i] = -1m; // 使用 -1 表示无法获取价格
            changes[i] = -1m; // 使用 -1 表示无法获取涨跌幅
        }
    }

    return (prices, changes);
}
public static async Task<decimal> GetOkxPriceAsync(string baseCurrency, string quoteCurrency, string method)
{
    var client = new HttpClient();

    var url = $"https://www.okx.com/v3/c2c/tradingOrders/books?quoteCurrency={quoteCurrency}&baseCurrency={baseCurrency}&side=sell&paymentMethod={method}&userType=blockTrade&showTrade=false&receivingAds=false&showFollow=false&showAlreadyTraded=false&isAbleFilter=false&urlId=2";

    var response = await client.GetAsync(url);

    if (response.IsSuccessStatusCode)
    {
        var jsonString = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(jsonString);

        if (doc.RootElement.TryGetProperty("data", out var data) && data.TryGetProperty("sell", out var sell))
        {
            var sellArray = sell.EnumerateArray();

            if (sellArray.MoveNext())
            {
                var firstElement = sellArray.Current;

                if (firstElement.TryGetProperty("price", out var price))
                {
                    return decimal.Parse(price.GetString());
                }
            }
        }
    }

    throw new Exception("Could not get price from OKX API.");
}

static async Task SendAdvertisementOnce(ITelegramBotClient botClient, CancellationToken cancellationToken, IBaseRepository<TokenRate> rateRepository, decimal FeeRate, long chatId)
{    
        var rate = await rateRepository.Where(x => x.Currency == Currency.USDT && x.ConvertCurrency == Currency.TRX).FirstAsync(x => x.Rate);
        decimal usdtToTrx = 100m.USDT_To_TRX(rate, FeeRate, 0);
        // 获取比特币以太坊价格和涨跌幅
        var cryptoSymbols = new[] { "bitcoin", "ethereum" };
        var (prices, changes) = await GetCryptoPricesAsync(cryptoSymbols);
        var bitcoinPrice = prices[0];
        var ethereumPrice = prices[1];
        var bitcoinChange = changes[0];
        var ethereumChange = changes[1];
        // 获取美元汇率
        var currencyRates = await GetCurrencyRatesAsync();
        if (!currencyRates.TryGetValue("美元 (USD)", out var usdRateTuple)) 
        {
            Console.WriteLine("Could not find USD rate in response.");
            return; // 或者你可以选择继续，只是不显示美元汇率
        }
        var usdRate = 1 / usdRateTuple.Item1;
        decimal okxPrice = await GetOkxPriceAsync("USDT", "CNY", "all");
         // 获取24小时爆仓信息
        decimal h24TotalVolUsd = await GetH24TotalVolUsdAsync("https://open-api.coinglass.com/public/v2/liquidation_info?time_type=h24&symbol=all", "9e8ff0ca25f14355a015972f21f162de");
        (decimal btcLongRate, decimal btcShortRate) = await GetH24LongShortAsync("https://open-api.coinglass.com/public/v2/long_short?time_type=h24&symbol=BTC", "9e8ff0ca25f14355a015972f21f162de");
        (decimal ethLongRate, decimal ethShortRate) = await GetH1EthLongShortAsync("https://open-api.coinglass.com/public/v2/long_short?time_type=h1&symbol=ETH", "9e8ff0ca25f14355a015972f21f162de");
        
        string channelLink = "tg://resolve?domain=yifanfu"; // 使用 'tg://' 协议替换为你的频道链接
        string advertisementText = $"\U0001F4B9实时汇率：<b>100 USDT = {usdtToTrx:#.####} TRX</b>\n\n" +
            "机器人收款地址:\n (<b>点击自动复制</b>):<code>TGUJoKVqzT7igyuwPfzyQPtcMFHu76QyaC</code>\n\n" + //手动输入地址
            "\U0000267B进U即兑,全自动返TRX,10U起兑!\n" +
            "\U0000267B请勿使用交易所或中心化钱包转账!\n" +
            $"\U0000267B有任何问题,请私聊联系 <a href=\"{channelLink}\">机器人管理员</a>\n\n" +
            "<b>另代开TG会员</b>:\n\n" +
            "\u2708三月高级会员   24.99 u\n" +
            "\u2708六月高级会员   39.99 u\n" +
            "\u2708一年高级会员   70.99 u\n" +
            "(<b>需要开通会员请联系管理,切记不要转TRX兑换地址!!!</b>)\n"+
             $"————————<b>其它汇率</b>————————\n" +
             $"<b>\U0001F4B0 美元汇率参考 ≈ {usdRate:#.####}</b>\n" +
             $"<b>\U0001F4B0 USDT实时OTC价格 ≈ {okxPrice} CNY</b>\n\n" +
             $"<code>\U0001F4B8 全网24小时合约爆仓 ≈ {h24TotalVolUsd:#,0} USDT</code>\n" + // 添加新的一行
             $"<code>\U0001F4B8 比特币价格 ≈ {bitcoinPrice} USDT    {(bitcoinChange >= 0 ? "+" : "")}{bitcoinChange:0.##}% </code>\n" +
             $"<code>\U0001F4B8 以太坊价格 ≈ {ethereumPrice} USDT  {(ethereumChange >= 0 ? "+" : "")}{ethereumChange:0.##}% </code>\n" +
             $"<code>\U0001F4B8 比特币24小时合约：{btcLongRate:#.##}% 做多  {btcShortRate:#.##}% 做空</code>\n" + // 添加新的一行
             $"<code>\U0001F4B8 以太坊1小时合约： {ethLongRate:#.##}% 做多  {ethShortRate:#.##}% 做空</code>\n\n" ; // 添加新的一行
            
            
string botUsername = "yifanfubot"; // 替换为你的机器人的用户名
string startParameter = ""; // 如果你希望机器人在被添加到群组时收到一个特定的消息，可以设置这个参数
string shareLink = $"https://t.me/{botUsername}?startgroup={startParameter}";

// 创建 InlineKeyboardButton 并设置文本和回调数据
var visitButton1 = new InlineKeyboardButton("\U0000267B 进交流群")
{
    Url = "https://t.me/+b4NunT6Vwf0wZWI1" // 将此链接替换为你想要跳转的左侧链接
};

var visitButton2 = new InlineKeyboardButton("\u2B50 会员代开")
{
    Url = "https://t.me/Yifanfu" // 将此链接替换为你想要跳转的右侧链接
};

var shareToGroupButton = InlineKeyboardButton.WithUrl("\U0001F449 分享到群组 \U0001F448", shareLink);

// 创建 InlineKeyboardMarkup 并添加按钮
var inlineKeyboard = new InlineKeyboardMarkup(new[]
{
    new[] { visitButton1, visitButton2 }, // 第一行按钮
    new[] { shareToGroupButton } // 第二行按钮
});
    // 发送广告到指定的聊天
    await botClient.SendTextMessageAsync(
        chatId: chatId,
        text: advertisementText,
        parseMode: ParseMode.Html,
        replyMarkup: new InlineKeyboardMarkup(
            new[]
            {
                new[] { visitButton1, visitButton2 },
                new[] { shareToGroupButton }
            }),
        cancellationToken: cancellationToken);
}
public static class GroupManager
{
    private static HashSet<long> groupIds = new HashSet<long>();

    static GroupManager()
    {
        // 添加初始群组 ID
        groupIds.Add(-1001862069013);  // 用你的初始群组 ID 替换 
        //groupIds.Add(-1001885354740);  // 添加第二个初始群组 ID
    }

    public static IReadOnlyCollection<long> GroupIds => groupIds.ToList().AsReadOnly();

    public static void AddGroupId(long id)
    {
        groupIds.Add(id);
    }

    public static void RemoveGroupId(long id)  // 这是新添加的方法
    {
        groupIds.Remove(id);
    }
}

private static async Task<decimal> GetH24TotalVolUsdAsync(string apiUrl, string apiKey)
{
    using var httpClient = new HttpClient();
    httpClient.DefaultRequestHeaders.Add("accept", "application/json");
    httpClient.DefaultRequestHeaders.Add("coinglassSecret", apiKey);
    
    var response = await httpClient.GetAsync(apiUrl);
    response.EnsureSuccessStatusCode();
    
    var jsonString = await response.Content.ReadAsStringAsync();
    var jsonObject = JObject.Parse(jsonString);
    
    return jsonObject["data"]["h24TotalVolUsd"].ToObject<decimal>();
}
// 新方法获取比特币24小时长短期利率
private static async Task<(decimal longRate, decimal shortRate)> GetH24LongShortAsync(string apiUrl, string apiKey)
{
    using var httpClient = new HttpClient();
    httpClient.DefaultRequestHeaders.Add("accept", "application/json");
    httpClient.DefaultRequestHeaders.Add("coinglassSecret", apiKey);

    var response = await httpClient.GetAsync(apiUrl);
    response.EnsureSuccessStatusCode();

    var jsonString = await response.Content.ReadAsStringAsync();
    var jsonObject = JObject.Parse(jsonString);

    var data = jsonObject["data"].FirstOrDefault(d => d["symbol"].ToString() == "BTC");
    if (data == null)
    {
        throw new Exception("BTC 数据在响应中未找到。");
    }

    decimal longRate = data["longRate"].ToObject<decimal>();
    decimal shortRate = data["shortRate"].ToObject<decimal>();

    return (longRate, shortRate);
}
// 新方法获取以太坊1小时长短期利率
private static async Task<(decimal longRate, decimal shortRate)> GetH1EthLongShortAsync(string apiUrl, string apiKey)
{
    using var httpClient = new HttpClient();
    httpClient.DefaultRequestHeaders.Add("accept", "application/json");
    httpClient.DefaultRequestHeaders.Add("coinglassSecret", apiKey);

    var response = await httpClient.GetAsync(apiUrl);
    response.EnsureSuccessStatusCode();

    var jsonString = await response.Content.ReadAsStringAsync();
    var jsonObject = JObject.Parse(jsonString);

    var data = jsonObject["data"].FirstOrDefault(d => d["symbol"].ToString() == "ETH");
    if (data == null)
    {
        throw new Exception("ETH 数据在响应中未找到。");
    }

    decimal longRate = data["longRate"].ToObject<decimal>();
    decimal shortRate = data["shortRate"].ToObject<decimal>();

    return (longRate, shortRate);
}
static async Task SendAdvertisement(ITelegramBotClient botClient, CancellationToken cancellationToken, IBaseRepository<TokenRate> rateRepository, decimal FeeRate)
{
  

    while (!cancellationToken.IsCancellationRequested)
    {
        var rate = await rateRepository.Where(x => x.Currency == Currency.USDT && x.ConvertCurrency == Currency.TRX).FirstAsync(x => x.Rate);
        decimal usdtToTrx = 100m.USDT_To_TRX(rate, FeeRate, 0);
        // 获取比特币以太坊价格和涨跌幅
        var cryptoSymbols = new[] { "bitcoin", "ethereum" };
        var (prices, changes) = await GetCryptoPricesAsync(cryptoSymbols);
        var bitcoinPrice = prices[0];
        var ethereumPrice = prices[1];
        var bitcoinChange = changes[0];
        var ethereumChange = changes[1];
        // 获取美元汇率
        var currencyRates = await GetCurrencyRatesAsync();
        if (!currencyRates.TryGetValue("美元 (USD)", out var usdRateTuple)) 
        {
            Console.WriteLine("Could not find USD rate in response.");
            return; // 或者你可以选择继续，只是不显示美元汇率
        }
        var usdRate = 1 / usdRateTuple.Item1;
        decimal okxPrice = await GetOkxPriceAsync("USDT", "CNY", "all");
        
        string channelLink = "tg://resolve?domain=yifanfu"; // 使用 'tg://' 协议替换为你的频道链接
        string advertisementText = $"\U0001F4B9实时汇率：<b>100 USDT = {usdtToTrx:#.####} TRX</b>\n\n" +
            "机器人收款地址:\n (<b>点击自动复制</b>):<code>TGUJoKVqzT7igyuwPfzyQPtcMFHu76QyaC</code>\n\n\n" + //手动输入地址
            "\U0000267B进U即兑,全自动返TRX,10U起兑!\n" +
            "\U0000267B请勿使用交易所或中心化钱包转账!\n" +
            $"\U0000267B有任何问题,请私聊联系<a href=\"{channelLink}\">机器人管理员</a>\n\n" +
            "<b>另代开TG会员</b>:\n\n" +
            "\u2708三月高级会员   24.99 u\n" +
            "\u2708六月高级会员   39.99 u\n" +
            "\u2708一年高级会员   70.99 u\n" +
            "(<b>需要开通会员请联系管理,切记不要转TRX兑换地址!!!</b>)\n" +  
            $"————————<b>其它汇率</b>————————\n" +
            $"<b>\U0001F4B0 美元汇率参考 ≈ {usdRate:#.####} </b>\n" +
            $"<b>\U0001F4B0 USDT实时OTC价格 ≈ {okxPrice} CNY</b>\n" +
            $"<b>\U0001F4B0 比特币价格 ≈ {bitcoinPrice} USDT     {(bitcoinChange >= 0 ? "+" : "")}{bitcoinChange:0.##}% </b>\n" +
            $"<b>\U0001F4B0 以太坊价格 ≈ {ethereumPrice} USDT  {(ethereumChange >= 0 ? "+" : "")}{ethereumChange:0.##}% </b>\n" ;
            
            
string botUsername = "yifanfubot"; // 替换为你的机器人的用户名
string startParameter = ""; // 如果你希望机器人在被添加到群组时收到一个特定的消息，可以设置这个参数
string shareLink = $"https://t.me/{botUsername}?startgroup={startParameter}";

// 创建 InlineKeyboardButton 并设置文本和回调数据
var visitButton1 = new InlineKeyboardButton("\U0000267B 更多汇率")
{
    Url = "https://t.me/yifanfubot" // 将此链接替换为你想要跳转的左侧链接
};

var visitButton2 = new InlineKeyboardButton("\u2B50 会员代开")
{
    Url = "https://t.me/Yifanfu" // 将此链接替换为你想要跳转的右侧链接
};

var shareToGroupButton = InlineKeyboardButton.WithUrl("\U0001F449 分享到群组 \U0001F448", shareLink);

// 创建 InlineKeyboardMarkup 并添加按钮
var inlineKeyboard = new InlineKeyboardMarkup(new[]
{
    new[] { visitButton1, visitButton2 }, // 第一行按钮
    new[] { shareToGroupButton } // 第二行按钮
});

        try
        {
            // 用于存储已发送消息的字典
            var sentMessages = new Dictionary<long, Message>();
       
            // 遍历群组 ID 并发送广告消息
            var groupIds = GroupManager.GroupIds.ToList();
            foreach (var groupId in groupIds)
            {
                try
                {
                    Message sentMessage = await botClient.SendTextMessageAsync(groupId, advertisementText, parseMode: ParseMode.Html, replyMarkup: inlineKeyboard);
                    sentMessages[groupId] = sentMessage;
                }
                catch
                {
                    // 如果在尝试发送消息时出现错误，就从 groupIds 列表中移除这个群组
                    GroupManager.RemoveGroupId(groupId);
                    // 然后继续下一个群组，而不是停止整个任务
                    continue;
                }
            }

            // 等待10分钟
            await Task.Delay(TimeSpan.FromSeconds(600), cancellationToken);

            // 遍历已发送的消息并撤回
            foreach (var sentMessage in sentMessages)
            {
                await botClient.DeleteMessageAsync(sentMessage.Key, sentMessage.Value.MessageId);
            }

            // 等待5秒，再次发送广告
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
        }
        catch (Exception ex)
        {
            // 发送广告过程中出现异常
            Console.WriteLine("Error in advertisement loop: " + ex.Message);

            // 等10秒重启广告服务
            await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
        }
    }
}


    public static Task PollingErrorHandler(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        var ErrorMessage = exception switch
        {
            ApiRequestException apiRequestException => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
            _ => exception.ToString()
        };

        Log.Error(exception, ErrorMessage);
        return Task.CompletedTask;
    }
    /// <summary>
    /// 处理更新
    /// </summary>
    /// <param name="botClient"></param>
    /// <param name="update"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        var handler = update.Type switch
        {
            UpdateType.Message => BotOnMessageReceived(botClient, update.Message!),
            _ => UnknownUpdateHandlerAsync(botClient, update)
        };
    if (update.Type == UpdateType.Message)
    {
        var message = update.Message;

        // 在这里处理消息更新...
    }
        if (update.Type == UpdateType.Message)
    {
var message = update.Message;
if (message?.Text != null)
{
    // 检查输入文本是否为 Tron 地址
    var isTronAddress = Regex.IsMatch(message.Text, @"^(T[A-Za-z0-9]{33})$");

    if (isTronAddress)
    {
        await HandleQueryCommandAsync(botClient, message); // 当满足条件时，调用查询方法
    }
    else
    {
        // 在这里处理其他文本消息
    }
}
        // 检查消息文本是否以 "转" 开头
        if (message?.Text != null && message.Text.StartsWith("转"))
        {
            await HandleTranslateCommandAsync(botClient, message); // 在这里处理翻译命令
        }          
else
{
    var inputText = message.Text.Trim();

    if (!string.IsNullOrWhiteSpace(inputText))
    {
        // 检查输入文本是否以指定的命令开头、包含指定的关键词或为纯数字
        var containsKeywordsOrCommandsOrNumbers = Regex.IsMatch(inputText, @"^\/(start|yi|fan|fu|btc|usd|boss|cny)|联系管理|汇率换算|实时汇率|U兑TRX|币圈行情|外汇助手|^[\d\+\-\*/\s]+$");

        // 检查输入文本是否为 Tron 地址
        var isTronAddress = Regex.IsMatch(inputText, @"^(T[A-Za-z0-9]{33})$");

        if (!containsKeywordsOrCommandsOrNumbers && !isTronAddress)
        {
            // 检查输入文本是否包含任何非中文字符
            var containsNonChinese = Regex.IsMatch(inputText, @"[^\u4e00-\u9fa5]");

            if (containsNonChinese)
            {
                var targetLanguage = "zh-CN"; // 将目标语言设置为简体中文
                var translatedText = await GoogleTranslateFree.TranslateAsync(inputText, targetLanguage);
                await botClient.SendTextMessageAsync(message.Chat.Id, $"翻译结果：\n\n<code>{translatedText}</code>", parseMode: ParseMode.Html);
            }
        }
    }
}
    }
if(update.CallbackQuery != null && update.CallbackQuery.Data == "membershipOptions")
{
    var membershipKeyboard = new InlineKeyboardMarkup(new[]
    {
        new [] // 第一行按钮
        {
            InlineKeyboardButton.WithCallbackData("3个月会员    24.99 u", "3months"),
        },
        new [] // 第二行按钮
        {
            InlineKeyboardButton.WithCallbackData("6个月会员    39.99 u", "6months"),
        },
        new [] // 第三行按钮
        {
            InlineKeyboardButton.WithCallbackData("一年会员    70.99 u", "1year"),
        },
        new [] // 第四行按钮
        {
            InlineKeyboardButton.WithCallbackData("返回", "back"),
        }
    });

    await botClient.EditMessageTextAsync(
        chatId: update.CallbackQuery.Message.Chat.Id,
        messageId: update.CallbackQuery.Message.MessageId,
        text: "请选择会员期限：",
        replyMarkup: membershipKeyboard
    );
}
if (update.CallbackQuery != null &&
    (update.CallbackQuery.Data == "3months" || update.CallbackQuery.Data == "6months" || update.CallbackQuery.Data == "1year"))
{
    var inlineKeyboard = new InlineKeyboardMarkup(new[]
    {
        new [] // 第一行按钮
        {
            InlineKeyboardButton.WithUrl("支付成功", "https://t.me/yifanfu"),
            InlineKeyboardButton.WithCallbackData("重新选择", "cancelPayment"),
        }
    });

    await botClient.EditMessageTextAsync(
        chatId: update.CallbackQuery.Message.Chat.Id,
        messageId: update.CallbackQuery.Message.MessageId,
        text: "<b>收款地址</b>：<code>TJ4c6esQYEM7jn5s8DD5zk2DBYJTLHnFR3</code>",
        parseMode: ParseMode.Html,
        replyMarkup: inlineKeyboard
    );
}

if (update.CallbackQuery != null && update.CallbackQuery.Data == "cancelPayment")
{
    var membershipKeyboard = new InlineKeyboardMarkup(new[]
    {
        new [] // 第一行按钮
        {
            InlineKeyboardButton.WithCallbackData("3个月会员    24.99 u", "3months"),
        },
        new [] // 第二行按钮
        {
            InlineKeyboardButton.WithCallbackData("6个月会员    39.99 u", "6months"),
        },
        new [] // 第三行按钮
        {
            InlineKeyboardButton.WithCallbackData("一年会员    70.99 u", "1year"),
        },
        new [] // 第四行按钮
        {
            InlineKeyboardButton.WithCallbackData("返回", "back"),
        }
    });

    await botClient.EditMessageTextAsync(
        chatId: update.CallbackQuery.Message.Chat.Id,
        messageId: update.CallbackQuery.Message.MessageId,
        text: "请选择会员期限：",
        replyMarkup: membershipKeyboard
    );
}

if(update.CallbackQuery != null && update.CallbackQuery.Data == "back")
{
    // 返回上一级菜单
    var inlineKeyboard = new InlineKeyboardMarkup(new[]
    {
        new [] // 第一行按钮
        {
            //InlineKeyboardButton.WithUrl("管理员", "https://t.me/Yifanfu"),
            InlineKeyboardButton.WithUrl("\U0001F449 进群交流", "https://t.me/+b4NunT6Vwf0wZWI1"),
            InlineKeyboardButton.WithCallbackData("\u2B50 会员代开", "membershipOptions")
        }
    });

    await botClient.EditMessageTextAsync(
        chatId: update.CallbackQuery.Message.Chat.Id,
        messageId: update.CallbackQuery.Message.MessageId,
        text: "欢迎使用本机器人,请选择下方按钮操作：",
        replyMarkup: inlineKeyboard
    );
}        
    else if (update.Type == UpdateType.MyChatMember)
    {
        var chatMemberUpdated = update.MyChatMember;

        if (chatMemberUpdated.NewChatMember.Status == ChatMemberStatus.Member)
        {
            // 保存这个群组的ID
            GroupManager.AddGroupId(chatMemberUpdated.Chat.Id);
        }
        else if (chatMemberUpdated.NewChatMember.Status == ChatMemberStatus.Kicked || chatMemberUpdated.NewChatMember.Status == ChatMemberStatus.Left)  // 这是新添加的判断语句
        {
            // 如果机器人被踢出群组或者离开群组，我们移除这个群组的 ID
            GroupManager.RemoveGroupId(chatMemberUpdated.Chat.Id);
        }
    }

        try
        {
            await handler;
        }
        catch (Exception exception)
        {
            Log.Error(exception, "呜呜呜，机器人输错啦~");
            await PollingErrorHandler(botClient, exception, cancellationToken);
        }
    }
    /// <summary>
    /// 消息接收
    /// </summary>
    /// <param name="botClient"></param>
    /// <param name="message"></param>
    /// <returns></returns>
    private static async Task BotOnMessageReceived(ITelegramBotClient botClient, Message message)
    {
        Log.Information($"Receive message type: {message.Type}");
        if (message.Text is not { } messageText)
            return;
        var scope = serviceScopeFactory.CreateScope();
        var provider = scope.ServiceProvider;
        var _myTronConfig = provider.GetRequiredService<IOptionsSnapshot<MyTronConfig>>();
        try
        {
            await InsertOrUpdateUserAsync(botClient, message);
        }
        catch (Exception e)
        {
            Log.Logger.Error(e, "更新Telegram用户信息失败！");
        }
// 在处理回调的地方
if (messageText.StartsWith("/gk") || messageText.Contains("兑换记录"))
{
    var transactionRecords = await UpdateHandlers.GetTransactionRecordsAsync();
    await botClient.SendTextMessageAsync(message.Chat.Id, transactionRecords);
}        
        // 检查是否接收到了 /gg 消息，收到就启动广告
        if (messageText.StartsWith("/gg"))
        {
            var cancellationTokenSource = new CancellationTokenSource();
            var rateRepository = provider.GetRequiredService<IBaseRepository<TokenRate>>();
            _ = SendAdvertisement(botClient, cancellationTokenSource.Token, rateRepository, FeeRate);
        }
        
    // 检查是否接收到了 /cny 消息，收到就在当前聊天中发送广告
    else if (messageText.StartsWith("/cny"))
    {
        var cancellationTokenSource = new CancellationTokenSource();
        var rateRepository = provider.GetRequiredService<IBaseRepository<TokenRate>>();
        _ = SendAdvertisementOnce(botClient, cancellationTokenSource.Token, rateRepository, FeeRate, message.Chat.Id);
    }        
        // 添加这部分代码以处理 /crypto 和 /btc 指令
        if (messageText.StartsWith("\U0001F4B8币圈行情", StringComparison.OrdinalIgnoreCase) || messageText.StartsWith("/btc", StringComparison.OrdinalIgnoreCase))
        {
            await SendCryptoPricesAsync(botClient, message);
        }
else
{
    // 修改了正则表达式
    var calculatorPattern = @"^([-+]?[0-9]*\.?[0-9]+(\s*[-+*/]\s*[-+]?[0-9]*\.?[0-9]+)*)$";
    if (Regex.IsMatch(messageText, calculatorPattern))
    {
        // 原始问题备份
        var originalQuestion = messageText;

        // 使用 DataTable.Compute 方法计算表达式，该方法能够考虑运算符优先级
        var result = new DataTable().Compute(messageText, null);

        // 发送最终计算结果
        await botClient.SendTextMessageAsync(
            chatId: message.Chat.Id,
            // 使用 HTML 语法加粗结果，并附带原始问题
            text: $"<code>{System.Net.WebUtility.HtmlEncode(originalQuestion)}={result}</code>",
            parseMode: Telegram.Bot.Types.Enums.ParseMode.Html
        );
    }
    else
    {
        // 在这里处理非计算器相关的信息
    }
}        
if (message.Text == "\U0001F310外汇助手" || message.Text == "/usd") // 添加 /usd 条件
{
    var rates = await GetCurrencyRatesAsync();
    var text = "<b>100元人民币兑换其他国家货币</b>:\n\n";

    int count = 0;
    foreach (var rate in rates)
    {
        decimal convertedAmount = rate.Value.Item1 * 100;
        decimal exchangeRate = 1 / rate.Value.Item1;
        text += $"<code>{rate.Key}: {convertedAmount:0.#####} {rate.Value.Item2}  汇率≈{exchangeRate:0.######}</code>\n";

        // 如果还有更多的汇率条目，添加分隔符
        if (count < rates.Count - 1)
        {
            text += "——————————————————————\n";
        }

        count++;
    }

    string botUsername = "yifanfubot"; // 替换为你的机器人的用户名
    string startParameter = ""; // 如果你希望机器人在被添加到群组时收到一个特定的消息，可以设置这个参数
    string shareLink = $"https://t.me/{botUsername}?startgroup={startParameter}";

    // 创建一个虚拟键盘
    var inlineKeyboard = new InlineKeyboardMarkup(new[]
    {
        new [] // 第一行按钮
        {
            InlineKeyboardButton.WithUrl("分享到群组", shareLink)
        }
    });

    await botClient.SendTextMessageAsync(
        chatId: message.Chat.Id,
        text: text,
        parseMode: ParseMode.Html,
        disableWebPagePreview: true,
        replyMarkup: inlineKeyboard
    );
}
else
{
    var regex = new Regex(@"^(\d+)([a-zA-Z]{3}|[\u4e00-\u9fa5]+)$");
    var match = regex.Match(message.Text);
    if (match.Success)
    {
        int inputAmount = int.Parse(match.Groups[1].Value);
        string inputCurrency = match.Groups[2].Value;

        string inputCurrencyCode = null;
        if (CurrencyFullNames.ContainsValue(inputCurrency))
        {
            inputCurrencyCode = CurrencyFullNames.FirstOrDefault(x => x.Value == inputCurrency).Key;
        }
        else
        {
            inputCurrencyCode = inputCurrency.ToUpper();
        }

        var rates = await GetCurrencyRatesAsync();
        if (TryGetRateByCurrencyCode(rates, inputCurrencyCode, out var rate))
        {
            decimal convertedAmount = inputAmount / rate.Value.Item1;
            string currencyFullName = CurrencyFullNames.ContainsKey(inputCurrencyCode) ? CurrencyFullNames[inputCurrencyCode] : inputCurrencyCode;
            string text = $"<b>{inputAmount}{currencyFullName} ≈ {convertedAmount:0.##}元人民币</b>";
            await botClient.SendTextMessageAsync(chatId: message.Chat.Id,
                                                 text: text,
                                                 parseMode: ParseMode.Html);
        }
    }
}
        messageText = messageText.Replace($"@{BotUserName}", "");
        var action = messageText.Split(' ')[0] switch
        {
            "/start" => Start(botClient, message),
            "/fu" => Valuation(botClient, message),
            "\U0001F4B0U兑TRX" => ConvertCoinTRX(botClient, message), // 添加这一行
            "\U0001F570实时汇率" => PriceTRX(botClient, message), // 添加这一行
            "\U0001F4B9汇率换算" => Valuation(botClient, message), // 添加这一行
            "/yi" => ConvertCoinTRX(botClient, message),
            "/fan" => PriceTRX(botClient, message),
            "绑定波场地址" => BindAddress(botClient, message),
            "解绑波场地址" => UnBindAddress(botClient, message),
            "\u260E联系管理" => QueryAccount(botClient, message),
            "/boss" => QueryAccount(botClient, message), // 添加这一行
            "关闭键盘" => guanbi(botClient, message),
            _ => Usage(botClient, message)
        };
        async Task<decimal> GetMonthlyUSDTIncomeAsync(string ReciveAddress, string contractAddress)
{
    // 获取本月1号零点的时间戳
    var firstDayOfMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
    var firstDayOfMonthMidnight = new DateTimeOffset(firstDayOfMonth).ToUnixTimeSeconds();

    // 调用TronGrid API以获取交易记录
    var httpClient = new HttpClient();
    httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    string apiEndpoint = $"https://api.trongrid.io/v1/accounts/{ReciveAddress}/transactions/trc20?only_confirmed=true&only_to=true&min_timestamp={firstDayOfMonthMidnight * 1000}&contract_address={contractAddress}";
    var response = await httpClient.GetAsync(apiEndpoint);

    if (!response.IsSuccessStatusCode)
    {
        // 请求失败，返回0
        return 0;
    }

    var jsonResponse = await response.Content.ReadAsStringAsync();
    JObject transactions = JObject.Parse(jsonResponse);

    // 遍历交易记录并累计 USDT 收入
    decimal usdtIncome = 0;
    foreach (var tx in transactions["data"])
    {
        var rawAmount = (decimal)tx["value"];
        usdtIncome += rawAmount / 1_000_000L;
    }

    return usdtIncome;
}
        async Task<decimal> GetTodayUSDTIncomeAsync(string ReciveAddress, string contractAddress)
{
    // 获取今天零点的时间戳
    var todayMidnight = new DateTimeOffset(DateTime.Today).ToUnixTimeSeconds();

    // 调用TronGrid API以获取交易记录
    var httpClient = new HttpClient();
    httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    string apiEndpoint = $"https://api.trongrid.io/v1/accounts/{ReciveAddress}/transactions/trc20?only_confirmed=true&only_to=true&min_timestamp={todayMidnight * 1000}&contract_address={contractAddress}";
    var response = await httpClient.GetAsync(apiEndpoint);

    if (!response.IsSuccessStatusCode)
    {
        // 请求失败，返回0
        return 0;
    }

    var jsonResponse = await response.Content.ReadAsStringAsync();
    JObject transactions = JObject.Parse(jsonResponse);

    // 遍历交易记录并累计 USDT 收入
    decimal usdtIncome = 0;
    foreach (var tx in transactions["data"])
    {
        var rawAmount = (decimal)tx["value"];
        usdtIncome += rawAmount / 1_000_000L;
    }

    return usdtIncome;
}
        Message sentMessage = await action;
        async Task<Message> QueryAccount(ITelegramBotClient botClient, Message message)
        {
            if (message.From == null) return message;
            var from = message.From;
            var UserId = message.Chat.Id;

if (UserId != AdminUserId)
{
    var inlineKeyboard = new InlineKeyboardMarkup(new[]
    {
        new [] // 第一行按钮
        {
            //InlineKeyboardButton.WithUrl("管理员", "https://t.me/Yifanfu"),
            InlineKeyboardButton.WithUrl("\U0001F449 进群交流", "https://t.me/+b4NunT6Vwf0wZWI1"),
            InlineKeyboardButton.WithCallbackData("\u2B50 会员代开", "membershipOptions") // 新增按钮
        }
    });

    await botClient.SendTextMessageAsync(
        chatId: message.Chat.Id,
        text: "欢迎使用本机器人,请选择下方按钮操作：",
        replyMarkup: inlineKeyboard
    );

    return message;
}
            var _myTronConfig = provider.GetRequiredService<IOptionsSnapshot<MyTronConfig>>();
            var _wallet = provider.GetRequiredService<IWalletClient>();
            var _transactionClient = provider.GetRequiredService<ITransactionClient>();
            var _contractClientFactory = provider.GetRequiredService<IContractClientFactory>();
            var protocol = _wallet.GetProtocol();
            var Address = _myTronConfig.Value.Address;
            var addr = _wallet.ParseAddress(Address);

            var resource = await protocol.GetAccountResourceAsync(new TronNet.Protocol.Account
            {
                Address = addr
            });
            var account = await protocol.GetAccountAsync(new TronNet.Protocol.Account
            {
                Address = addr
            });
            var TRX = Convert.ToDecimal(account.Balance) / 1_000_000L;
            var contractAddress = _myTronConfig.Value.USDTContractAddress;
            var contractClient = _contractClientFactory.CreateClient(ContractProtocol.TRC20);
            //Log.Information("查询 USDT 余额...");
            var USDT = await contractClient.BalanceOfAsync(contractAddress, _wallet.GetAccount(_myTronConfig.Value.PrivateKey));
            //Log.Information($"查询 USDT 余额: 合约地址: {contractAddress}, 查询地址: {_wallet.GetAccount(_myTronConfig.Value.PrivateKey).Address}, 余额: {USDT}");
            
             // 调用新方法获取今日收入
            //Log.Information("查询今日收入...");
            string targetReciveAddress = "TGUJoKVqzT7igyuwPfzyQPtcMFHu76QyaC";//填写你想要监控收入的地址
            decimal todayIncome = await GetTodayUSDTIncomeAsync(targetReciveAddress, contractAddress);
            //Log.Information($"今日收入: {todayIncome}");
            
            // 调用新方法获取本月收入
            decimal monthlyIncome = await GetMonthlyUSDTIncomeAsync(targetReciveAddress, contractAddress);

            var msg = @$"当前账户资源如下：
地址： <code>{Address}</code>
TRX余额： <b>{TRX}</b>
USDT余额： <b>{USDT}</b>
免费带宽： <b>{resource.FreeNetLimit - resource.FreeNetUsed}/{resource.FreeNetLimit}</b>
质押带宽： <b>{resource.NetLimit - resource.NetUsed}/{resource.NetLimit}</b>
质押能量： <b>{resource.EnergyUsed}/{resource.EnergyLimit}</b>
————————————————————
带宽质押比：<b>100 TRX = {resource.TotalNetLimit * 1.0m / resource.TotalNetWeight * 100:0.000} 带宽</b>
能量质押比：<b>100 TRX = {resource.TotalEnergyLimit * 1.0m / resource.TotalEnergyWeight * 100:0.000} 能量</b>
————————————————————
今日承兑：<b>{todayIncome} USDT</b>
本月承兑：<b>{monthlyIncome} USDT</b>
";
            // 创建包含两行，每行两个按钮的虚拟键盘
            var keyboard = new ReplyKeyboardMarkup(new[]
            {
                new [] // 第一行
                {
                    new KeyboardButton("\U0001F4B0U兑TRX"),
                    new KeyboardButton("\U0001F570实时汇率"),
                    new KeyboardButton("\U0001F4B9汇率换算"),
                },   
                new [] // 第二行
                {
                    new KeyboardButton("\U0001F4B8币圈行情"),
                    new KeyboardButton("\U0001F310外汇助手"),
                    new KeyboardButton("\u260E联系管理"),
                }    
            });
            keyboard.ResizeKeyboard = true;           
            keyboard.OneTimeKeyboard = false;
            return await botClient.SendTextMessageAsync(chatId: message.Chat.Id,
                                                        text: msg,
                                                        parseMode: ParseMode.Html,
                                                        replyMarkup: keyboard);
        }
        async Task<Message> BindAddress(ITelegramBotClient botClient, Message message)
        {
            if (message.From == null) return message;
            if (message.Text is not { } messageText)
                return message;
            var address = messageText.Split(' ').Last();
            if (address.StartsWith("T") && address.Length == 34)
            {
                var from = message.From;
                var UserId = message.Chat.Id;

                var _bindRepository = provider.GetRequiredService<IBaseRepository<TokenBind>>();
                var bind = await _bindRepository.Where(x => x.UserId == UserId && x.Address == address).FirstAsync();
                if (bind == null)
                {
                    bind = new TokenBind();
                    bind.Currency = Currency.TRX;
                    bind.UserId = UserId;
                    bind.Address = address;
                    bind.UserName = $"@{from.Username}";
                    bind.FullName = $"{from.FirstName} {from.LastName}";
                    await _bindRepository.InsertAsync(bind);
                }
                else
                {
                    bind.Currency = Currency.TRX;
                    bind.UserId = UserId;
                    bind.Address = address;
                    bind.UserName = $"@{from.Username}";
                    bind.FullName = $"{from.FirstName} {from.LastName}";
                    await _bindRepository.UpdateAsync(bind);
                }
// 创建包含两行，每行两个按钮的虚拟键盘
var keyboard = new ReplyKeyboardMarkup(new[]
{
    new [] // 第一行
    {
        new KeyboardButton("\U0001F4B0U兑TRX"),
        new KeyboardButton("\U0001F570实时汇率"),
        new KeyboardButton("\U0001F4B9汇率换算"),
    },   
    new [] // 第二行
    {
        new KeyboardButton("\U0001F4B8币圈行情"),
        new KeyboardButton("\U0001F310外汇助手"),
        new KeyboardButton("\u260E联系管理"),
    }    
});
                keyboard.ResizeKeyboard = true; // 调整键盘高度
                keyboard.OneTimeKeyboard = false;
                return await botClient.SendTextMessageAsync(chatId: message.Chat.Id, text: @$"您已成功绑定<b>{address}</b>！
当我们向您的钱包转账时，您将收到通知！
如需解绑，请发送
<code>解绑波场地址 Txxxxxxx</code>(您的钱包地址)", parseMode: ParseMode.Html, replyMarkup: keyboard);
            }
            else
            {
// 创建包含两行，每行两个按钮的虚拟键盘
var keyboard = new ReplyKeyboardMarkup(new[]
{
    new [] // 第一行
    {
        new KeyboardButton("\U0001F4B0U兑TRX"),
        new KeyboardButton("\U0001F570实时汇率"),
        new KeyboardButton("\U0001F4B9汇率换算"),
    },   
    new [] // 第二行
    {
        new KeyboardButton("\U0001F4B8币圈行情"),
        new KeyboardButton("\U0001F310外汇助手"),
        new KeyboardButton("\u260E联系管理"),
    }    
});
                keyboard.ResizeKeyboard = true; // 调整键盘高度
                keyboard.OneTimeKeyboard = false;
                return await botClient.SendTextMessageAsync(chatId: message.Chat.Id, text: $"您输入的波场地址<b>{address}</b>有误！", parseMode: ParseMode.Html, replyMarkup: keyboard);
            }
        }
        async Task<Message> UnBindAddress(ITelegramBotClient botClient, Message message)
        {
            if (message.From == null) return message;
            if (message.Text is not { } messageText)
                return message;
            var address = messageText.Split(' ').Last();

            var _bindRepository = provider.GetRequiredService<IBaseRepository<TokenBind>>();
            var from = message.From;
            var UserId = message.Chat.Id;
            var bind = await _bindRepository.Where(x => x.UserId == UserId && x.Address == address).FirstAsync();
            if (bind != null)
            {
                await _bindRepository.DeleteAsync(bind);
            }
// 创建包含两行，每行两个按钮的虚拟键盘
var keyboard = new ReplyKeyboardMarkup(new[]
{
    new [] // 第一行
    {
        new KeyboardButton("\U0001F4B0U兑TRX"),
        new KeyboardButton("\U0001F570实时汇率"),
        new KeyboardButton("\U0001F4B9汇率换算"),
    },   
    new [] // 第二行
    {
        new KeyboardButton("\U0001F4B8币圈行情"),
        new KeyboardButton("\U0001F310外汇助手"),
        new KeyboardButton("\u260E联系管理"),
    }    
});
                keyboard.ResizeKeyboard = true; // 调整键盘高度
                keyboard.OneTimeKeyboard = false;
            return await botClient.SendTextMessageAsync(chatId: message.Chat.Id, text: $"您已成功解绑<b>{address}</b>！", parseMode: ParseMode.Html, replyMarkup: keyboard);

        }
        async Task<Message> ConvertCoinTRX(ITelegramBotClient botClient, Message message)
        {
            if (message.From == null) return message;
            var from = message.From;
            var UserId = message.From.Id;
            var _rateRepository = provider.GetRequiredService<IBaseRepository<TokenRate>>();
            var rate = await _rateRepository.Where(x => x.Currency == Currency.USDT && x.ConvertCurrency == Currency.TRX).FirstAsync(x => x.Rate);

            var addressArray = configuration.GetSection("Address:USDT-TRC20").Get<string[]>();
            if (addressArray.Length == 0)
            {

                return await botClient.SendTextMessageAsync(chatId: message.Chat.Id,
                                                            text: $"管理员还未配置收款地址，请联系管理员： {AdminUserUrl}",
                                                            parseMode: ParseMode.Html,
                                                            replyMarkup: new ReplyKeyboardRemove());
            }
            var ReciveAddress = addressArray[UserId % addressArray.Length];
            var msg = @$"<b>请向此地址转入任意金额，机器人自动回款TRX</b>
            
机器人收款地址： <code>{ReciveAddress}</code>

手续费说明：手续费用于支付转账所消耗的资源，及机器人运行成本。
当前手续费：<b>兑换金额的 1% 或 1 USDT，取大者</b>

示例：
<code>转入金额：<b>10 USDT</b>
手续费：<b>1 USDT</b>
实时汇率：<b>1 USDT = {1m.USDT_To_TRX(rate, FeeRate, 0):#.####} TRX</b>
获得TRX：<b>(10 - 1) * {1m.USDT_To_TRX(rate, FeeRate, 0):#.####} = {10m.USDT_To_TRX(rate, FeeRate, USDTFeeRate):0.00} TRX</b></code>

注意：<b>只支持{MinUSDT} USDT以上的金额兑换。</b>

转帐前，推荐您使用以下命令来接收入账通知
<code>绑定波场地址 Txxxxxxx</code>(您的钱包地址)
";
            if (USDTFeeRate == 0)
            {
                msg = @$"<b>请向此地址转入任意金额，机器人自动回款TRX</b>
机器人收款地址:(<b>↓点击自动复制↓</b>):<code>{ReciveAddress}</code>

示例：
<code>转入金额：<b>100 USDT</b>
实时汇率：<b>1 USDT = {1m.USDT_To_TRX(rate, FeeRate, 0):#.####} TRX</b>
获得TRX：<b>100 * {1m.USDT_To_TRX(rate, FeeRate, 0):#.####} = {100m.USDT_To_TRX(rate, FeeRate, USDTFeeRate):0.00} TRX</b></code>

注意：<b>只支持{MinUSDT} USDT以上的金额兑换。</b>
<b>只限钱包转账，收到u自动返回TRX，如需兑换到其它地址请联系管理!</b>

转帐前，推荐您使用以下命令来接收入账通知
<code>绑定波场地址 Txxxxxxx</code>(您的钱包地址)


<b>限时福利：</b>
<code>单笔兑换：<b>666 USDT或以上金额,电报会员免费送!!!</b></code>
<code>单笔兑换：<b>666 USDT或以上金额,电报会员免费送!!!</b></code>
<code>单笔兑换：<b>666 USDT或以上金额,电报会员免费送!!!</b></code>
";
            }
// 创建包含两行，每行两个按钮的虚拟键盘
var keyboard = new ReplyKeyboardMarkup(new[]
{
    new [] // 第一行
    {
        new KeyboardButton("\U0001F4B0U兑TRX"),
        new KeyboardButton("\U0001F570实时汇率"),
        new KeyboardButton("\U0001F4B9汇率换算"),
    },   
    new [] // 第二行
    {
        new KeyboardButton("\U0001F4B8币圈行情"),
        new KeyboardButton("\U0001F310外汇助手"),
        new KeyboardButton("\u260E联系管理"),
    }    
});
            keyboard.ResizeKeyboard = true; // 将键盘高度设置为最低
            keyboard.OneTimeKeyboard = false; // 添加这一行，确保虚拟键盘在用户与其交互后保持可见            
            return await botClient.SendTextMessageAsync(chatId: message.Chat.Id,
                                                        text: msg,
                                                        parseMode: ParseMode.Html,
                                                        replyMarkup: keyboard);
        }
        async Task<Message> PriceTRX(ITelegramBotClient botClient, Message message)
        {
            if (message.From == null) return message;
            var from = message.From;
            var UserId = message.From.Id;
            var _rateRepository = provider.GetRequiredService<IBaseRepository<TokenRate>>();
            var rate = await _rateRepository.Where(x => x.Currency == Currency.USDT && x.ConvertCurrency == Currency.TRX).FirstAsync(x => x.Rate);

            var addressArray = configuration.GetSection("Address:USDT-TRC20").Get<string[]>();
            var ReciveAddress = addressArray.Length == 0 ? "未配置" : addressArray[UserId % addressArray.Length];
            var msg = @$"<b>实时价目表</b>

实时汇率：<b>100 USDT = {100m.USDT_To_TRX(rate, FeeRate, 0):#.####} TRX</b>
————————————————————<code>
   5 USDT = {(5m * 1).USDT_To_TRX(rate, FeeRate, USDTFeeRate):0.00} TRX
  10 USDT = {(5m * 2).USDT_To_TRX(rate, FeeRate, USDTFeeRate):0.00} TRX
  20 USDT = {(5m * 4).USDT_To_TRX(rate, FeeRate, USDTFeeRate):0.00} TRX
  50 USDT = {(5m * 10).USDT_To_TRX(rate, FeeRate, USDTFeeRate):0.00} TRX
 100 USDT = {(5m * 20).USDT_To_TRX(rate, FeeRate, USDTFeeRate):0.00} TRX
 500 USDT = {(5m * 100).USDT_To_TRX(rate, FeeRate, USDTFeeRate):0.00} TRX
1000 USDT = {(5m * 200).USDT_To_TRX(rate, FeeRate, USDTFeeRate):0.00} TRX
</code>

机器人收款地址:(<b>↓点击自动复制↓</b>):<code>{ReciveAddress}</code>

注意：<b>暂时只支持{MinUSDT} USDT以上(不含{MinUSDT} USDT)的金额兑换，若转入{MinUSDT} USDT及以下金额，将无法退还！！！</b>

转帐前，推荐您使用以下命令来接收入账通知
<code>绑定波场地址 Txxxxxxx</code>(您的钱包地址)


<b>限时福利：</b>
<code>单笔兑换：<b>666 USDT或以上金额,电报会员免费送!!!</b></code>
<code>单笔兑换：<b>666 USDT或以上金额,电报会员免费送!!!</b></code>
<code>单笔兑换：<b>666 USDT或以上金额,电报会员免费送!!!</b></code>
";
// 创建包含两行，每行两个按钮的虚拟键盘
var keyboard = new ReplyKeyboardMarkup(new[]
{
    new [] // 第一行
    {
        new KeyboardButton("\U0001F4B0U兑TRX"),
        new KeyboardButton("\U0001F570实时汇率"),
        new KeyboardButton("\U0001F4B9汇率换算"),
    },   
    new [] // 第二行
    {
        new KeyboardButton("\U0001F4B8币圈行情"),
        new KeyboardButton("\U0001F310外汇助手"),
        new KeyboardButton("\u260E联系管理"),
    }    
});
            keyboard.ResizeKeyboard = true; // 将键盘高度设置为最低
            keyboard.OneTimeKeyboard = false; // 添加这一行，确保虚拟键盘在用户与其交互后保持可见

            return await botClient.SendTextMessageAsync(chatId: message.Chat.Id,
                                                        text: msg,
                                                        parseMode: ParseMode.Html,
                                                        replyMarkup: keyboard);
        }
        //通用回复
        static async Task<Message> Start(ITelegramBotClient botClient, Message message)
        {
            // 先发送GIF
            string gifUrl = "https://i.postimg.cc/0QKYJ0Cb/333.gif"; // 替换为您的GIF URL
            await botClient.SendAnimationAsync(
                chatId: message.Chat.Id,
                animation: gifUrl
            );
            long userId = message.From.Id; // 更改为 long 类型
            string username = message.From.FirstName;
            string botUsername = "yifanfubot"; // 替换为你的机器人的用户名
            string startParameter = ""; // 如果你希望机器人在被添加到群组时收到一个特定的消息，可以设置这个参数
            string shareLink = $"https://t.me/{botUsername}?startgroup={startParameter}";
            string groupFunctionText = $"<a href=\"{shareLink}\">把机器人拉进群聊解锁更多功能</a>";
            
            //1带ID  2不带
            //string usage = @$"<b>{username}</b> (ID:<code>{userId}</code>) 你好，欢迎使用TRX自助兑换机器人！
            string usage = @$"<b>{username}</b> 你好，欢迎使用TRX自助兑换机器人！
            
使用方法：
   点击菜单 选择&#x1F4B0;U兑TRX
   转账USDT到指定地址，即可秒回TRX
   
   {groupFunctionText}
   
";
// 创建包含两行，每行两个按钮的虚拟键盘
var keyboard = new ReplyKeyboardMarkup(new[]
{
    new [] // 第一行
    {
        new KeyboardButton("\U0001F4B0U兑TRX"),
        new KeyboardButton("\U0001F570实时汇率"),
        new KeyboardButton("\U0001F4B9汇率换算"),
    },   
    new [] // 第二行
    {
        new KeyboardButton("\U0001F4B8币圈行情"),
        new KeyboardButton("\U0001F310外汇助手"),
        new KeyboardButton("\u260E联系管理"),
    }    
});
            keyboard.ResizeKeyboard = true; // 将键盘高度设置为最低
            keyboard.OneTimeKeyboard = false; // 添加这一行，确保虚拟键盘在用户与其交互后保持可见
            return await botClient.SendTextMessageAsync(chatId: message.Chat.Id,
                                                        text: usage,
                                                        parseMode: ParseMode.Html,
                                                        disableWebPagePreview: true,
                                                        replyMarkup: keyboard);
        }
        //估价
        static async Task<Message> Valuation(ITelegramBotClient botClient, Message message)
        {
            string usage = @$"如需换算请直接发送<b>金额+币种</b>
如发送： <code>10 USDT</code>
回复：<b>10 USDT = xxx TRX</b>

如发送： <code>100 TRX</code>
回复：<b>100 TRX = xxx USDT</b>

查外汇直接发送<b>金额+货币或代码</b>
如发送： <code>100美元</code>或<code>100usd</code>
回复：<b>100美元 ≈  xxx 元人民币</b>

数字计算<b>直接对话框发送</b>
如发送：1+1
回复： <code>1+1=2</code>
注：<b>群内计算需要@机器人或设置机器人为管理</b>

";
    
// 创建包含两行，每行两个按钮的虚拟键盘
var keyboard = new ReplyKeyboardMarkup(new[]
{
    new [] // 第一行
    {
        new KeyboardButton("\U0001F4B0U兑TRX"),
        new KeyboardButton("\U0001F570实时汇率"),
        new KeyboardButton("\U0001F4B9汇率换算"),
    },   
    new [] // 第二行
    {
        new KeyboardButton("\U0001F4B8币圈行情"),
        new KeyboardButton("\U0001F310外汇助手"),
        new KeyboardButton("\u260E联系管理"),
    }    
});
            keyboard.ResizeKeyboard = true; // 将键盘高度设置为最低
            keyboard.OneTimeKeyboard = false; // 添加这一行，确保虚拟键盘在用户与其交互后保持可见  
            return await botClient.SendTextMessageAsync(chatId: message.Chat.Id,
                                                        text: usage,
                                                        parseMode: ParseMode.Html,
                                                        replyMarkup: keyboard);
        }
        //关闭虚拟键盘
        static async Task<Message> guanbi(ITelegramBotClient botClient, Message message)
        {
            string usage = @$"键盘已关闭
";

            return await botClient.SendTextMessageAsync(chatId: message.Chat.Id,
                                                        text: usage,
                                                        parseMode: ParseMode.Html,
                                                        replyMarkup: new ReplyKeyboardRemove());
        }
        //通用回复
        static async Task<Message> Usage(ITelegramBotClient botClient, Message message)
        {
            var text = (message.Text ?? "").ToUpper().Trim();
            if (text.EndsWith("USDT") && decimal.TryParse(text.Replace("USDT", ""), out var usdtPrice))
            {
                return await ValuationAction(botClient, message, usdtPrice, Currency.USDT, Currency.TRX);
            }
            if (text.EndsWith("TRX") && decimal.TryParse(text.Replace("TRX", ""), out var trxPrice))
            {
                return await ValuationAction(botClient, message, trxPrice, Currency.TRX, Currency.USDT);
            }
            return message;
        }
        static async Task<Message> ValuationAction(ITelegramBotClient botClient, Message message, decimal price, Currency fromCurrency, Currency toCurrency)
        {
            var scope = serviceScopeFactory.CreateScope();
            var provider = scope.ServiceProvider;
            var _rateRepository = provider.GetRequiredService<IBaseRepository<TokenRate>>();
            var rate = await _rateRepository.Where(x => x.Currency == Currency.USDT && x.ConvertCurrency == Currency.TRX).FirstAsync(x => x.Rate);
            var msg = $"<b>{price} {fromCurrency} = {price} {fromCurrency}</b>";
            if (fromCurrency == Currency.USDT && toCurrency == Currency.TRX)
            {
                if (price < MinUSDT)
                {
                    msg = $"仅支持大于{MinUSDT} USDT 的兑换";
                }
                else
                {
                    var toPrice = price.USDT_To_TRX(rate, FeeRate, USDTFeeRate);
                    msg = $"<b>{price} {fromCurrency} = {toPrice} {toCurrency}</b>";
                }
            }
            if (fromCurrency == Currency.TRX && toCurrency == Currency.USDT)
            {
                var toPrice = price.TRX_To_USDT(rate, FeeRate, USDTFeeRate);
                if (toPrice < MinUSDT)
                {
                    msg = $"仅支持大于{MinUSDT} USDT 的兑换";
                }
                else
                {
                    msg = $"<b>{price} {fromCurrency} = {toPrice} {toCurrency}</b>";
                }
            }
// 创建包含两行，每行两个按钮的虚拟键盘
var keyboard = new ReplyKeyboardMarkup(new[]
{
    new [] // 第一行
    {
        new KeyboardButton("\U0001F4B0U兑TRX"),
        new KeyboardButton("\U0001F570实时汇率"),
        new KeyboardButton("\U0001F4B9汇率换算"),
    },   
    new [] // 第二行
    {
        new KeyboardButton("\U0001F4B8币圈行情"),
        new KeyboardButton("\U0001F310外汇助手"),
        new KeyboardButton("\u260E联系管理"),
    }    
});
            keyboard.ResizeKeyboard = true; // 将键盘高度设置为最低
            keyboard.OneTimeKeyboard = false; // 添加这一行，确保虚拟键盘在用户与其交互后保持可见

            return await botClient.SendTextMessageAsync(chatId: message.Chat.Id,
                                                        text: msg,
                                                        parseMode: ParseMode.Html,
                                                        replyMarkup: keyboard);
        }
        async Task InsertOrUpdateUserAsync(ITelegramBotClient botClient, Message message)
        {
            if (message.From == null) return;
            var curd = provider.GetRequiredService<IBaseRepository<Users>>();
            var from = message.From;
            var UserId = message.Chat.Id;
            Log.Information("{user}: {message}", $"{from.FirstName} {from.LastName}", message.Text);

            var user = await curd.Where(x => x.UserId == UserId).FirstAsync();
            if (user == null)
            {
                user = new Users
                {
                    UserId = UserId,
                    UserName = from.Username,
                    FirstName = from.FirstName,
                    LastName = from.LastName
                };
                await curd.InsertAsync(user);
                return;
            }
            user.UserId = UserId;
            user.UserName = from.Username;
            user.FirstName = from.FirstName;
            user.LastName = from.LastName;
            await curd.UpdateAsync(user);
        }
    }

    private static Task UnknownUpdateHandlerAsync(ITelegramBotClient botClient, Update update)
    {
        Log.Information($"Unknown update type: {update.Type}");
        return Task.CompletedTask;
    }
}
