﻿namespace SimpleBacktestLib.Internal.Logic;

internal static class MarginAccess
{
    /// <summary>
    /// Open a long or short position at the current candle with 
    /// user settings and an optionally custom trade input.
    /// </summary>
    /// <param name="state"></param>
    /// <param name="tradeInput"></param>
    /// <returns>Id of the created margin position</returns>
    internal static int ExecuteOpenPosition(TradeType marginType, BacktestState state, TradeInput tradeInput)
    {
        // Generate the short
        decimal candlePrice = state.GetCurrentCandlePrice();
        int selectedMarginId = state.NextMarginId;
        MarginPosition pos = MarginPosition.GeneratePosition(
            marginType,
            candlePrice,
            tradeInput,
            state.BaseBalance,
            state.QuoteBalance,
            state.SetupConfig.MarginLeverageRatio,
            state.SetupConfig.MarginLiquidationRatio
        );
        
        pos.CandleOpenIndex = state.CurrentCandleIndex;
        
        // Log it
        string positionTypeStr = marginType == TradeType.MarginLong ? "long" : "short";
        LogHandler.AddLogEntry(
            state, $"Opening margin {positionTypeStr} (id: {selectedMarginId}) at price {candlePrice} borrowing {pos.BorrowedAmount}",
            candleIndex: state.CurrentCandleIndex
        );

        // Add it to dictionary and prepare next position id
        state.MarginTrades.Add(selectedMarginId, pos);
        state.NextMarginId++;
        return selectedMarginId;
    }


    /// <summary>
    /// Close a margin position and take profit
    /// </summary>
    /// <param name="state"></param>
    /// <param name="positionId"></param>
    internal static void ExecuteClosePosition(BacktestState state, int positionId)
    {
        // Get position value
        (bool isLiquid, decimal newBase, decimal newQuote) = MarginLogic.CalculateUnrealizedBalances(
            state.MarginTrades[positionId],
            state.GetCurrentCandlePrice(),
            state.BaseBalance,
            state.QuoteBalance);

        // Log it
        decimal baseProfit = newBase - state.BaseBalance;
        decimal quoteProfit = newQuote - state.QuoteBalance;
        string liquidness = isLiquid ? "liquid" : "illiquid";
        LogHandler.AddLogEntry(state,
            $"Closed {liquidness} margin position {positionId} with profit {baseProfit:F3} Base Asset and {quoteProfit:F3} Quote Asset",
            state.CurrentCandleIndex, isLiquid ? LogLevel.Information : LogLevel.Warning);

        // Close the position and take profit/loss
        state.MarginTrades[positionId].MarkAsClosed();
        state.MarginTrades[positionId].QuoteProfit = quoteProfit;
        state.MarginTrades[positionId].BaseProfit = baseProfit;
        state.MarginTrades[positionId].CandleCloseIndex = state.CurrentCandleIndex;
        state.BaseBalance = newBase;
        state.QuoteBalance = newQuote;
    }

}
