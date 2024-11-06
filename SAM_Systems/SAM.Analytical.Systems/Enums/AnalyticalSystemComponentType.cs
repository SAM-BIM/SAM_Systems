﻿using System.ComponentModel;

namespace SAM.Analytical.Systems
{
    [Description("Analytical System Component Type")]
    public enum AnalyticalSystemComponentType
    {
        [Description("Undefined")] Undefined,
        [Description("System Heating Coil")] SystemHeatingCoil,
        [Description("System Cooling Coil")] SystemCoolingCoil,
        [Description("System Fan")] SystemFan,
        [Description("System Boiler")] SystemBoiler,
        [Description("System Air Junction")] SystemAirJunction,
        [Description("System Chilled Beam")] SystemChilledBeam,
        [Description("System Damper")] SystemDamper,
        [Description("System Desiccant Wheel")] SystemDesiccantWheel,
        [Description("System Exchanger")] SystemExchanger,
        [Description("System Direct Evaporative Cooler")] SystemDirectEvaporativeCooler,
        [Description("System DX Coil")] SystemDXCoil,
        [Description("System Economiser")] SystemEconomiser,
        [Description("System Humidifier")] SystemHumidifier,
        [Description("System Mixing Box")] SystemMixingBox,
        [Description("System Spray Humidifier")] SystemSprayHumidifier,
        [Description("System Steam Humidifier")] SystemSteamHumidifier,
        [Description("System Space")] SystemSpace,
        [Description("System Difference Controller")] SystemDifferenceController,
        [Description("System Normal Controller")] SystemNormalController,
        [Description("System Outdoor Controller")] SystemOutdoorController,
        [Description("System Passthrough Controller")] SystemPassthroughController,
        [Description("System Maximal Logical Controller")] SystemMaxLogicalController,
        [Description("System Minimal Logical Controller")] SystemMinLogicalController,
        [Description("System Not Logical Controller")] SystemNotLogicalController,
        [Description("System If Logical Controller")] SystemIfLogicalController,
        [Description("System Signal Logical Controller")] SystemSigLogicalController,
        [Description("System Pump")] SystemPump,
        [Description("System Air Source Chiller")] SystemAirSourceChiller,
        [Description("System Water Source Chiller")] SystemWaterSourceChiller,
        [Description("System Multi Chiller")] SystemMultiChiller,
        [Description("System Absorption Chiller")] SystemAbsorptionChiller,
        [Description("System Air Source Direct Absorption Chiller")] SystemAirSourceDirectAbsorptionChiller,
        [Description("System Water Source Absorption Chiller")] SystemWaterSourceAbsorptionChiller,
        [Description("System Water Source Direct Absorption Chiller")] SystemWaterSourceDirectAbsorptionChiller,
        [Description("System Ice Storage Chiller")] SystemIceStorageChiller,
        [Description("System Water Source Ice Storage Chiller")] SystemWaterSourceIceStorageChiller,
        [Description("System Liquid Junction")] SystemLiquidJunction,
        [Description("System Air Source Heat Pump")] SystemAirSourceHeatPump,
        [Description("System Water Source Heat Pump")] SystemWaterSourceHeatPump,
        [Description("System Water To Water Heat Pump")] SystemWaterToWaterHeatPump,
        [Description("System Tank")] SystemTank,
        [Description("System Cooling Collection")] CoolingSystemCollection,
        [Description("System Heating Collection")] HeatingSystemCollection,
        [Description("System Domestic Hot Water Collection")] DomesticHotWaterSystemCollection,
        [Description("System Electrical Collection")] ElectricalSystemCollection,
        [Description("System Fuel Collection")] FuelSystemCollection,
        [Description("System Refrigerant Collection")] RefrigerantSystemCollection,
        [Description("System Pipe Loss Component")] SystemPipeLossComponent,
        [Description("System Liquid Exchanger")] SystemLiquidExchanger,
        [Description("System Cooling Tower")] SystemCoolingTower,
        [Description("System Dry Cooler")] SystemDryCooler,
        [Description("System Vertical Borehole")] SystemVerticalBorehole,
        [Description("System Slinky Coil")] SystemSlinkyCoil,
        [Description("System CHP")] SystemCHP,
        [Description("System Surface Water Exchanger")] SystemSurfaceWaterExchanger,
        [Description("System Horizontal Exchanger")] SystemHorizontalExchanger,
        [Description("System Solar Panel")] SystemSolarPanel,
        [Description("System Photovoltaic Panel")] SystemPhotovoltaicPanel,
        [Description("System Valve")] SystemValve
    }
}