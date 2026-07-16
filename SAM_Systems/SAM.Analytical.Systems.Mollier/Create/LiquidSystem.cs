// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors
using System.Collections.Generic;
using SAM.Core;
using SAM.Core.Systems;

namespace SAM.Analytical.Systems.Mollier
{
    public static partial class Create
    {
        // Default energy source names mirror the MVRE.json reference project; SAM_Tas resolves fuel sources by
        // these names via SystemObjectParameter.EnergySourceName when exporting to TPD.
        internal const string NaturalGasEnergySourceName = "Natural Gas";
        internal const string GridElectricityEnergySourceName = "Grid Supplied Electricity";

        internal static void InjectLiquidSystems(SystemPlantRoom systemPlantRoom, out List<ConversionDiagnostic> diagnostics)
        {
            InjectLiquidSystems(systemPlantRoom, out _, out diagnostics);
        }

        /// <summary>
        /// Injects a closed heating and/or cooling liquid loop for every heating/cooling coil in the plant room,
        /// and returns the default <see cref="SystemEnergySource"/>s (gas for the boiler, grid electricity for
        /// the chiller) that the caller must add to the owning <see cref="SystemEnergyCentre"/> - energy sources
        /// live at the energy-centre level, not in the plant room.
        /// </summary>
        internal static void InjectLiquidSystems(SystemPlantRoom systemPlantRoom, out List<SystemEnergySource> systemEnergySources, out List<ConversionDiagnostic> diagnostics)
        {
            systemEnergySources = new List<SystemEnergySource>();
            diagnostics = new List<ConversionDiagnostic>();
            if (systemPlantRoom == null)
            {
                return;
            }

            List<SystemHeatingCoil> heatingCoils = systemPlantRoom.GetSystemComponents<SystemHeatingCoil>();
            if (heatingCoils != null && heatingCoils.Count > 0)
            {
                SystemBoiler systemBoiler = new SystemBoiler("Boiler");
                // Parameters must be stamped before Add: SystemPlantRoom.Add stores a clone.
                systemBoiler.SetValue(SystemObjectParameter.EnergySourceName, NaturalGasEnergySourceName);

                InjectLiquidLoop(systemPlantRoom, heatingCoils, "Heating Hot Water", systemBoiler, diagnostics);
                systemEnergySources.Add(NaturalGasEnergySource());
            }

            List<SystemCoolingCoil> coolingCoils = systemPlantRoom.GetSystemComponents<SystemCoolingCoil>();
            if (coolingCoils != null && coolingCoils.Count > 0)
            {
                SystemAirSourceChiller systemAirSourceChiller = new SystemAirSourceChiller("Chiller");
                systemAirSourceChiller.SetValue(SystemObjectParameter.EnergySourceName, GridElectricityEnergySourceName);
                systemAirSourceChiller.SetValue(SystemObjectParameter.FanEnergySourceName, GridElectricityEnergySourceName);

                InjectLiquidLoop(systemPlantRoom, coolingCoils, "Chilled Water", systemAirSourceChiller, diagnostics);
                systemEnergySources.Add(GridElectricityEnergySource());
            }
        }

        /// <summary>
        /// Default gas tariff mirroring the MVRE.json reference: CO2 0.216 kg/kWh, peak cost 0.05, PEF 0.
        /// </summary>
        private static SystemEnergySource NaturalGasEnergySource()
        {
            SystemEnergySource result = new SystemEnergySource(NaturalGasEnergySourceName);
            result.CO2Factor = new IndexedDoubles();
            result.CO2Factor.Add(0, 0.216);
            result.PeakCost = new IndexedDoubles();
            result.PeakCost.Add(0, 0.05);
            result.PrimaryEnergyFactor = new IndexedDoubles();
            result.PrimaryEnergyFactor.Add(0, 0.0);
            return result;
        }

        /// <summary>
        /// Default grid-electricity tariff mirroring the MVRE.json reference: CO2 0.519 kg/kWh, peak cost 0.13,
        /// PEF 0. An <see cref="ElectricalEnergySource"/> so SAM_Tas exports it as an electrical FuelSource.
        /// </summary>
        private static SystemEnergySource GridElectricityEnergySource()
        {
            ElectricalEnergySource result = new ElectricalEnergySource(GridElectricityEnergySourceName);
            result.CO2Factor = new IndexedDoubles();
            result.CO2Factor.Add(0, 0.519);
            result.PeakCost = new IndexedDoubles();
            result.PeakCost.Add(0, 0.13);
            result.PrimaryEnergyFactor = new IndexedDoubles();
            result.PrimaryEnergyFactor.Add(0, 0.0);
            return result;
        }

