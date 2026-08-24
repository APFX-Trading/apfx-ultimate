namespace cAlgo.Robots;

/// <summary>Account details required by reporting modules, without exposing the robot host.</summary>
public readonly record struct AccountSummary(
    double Balance,
    double Equity,
    double Margin,
    double FreeMargin,
    string Currency,
    string BrokerName,
    long AccountNumber,
    bool IsLive);
