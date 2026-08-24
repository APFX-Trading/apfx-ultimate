namespace cAlgo.Robots;

public readonly record struct TelegramReportingConfiguration(
    bool Enabled,
    string BotToken,
    string ChatId,
    int UtcOffsetMinutes,
    bool EnableDaily,
    int DailyHour,
    int DailyMinute,
    bool EnableWeekly,
    DayOfWeek WeeklyDay,
    int WeeklyHour,
    int WeeklyMinute,
    bool EnableMonthly,
    int MonthlyDay,
    int MonthlyHour,
    int MonthlyMinute,
    bool AlertClosedTrades,
    string SymbolFilter,
    string LabelFilter,
    string ReportTitle);
