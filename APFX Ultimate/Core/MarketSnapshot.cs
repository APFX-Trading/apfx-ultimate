namespace cAlgo.Robots;

/// <summary>
/// Current chart-symbol market, account, and internally calculated indicator values.
/// </summary>
public readonly record struct MarketSnapshot(
    string SymbolName,
    double Bid,
    double Ask,
    double PipSize,
    double PipValue,
    double Equity,
    double FreeMargin,
    double AtrPips,
    double Adx,
    double DiPlus,
    double DiMinus,
    double RsiM1,
    double RsiM5,
    double RsiM15,
    double RsiH1,
    double RsiH4,
    double BollingerTop,
    double BollingerBottom,
    double FastEma,
    double SlowEma);