        private static void InjectLiquidLoop(SystemPlantRoom systemPlantRoom,
            System.Collections.IList coils,
            string liquidSystemName,
            ISystemComponent plantEquipment,
            List<ConversionDiagnostic> diagnostics)
        {
            try
            {
                if (plantEquipment == null)
                {
                    diagnostics.Add(new ConversionDiagnostic(DiagnosticSeverity.Error,
                        DiagnosticCodes.LiquidInjectionFailed,
                        "Could not create plant equipment for " + liquidSystemName + ".", null));
                    return;
                }

                LiquidSystem liquidSystem = new LiquidSystem(liquidSystemName);
                systemPlantRoom.Add(liquidSystem);

                systemPlantRoom.Add(plantEquipment);
                if (!systemPlantRoom.Connect(liquidSystem, plantEquipment))
                {
                    diagnostics.Add(new ConversionDiagnostic(DiagnosticSeverity.Error,
                        DiagnosticCodes.LiquidInjectionFailed,
                        $"Liquid loop for '{liquidSystemName}' left partially wired: could not relate liquid system to '{ComponentName(plantEquipment)}'.", null));
                }

                SystemType systemType = new SystemType(typeof(LiquidSystem));

                ISystemComponent previous = plantEquipment;
                int count = coils.Count;
                for (int i = 0; i < count; i++)
                {
                    ISystemComponent coil = coils[i] as ISystemComponent;
                    if (coil == null)
                    {
                        continue;
                    }

                    if (!systemPlantRoom.Connect(liquidSystem, coil))
                    {
                        diagnostics.Add(new ConversionDiagnostic(DiagnosticSeverity.Error,
                            DiagnosticCodes.LiquidInjectionFailed,
                            $"Liquid loop for '{liquidSystemName}' left partially wired: could not relate liquid system to '{ComponentName(coil)}'.", null));
                    }

                    int prevOut = UnconnectedLiquidIndex(systemPlantRoom, previous, systemType, SAM.Core.Direction.Out);
                    int coilIn = UnconnectedLiquidIndex(systemPlantRoom, coil, systemType, SAM.Core.Direction.In);
                    bool coilConnected = prevOut != -1 && coilIn != -1 && systemPlantRoom.Connect(previous, coil, out _, liquidSystem, prevOut, coilIn);
                    if (!coilConnected)
                    {
                        diagnostics.Add(new ConversionDiagnostic(DiagnosticSeverity.Error,
                            DiagnosticCodes.LiquidInjectionFailed,
                            $"Liquid loop for '{liquidSystemName}' left partially wired: could not connect '{ComponentName(previous)}' to '{ComponentName(coil)}'.", null));
                    }

                    previous = coil;
                }

                int lastOut = UnconnectedLiquidIndex(systemPlantRoom, previous, systemType, SAM.Core.Direction.Out);
                int equipIn = UnconnectedLiquidIndex(systemPlantRoom, plantEquipment, systemType, SAM.Core.Direction.In);
                bool equipmentConnected = lastOut != -1 && equipIn != -1 && systemPlantRoom.Connect(previous, plantEquipment, out _, liquidSystem, lastOut, equipIn);
                if (!equipmentConnected)
                {
                    diagnostics.Add(new ConversionDiagnostic(DiagnosticSeverity.Error,
                        DiagnosticCodes.LiquidInjectionFailed,
                        $"Liquid loop for '{liquidSystemName}' left partially wired: could not connect '{ComponentName(previous)}' to '{ComponentName(plantEquipment)}'.", null));
                }
            }
            catch
            {
                diagnostics.Add(new ConversionDiagnostic(DiagnosticSeverity.Error,
                    DiagnosticCodes.LiquidInjectionFailed,
                    "Could not inject " + liquidSystemName + " liquid system.", null));
            }
        }

        private static int UnconnectedLiquidIndex(SystemPlantRoom systemPlantRoom, ISystemComponent systemComponent,
            SystemType systemType, SAM.Core.Direction direction)
        {
            List<int> indexes = systemPlantRoom?.Indexes(systemComponent, systemType, ConnectorStatus.Unconnected, direction);
            return indexes != null && indexes.Count > 0 ? indexes[0] : -1;
        }
    }
}
