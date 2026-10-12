# APFX Ultimate

APFX Ultimate is a modular cTrader cBot with chart-symbol trading controls, basket grid management, live account status, and a chart panel.

## Structure

- `APFX Ultimate.cs` — composition root: cTrader lifecycle hooks, parameters, platform adapters, and module registration.
- `Core/` — stable module contracts, lifecycle runner, host context, and shared snapshots.
- `Modules/GridTrading/` — grid basket lifecycle, pending-order management, drawdown protection, and target/lock chart lines.
- `Modules/Indicators/` — internal ATR, ADX/+DI/-DI, and multi-timeframe RSI presentation.
- `Modules/ChartPanel/` — panel composition, layout, position, and trading controls.
- `Modules/OpenPositionStatistics/` — live position, margin, grid target, trail, and drawdown information.
- `Modules/AccountStatistics/` — closed-trade performance summaries.
- `Modules/CalendarNews/` — authenticated AP Forex Trading calendar client, response validation, state, and panel presentation.

## Module conventions

Each feature owns one focused responsibility and implements `IRobotModule` only when it needs lifecycle callbacks. Modules receive platform operations through `RobotContext`; they do not access cTrader host objects directly. Startup runs in registration order, ticks run in registration order, and shutdown runs in reverse order. A module failure is logged without stopping the remaining modules.

`MarketSnapshot` is the shared read model for the active chart symbol. It keeps market prices, account values, and internal indicator values together without making the Grid module own indicator data.

## Performance behavior

- Grid management and trading safety checks run on every tick.
- Live panel values and indicators refresh at most four times per second.
- Closed-trade history is reloaded only when its count changes or a new server day begins. Performance rows, including observed equity drawdown, refresh every 15 seconds. The timer also samples equity and refreshes account values when no chart-symbol tick arrives.

## Drawdown behavior

Performance **DD (%)** shows the largest monetary peak-to-trough drop found in either the closed-deal balance curve or the equity observations for that period. Its percentage divides that loss by the peak associated with that loss; a later recovery does not change the denominator. It is a historical maximum, so recovery does not reset it. P/L and W/L use closed deals, so nonzero DD with zero closed trades can reflect floating losses.

Today, Week (Monday start), and Month use server calendar boundaries. The 3/6/12-month windows start at midnight on the corresponding date that many months ago. **Performance From** clips every window, including intraday cutoffs. Daily equity summaries retain chronological peaks, lows, and maximum losses, allowing longer periods to include daily and cross-day drops across restarts. Intraday cutoff tracking also persists separately.

Equity observations cover ticks, timer samples, and position-close refreshes while this bot instance runs. Closed-trade history cannot reconstruct earlier floating-equity peaks/troughs or moves missed while the bot was stopped. DD uses raw values from the trading account running the cBot: deposits, withdrawals, and other symbols' trading on that same account can affect it.

Daily DD, intraday cutoff DD, and grid baseline storage keys include broker name, trading account number, and live/demo status. Account identity changes reset the cached trade history, equity curves, and active grid state before calculations resume. Returning to an account restores only that account's saved records. Unscoped records from older versions are ignored because their owner cannot be verified; observed DD starts fresh after this upgrade, while the account's closed-trade history remains available. Storage reads/writes explicitly use the cBot instance scope, so separate instances do not share observed equity history or fall back to type/device records. Keys are hashed to comply with cTrader's 50-character alphanumeric limit ([cTrader LocalStorage reference](https://help.ctrader.com/ctrader-algo/references/Localstorage/LocalStorage/)). cTrader saves local storage automatically every minute; this bot also flushes at day rollover and shutdown ([cTrader local storage](https://help.ctrader.com/ctrader-algo/guides/local-storage/)).

Grid **DD Left / Limit** is a separate safety control. Its baseline is fixed when a manual basket is adopted, and restored across restarts. Used DD is `max(0, starting equity - current account equity)`. The limit is the smaller of the enabled percentage and cash limits; zero disables that individual limit. Reaching the limit closes both directions on the chart symbol and cancels the active basket's pending orders. Other symbols influence account equity but their positions are not closed by this module. Failed closes/cancellations are retried on subsequent ticks/timers; the baseline is cleared and the grid rearms only after the closing positions and basket orders are gone. This is a software stop checked on ticks/timers; execution delays and slippage can take the realised loss beyond the configured amount.

The drawdown regression harness runs with `dotnet run --project tests/DrawdownPersistence -p:AlgoPublish=false`.

## Chart panel

When **Use ATR Distance** and **Use ATR Scaling** are enabled, grid distance and basket profit target both use `ATR pips × 0.10`, with a maximum of 30 account-currency units and no 15 minimum. For example, 64 ATR pips gives a 6.40 distance and target, while 400 ATR pips gives 30 for both. This monetary ATR rule is independent of order volume; volume and the symbol's pip value determine the equivalent price distance. Target previews and the panel use that same capped target. The two ATR switches remain independent; fixed profit targets use the configured Target Profit, and fixed distance retains its 15–30 range.

When the grid is enabled and the chart symbol has no open positions, short pink lines to the right of the current candle preview the initial profit target for each enabled direction. They follow the current bid/ask, normalized order volume, and target settings, including ATR scaling. TP previews estimate gross profit before commission and slippage; the active basket target uses actual net profit. Previews disappear when a position opens or the bot stops.

The preview regression harness runs with `dotnet run --project tests/GridPreview -p:AlgoPublish=false` from the repository root. This disables the cTrader installation copy while testing.

Use **Graph > Show Trade Preview** in bot settings to set the initial visibility, or **TP target preview** in the panel's Graph section to change it immediately. Panel choices persist in instance-local storage across restarts. Changing the bot setting overrides the previously saved panel choice on the next start. The toggle only controls previews; grid trading and active basket target lines are unaffected.

The panel can be placed in any of nine chart positions and supports a configurable font size. Its two-column layout contains live indicators and account/grid status on the left, calendar news on the right, and full-width quick actions below.

Calendar News reads `https://apforextrading.com/api/calendar/next` asynchronously. Set **Calendar News → API Key** to an enabled key from the news system; the key is sent only as the required `x-api-key` request header. The bot refreshes the configured lookahead window every five minutes by default, validates the response before display, and shows a clear panel status if the key is absent, rejected, or the service is unavailable.

On restart, recent calendar results (up to six hours old) and Treasury yields (up to 36 hours old) appear from instance-local storage while fresh requests run. Cached data is labelled as such, and expired cache entries are ignored. The three Treasury series are requested concurrently. Startup and slow module timings are printed in the cTrader log.
