// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors
using System.Collections.Generic;
using SAM.Core;
using SAM.Core.Systems;

namespace SAM.Analytical.Systems.Mollier
{
    public static partial class Create
    {
        internal static void InjectLiquidSystems(SystemPlantRoom systemPlantRoom, out List<ConversionDiagnostic> diagnostics)
        {
            diagnostics = new List<ConversionDiagnostic>();
            if (systemPlantRoom == null)
            {
                return;
            }

            List<SystemHeatingCoil> heatingCoils = systemPlantRoom.GetSystemComponents<SystemHeatingCoil>();
            if (heatingCoils != null && heatingCoils.Count > 0)
            {
                InjectLiquidLoop(systemPlantRoom, heatingCoils, "Heating Hot Water",
                    new SystemBoiler("Boiler"), diagnostics);
            }

            List<SystemCoolingCoil> coolingCoils = systemPlantRoom.GetSystemComponents<SystemCoolingCoil>();
            if (coolingCoils != null && coolingCoils.Count > 0)
            {
                InjectLiquidLoop(systemPlantRoom, coolingCoils, "Chilled Water",
                    new SystemAirSourceChiller("Chiller"), diagnostics);
            }
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
                    diagnostics.Add(new ConversionDiagnostic(DiagnosticSeverity.Warning,
                        DiagnosticCodes.LiquidInjectionFailed,
                        "Could not create plant equipment for " + liquidSystemName + ".", null));
                    return;
                }

                LiquidSystem liquidSystem = new LiquidSystem(liquidSystemName);
                systemPlantRoom.Add(liquidSystem);

                systemPlantRoom.Add(plantEquipment);
                systemPlantRoom.Connect(liquidSystem, plantEquipment);

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

                    systemPlantRoom.Connect(liquidSystem, coil);

                    int prevOut = UnconnectedLiquidIndex(systemPlantRoom, previous, systemType, SAM.Core.Direction.Out);
                    int coilIn = UnconnectedLiquidIndex(systemPlantRoom, coil, systemType, SAM.Core.Direction.In);
                    if (prevOut != -1 && coilIn != -1)
                    {
                        systemPlantRoom.Connect(previous, coil, out _, liquidSystem, prevOut, coilIn);
                    }

                    previous = coil;
                }

                int lastOut = UnconnectedLiquidIndex(systemPlantRoom, previous, systemType, SAM.Core.Direction.Out);
                int equipIn = UnconnectedLiquidIndex(systemPlantRoom, plantEquipment, systemType, SAM.Core.Direction.In);
                if (lastOut != -1 && equipIn != -1)
                {
                    systemPlantRoom.Connect(previous, plantEquipment, out _, liquidSystem, lastOut, equipIn);
                }
            }
            catch
            {
                diagnostics.Add(new ConversionDiagnostic(DiagnosticSeverity.Warning,
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
