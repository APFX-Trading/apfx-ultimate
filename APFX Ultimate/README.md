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
- Performance history is recalculated only when a closed-trade count changes or a new server day begins; otherwise it is checked on a lightweight 15-second cadence.

## Chart panel

The panel can be placed in any of nine chart positions and supports a configurable font size. Its two-column layout contains live indicators and account/grid status on the left, calendar news on the right, and full-width quick actions below.

Calendar News reads `https://apforextrading.com/api/calendar/next` asynchronously. Set **Calendar News → API Key** to an enabled key from the news system; the key is sent only as the required `x-api-key` request header. The bot refreshes the configured lookahead window every five minutes by default, validates the response before display, and shows a clear panel status if the key is absent, rejected, or the service is unavailable.
