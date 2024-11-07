﻿using System.ComponentModel;

namespace SAM.Analytical.Systems
{
    [Description("Water Source Ice Source Chiller Data Type")]
    public enum WaterSourceIceStorageChillerDataType
    {
        [Description("Flow Rate")] FlowRate = 1,
        [Description("Temperature")] Temperature = 2,
        [Description("Demand")] Demand = 3,
        [Description("Consumption")] Consumption = 4,
        [Description("Compressor")] Compressor = 5,
        [Description("Fan Load")] FanLoad = 6,
    }
}
