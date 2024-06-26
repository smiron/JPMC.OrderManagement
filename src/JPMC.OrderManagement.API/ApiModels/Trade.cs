﻿using JPMC.OrderManagement.Common.DataModels;

namespace JPMC.OrderManagement.API.ApiModels;

internal class Trade
{
    public string Symbol { get; set; } = null!;

    public Side Side { get; set; }

    public int Amount { get; set; }
}
