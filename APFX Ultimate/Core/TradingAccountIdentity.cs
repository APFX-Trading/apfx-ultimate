namespace cAlgo.Robots;

/// <summary>Identifies the platform account independently of balances and chart symbols.</summary>
public readonly record struct TradingAccountIdentity(string BrokerName, long AccountNumber, bool IsLive)
{
    public static TradingAccountIdentity From(AccountSummary account) => new(account.BrokerName, account.AccountNumber, account.IsLive);

    public string DrawdownStorageKey(string logicalKey)
    {
        // JSON preserves field boundaries; hashing keeps keys within cTrader's
        // 50-character, alphanumeric-only limit even for long broker/symbol names.
        var input = JsonSerializer.Serialize(new { BrokerName, AccountNumber, IsLive, Key = logicalKey });
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(input));
        return "DD3" + Convert.ToHexString(hash)[..46];
    }
}
