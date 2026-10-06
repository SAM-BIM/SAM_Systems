// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core;
using SAM.Core.Systems;
using SAM.Geometry.Planar;
using System;
using System.Collections.Generic;

namespace SAM.Analytical.Systems
{
    public static partial class Create
    {
        /// <summary>
        /// PR5B (SAM#111): materialises one unit's aggregate cooling module as an internal recirculation
        /// branch inside the unit's OWN air system, onto its OWN existing rooms:
        /// <code>
        /// room -> return damper -> cooling coil -> recirculation fan -> supply damper -> SAME room
        /// </code>
        /// one return and one supply damper per room, all rooms' returns meeting at the coil inlet and all
        /// rooms' supplies leaving the fan outlet.
        /// <para>
        /// <b>What it never does.</b> It adds no room, no air system and no outdoor-air path; it writes no
        /// ventilation design airflow and no capacity; its connections carry no design flow rate, so no
        /// ventilation reconciliation counts them. Nothing here reads a product or a name.
        /// </para>
        /// <para>
        /// <b>Why each component is what it is</b> (licensed TAS evidence, SAM#111 PR5B):
        /// </para>
        /// <list type="bullet">
        /// <item><description>The coil's cooling setpoint is the published supply-air temperature, looked up
        /// by an equality table over outdoor dry bulb, entering dry bulb and entering airflow, with
        /// extrapolation OFF - held at the table's edges, never extended beyond what was published. The
        /// coil sits on the recirculation branch because the table's entering temperature is the room /
        /// mixed-return air.</description></item>
        /// <item><description>Its heating duty is zero and its heating setpoint is the cooling-enable
        /// temperature: the coil does nothing while its inlet is below that temperature, and it never
        /// heats. It is a cooling gate, not a heating control.</description></item>
        /// <item><description>The recirculation fan is a copy of the unit's supply fan - so it carries a
        /// pressure and an efficiency TAS accepts - at the validated ceiling, variable speed, with no heat
        /// gain. It is a pressure-flow carrier for the internal loop, not a claim that the product holds a
        /// further fan.</description></item>
        /// <item><description>Each room's two dampers carry the room's floor-area share of the ceiling as an
        /// absolute value. The controller that turns the law into flow acts on these dampers; it is grounded
        /// by the downstream conversion, which owns the native sensor position.</description></item>
        /// </list>
        /// <para><b>Linear</b> in the unit's rooms: one pass, dictionary lookups only.</para>
        /// </summary>
        /// <returns>False where a refusal was raised.</returns>
        internal static bool MechanicalVentilationRecirculationCooling(
            MechanicalVentilationContext context,
            SystemPlantRoom systemPlantRoom,
            AirSystem airSystem,
            AirHandlingUnit airHandlingUnit,
            string key_AirSystem,
            List<Guid> spaceGuids,
            SystemFan systemFan_Supply,
            ISystemComponent systemComponent_DamperPrototype,
            MechanicalVentilationCoolingSettings mechanicalVentilationCoolingSettings)
        {
            string refusal = mechanicalVentilationCoolingSettings?.Refusal() ?? "states no cooling settings.";
            if (mechanicalVentilationCoolingSettings == null || mechanicalVentilationCoolingSettings.Refusal() != null)
            {
                context.Refuse(string.Format("Air handling unit '{0}' has a cooling module that {1}", airHandlingUnit.Name, refusal));
                return false;
            }

            if (systemFan_Supply == null)
            {
                context.Refuse(string.Format("Air handling unit '{0}' has a cooling module, but its supply fan could not be identified to carry the recirculation loop.", airHandlingUnit.Name));
                return false;
            }

            if (!(systemComponent_DamperPrototype is SystemDamper systemDamper_Prototype))
            {
                context.Refuse(string.Format("Air handling unit '{0}' has a cooling module, but the component on its prototype room's supply connector is not a damper, so there is no damper to derive the recirculation dampers from.", airHandlingUnit.Name));
                return false;
            }

            if (spaceGuids == null || spaceGuids.Count == 0)
            {
                context.Refuse(string.Format("Air handling unit '{0}' has a cooling module and serves no room, so there is nothing to recirculate.", airHandlingUnit.Name));
                return false;
            }

            double ceiling_Lps = mechanicalVentilationCoolingSettings.MaximumOperatingAirFlow_Lps;

            //---------------------------------------------------------------------------------------------
            //The rooms' shares of the ceiling, by floor area. Refused, never defaulted, where an area is
            //missing: an even split would be a distribution nobody stated.
            //---------------------------------------------------------------------------------------------

            List<double> areas = new List<double>(spaceGuids.Count);
            double area_Total = 0;

            foreach (Guid guid_Space in spaceGuids)
            {
                Space space = context.Dictionary_Space[guid_Space];
                double area = space.TryGetValue(SpaceParameter.Area, out double area_Temp) ? area_Temp : double.NaN;

                if (double.IsNaN(area) || double.IsInfinity(area) || area <= 0)
                {
                    context.Refuse(string.Format("Air handling unit '{0}' has a cooling module, but room '{1}' states a floor area of {2} m2, so its share of the recirculation airflow could not be stated.", airHandlingUnit.Name, space.Name, area));
                    return false;
                }

                areas.Add(area);
                area_Total += area;
            }

            //---------------------------------------------------------------------------------------------
            //Identities. Derived from the unit's air-system key and, per room, the analytical space - never
            //minted - so the same design and settings always produce the same branch.
            //---------------------------------------------------------------------------------------------

            Guid Derived(string role, string component = null)
            {
                return component == null
                    ? context.Guid_Derived("RecirculationCooling", role, key_AirSystem)
                    : context.Guid_Derived("RecirculationCooling", role, component, key_AirSystem);
            }

            Guid guid_DXCoil = Derived("DXCoil");
            Guid guid_Fan = Derived("Fan");
            Guid guid_Connection_DXCoilToFan = Derived("Connection:DXCoil-Fan");

            if (context.HasRefusals)
            {
                return false;
            }

            //---------------------------------------------------------------------------------------------
            //The cooling coil.
            //---------------------------------------------------------------------------------------------

            TableModifier tableModifier = SupplyAirTemperatureModifier(mechanicalVentilationCoolingSettings.SupplyAirTemperatureTable, out double base_C);
            if (tableModifier == null)
            {
                context.Refuse(string.Format("Air handling unit '{0}' has a cooling module whose supply-air temperature table could not be written as a complete grid.", airHandlingUnit.Name));
                return false;
            }

            SystemDXCoil systemDXCoil = new SystemDXCoil("Recirculation cooling coil")
            {
                Description = "Aggregate cooling module on the internal recirculation branch: published supply-air temperature, equality table, no extrapolation. Heating duty zero; the heating setpoint is the cooling-enable temperature.",
                CoolingSetpoint = new ModifiableValue(tableModifier, base_C),
                HeatingSetpoint = new ModifiableValue(mechanicalVentilationCoolingSettings.CoolingEnableTemperature_C),
                HeatingDuty = new SizableValue(0.0) { SizingType = SizingType.Value },
            };

            Point2D point2D_Fan = (systemFan_Supply as DisplaySystemFan)?.SystemGeometry?.CoordinateSystem2D?.Origin;
            Point2D point2D_DXCoil = point2D_Fan == null ? new Point2D(0, 0) : new Point2D(point2D_Fan.X, point2D_Fan.Y - 4.0);

            DisplaySystemDXCoil displaySystemDXCoil = systemDXCoil.DisplayObject<DisplaySystemDXCoil>(point2D_DXCoil, Query.DefaultDisplaySystemManager());
            if (displaySystemDXCoil == null || !(displaySystemDXCoil.Duplicate(guid_DXCoil) is DisplaySystemDXCoil displaySystemDXCoil_Keyed))
            {
                context.Refuse(string.Format("Air handling unit '{0}' has a cooling module, but no cooling coil could be drawn for it.", airHandlingUnit.Name));
                return false;
            }

            //---------------------------------------------------------------------------------------------
            //The recirculation fan: the supply fan's copy, at the ceiling, variable speed, no heat gain.
            //---------------------------------------------------------------------------------------------

            if (!(systemFan_Supply.Duplicate(guid_Fan) is SystemFan systemFan))
            {
                context.Refuse(string.Format("Air handling unit '{0}' has a cooling module, but its supply fan could not be copied as the recirculation fan.", airHandlingUnit.Name));
                return false;
            }

            systemFan.Name = "Recirculation fan";
            systemFan.Description = "Pressure-flow carrier for the internal cooling recirculation loop - a simulation surrogate, not a manufacturer fan. No heat gain.";
            systemFan.FanControlType = FanControlType.VariableSpeed;
            systemFan.DesignFlowType = FlowRateType.Value;
            systemFan.DesignFlowRate = AbsoluteFlow(ceiling_Lps, systemFan_Supply.DesignFlowRate);
            systemFan.HeatGainFactor = 0.0;

            systemPlantRoom.Add(displaySystemDXCoil_Keyed);
            systemPlantRoom.Connect(airSystem, displaySystemDXCoil_Keyed);
            systemPlantRoom.Add(systemFan);
            systemPlantRoom.Connect(airSystem, systemFan);

            AttachRecirculation(systemPlantRoom, airSystem, displaySystemDXCoil_Keyed, 1, systemFan, 0, guid_Connection_DXCoilToFan);

            //---------------------------------------------------------------------------------------------
            //The rooms: the SAME SystemSpace each ventilation leg already uses.
            //---------------------------------------------------------------------------------------------

            List<MechanicalVentilationRecirculationRoom> rooms = new List<MechanicalVentilationRecirculationRoom>(spaceGuids.Count);

            for (int i = 0; i < spaceGuids.Count; i++)
            {
                Guid guid_Space = spaceGuids[i];
                string key_Space = Query.MechanicalVentilationGuidComponent(guid_Space);

                if (!context.Dictionary_SystemSpace.TryGetValue(guid_Space, out SystemSpace systemSpace) || systemSpace == null)
                {
                    context.Refuse(string.Format("Air handling unit '{0}' has a cooling module, but room '{1}' has no materialised system space to recirculate through.", airHandlingUnit.Name, context.Dictionary_Space[guid_Space].Name));
                    return false;
                }

                double share_Lps = ceiling_Lps * areas[i] / area_Total;

                Guid guid_Damper_Return = Derived("Damper:Return", key_Space);
                Guid guid_Damper_Supply = Derived("Damper:Supply", key_Space);
                Guid guid_Connection_RoomToReturnDamper = Derived("Connection:Room-ReturnDamper", key_Space);
                Guid guid_Connection_ReturnDamperToDXCoil = Derived("Connection:ReturnDamper-DXCoil", key_Space);
                Guid guid_Connection_FanToSupplyDamper = Derived("Connection:Fan-SupplyDamper", key_Space);
                Guid guid_Connection_SupplyDamperToRoom = Derived("Connection:SupplyDamper-Room", key_Space);

                if (context.HasRefusals)
                {
                    return false;
                }

                SystemDamper systemDamper_Return = RecirculationDamper(systemDamper_Prototype, guid_Damper_Return, "Recirculation return damper", share_Lps);
                SystemDamper systemDamper_Supply = RecirculationDamper(systemDamper_Prototype, guid_Damper_Supply, "Recirculation supply damper", share_Lps);

                if (systemDamper_Return == null || systemDamper_Supply == null)
                {
                    context.Refuse(string.Format("Air handling unit '{0}' has a cooling module, but the recirculation dampers of room '{1}' could not be derived.", airHandlingUnit.Name, systemSpace.Name));
                    return false;
                }

                foreach (SystemDamper systemDamper in new[] { systemDamper_Return, systemDamper_Supply })
                {
                    systemPlantRoom.Add(systemDamper);
                    systemPlantRoom.Connect(airSystem, systemDamper);
                }

                //A SystemSpace's connector 1 is its extract outlet and connector 0 its supply inlet; a
                //damper's 0 is its inlet and 1 its outlet; the coil's and the fan's 0 in, 1 out.
                AttachRecirculation(systemPlantRoom, airSystem, systemSpace, 1, systemDamper_Return, 0, guid_Connection_RoomToReturnDamper);
                AttachRecirculation(systemPlantRoom, airSystem, systemDamper_Return, 1, displaySystemDXCoil_Keyed, 0, guid_Connection_ReturnDamperToDXCoil);
                AttachRecirculation(systemPlantRoom, airSystem, systemFan, 1, systemDamper_Supply, 0, guid_Connection_FanToSupplyDamper);
                AttachRecirculation(systemPlantRoom, airSystem, systemDamper_Supply, 1, systemSpace, 0, guid_Connection_SupplyDamperToRoom);

                rooms.Add(new MechanicalVentilationRecirculationRoom(
                    guid_Space,
                    systemSpace.Guid,
                    guid_Damper_Return,
                    guid_Damper_Supply,
                    guid_Connection_RoomToReturnDamper,
                    guid_Connection_ReturnDamperToDXCoil,
                    guid_Connection_FanToSupplyDamper,
                    guid_Connection_SupplyDamperToRoom,
                    share_Lps));
            }

            context.RecirculationCoolings.Add(new MechanicalVentilationRecirculationCooling(
                airHandlingUnit.Guid,
                airSystem.Guid,
                guid_DXCoil,
                guid_Fan,
                ((SAMObject)systemFan_Supply).Guid,
                guid_Connection_DXCoilToFan,
                rooms,
                mechanicalVentilationCoolingSettings));

            context.Note(string.Format(
                "Air handling unit '{0}' carries a recirculation cooling branch inside its own air system: {1} room(s), {2:0.###} l/s ceiling ({3:0.###} l/s at the lowest fraction), cooling enabled from {4:0.###} C.",
                airHandlingUnit.Name,
                rooms.Count,
                ceiling_Lps,
                mechanicalVentilationCoolingSettings.MinimumOperatingAirFlow_Lps,
                mechanicalVentilationCoolingSettings.CoolingEnableTemperature_C));

            return true;
        }

        /// <summary>
        /// PR5B: read off the finished graph, never a modification of it. Every branch object is in the
        /// result, on its own unit's air system; every room it serves is that air system's existing
        /// <c>SystemSpace</c>; every branch connection states no design flow rate, so no ventilation
        /// reconciliation can count it; and the rooms' shares add up to the ceiling.
        /// </summary>
        internal static void MechanicalVentilationRecirculationCoolingReconcile(MechanicalVentilationContext context, SystemEnergyCentre systemEnergyCentre)
        {
            List<SystemPlantRoom> systemPlantRooms = systemEnergyCentre?.GetSystemPlantRooms();
            if (systemPlantRooms == null || systemPlantRooms.Count != 1)
            {
                context.Refuse("The recirculation cooling branches could not be reconciled: the result holds no single plant room.");
                return;
            }

            SystemPlantRoom systemPlantRoom = systemPlantRooms[0];

            Dictionary<Guid, AirSystem> dictionary_AirSystem = new Dictionary<Guid, AirSystem>();
            foreach (AirSystem airSystem in systemPlantRoom.GetSystems<AirSystem>() ?? new List<AirSystem>())
            {
                dictionary_AirSystem[airSystem.Guid] = airSystem;
            }

            foreach (MechanicalVentilationRecirculationCooling recirculationCooling in context.RecirculationCoolings)
            {
                if (!dictionary_AirSystem.TryGetValue(recirculationCooling.Guid_AirSystem, out AirSystem airSystem))
                {
                    context.Refuse(string.Format("The recirculation cooling branch of air handling unit {0} names air system {1}, which the result does not hold.", recirculationCooling.Guid_AirHandlingUnit, recirculationCooling.Guid_AirSystem));
                    return;
                }

                HashSet<Guid> guids_Component = new HashSet<Guid>();
                foreach (SystemComponent systemComponent in systemPlantRoom.GetSystemComponents<SystemComponent>(airSystem) ?? new List<SystemComponent>())
                {
                    guids_Component.Add(systemComponent.Guid);
                }

                Dictionary<Guid, ISystemConnection> dictionary_Connection = new Dictionary<Guid, ISystemConnection>();
                foreach (ISystemConnection systemConnection in systemPlantRoom.GetSystemComponents<ISystemConnection>(airSystem) ?? new List<ISystemConnection>())
                {
                    dictionary_Connection[systemConnection.Guid] = systemConnection;
                }

                foreach (Guid guid in recirculationCooling.Guids_Component)
                {
                    if (!guids_Component.Contains(guid))
                    {
                        context.Refuse(string.Format("The recirculation cooling branch of air handling unit {0} names component {1}, which is not on its air system.", recirculationCooling.Guid_AirHandlingUnit, guid));
                        return;
                    }
                }

                foreach (Guid guid in recirculationCooling.Guids_Connection)
                {
                    if (!dictionary_Connection.TryGetValue(guid, out ISystemConnection systemConnection))
                    {
                        context.Refuse(string.Format("The recirculation cooling branch of air handling unit {0} names connection {1}, which is not on its air system.", recirculationCooling.Guid_AirHandlingUnit, guid));
                        return;
                    }

                    if (systemConnection is SAMObject sAMObject && sAMObject.TryGetValue(SystemConnectionParameter.DesignFlowRate, out double _))
                    {
                        context.Refuse(string.Format("Recirculation connection {0} states a design flow rate, so it would be counted as a ventilation leg.", guid));
                        return;
                    }
                }

                double share_Total_Lps = 0;
                foreach (MechanicalVentilationRecirculationRoom room in recirculationCooling.Rooms)
                {
                    if (!guids_Component.Contains(room.Guid_SystemSpace) || !context.Dictionary_SystemSpaceGuid.TryGetValue(room.Guid_Space, out Guid guid_SystemSpace) || guid_SystemSpace != room.Guid_SystemSpace)
                    {
                        context.Refuse(string.Format("The recirculation cooling branch of air handling unit {0} serves system space {1}, which is not the existing room of analytical space {2} on its air system.", recirculationCooling.Guid_AirHandlingUnit, room.Guid_SystemSpace, room.Guid_Space));
                        return;
                    }

                    share_Total_Lps += room.DesignFlowRate_Lps;
                }

                if (Math.Abs(share_Total_Lps - recirculationCooling.Settings.MaximumOperatingAirFlow_Lps) > Tolerance_Lps)
                {
                    context.Refuse(string.Format("The recirculation cooling branch of air handling unit {0} shares {1} l/s between its rooms against a ceiling of {2} l/s.", recirculationCooling.Guid_AirHandlingUnit, share_Total_Lps, recirculationCooling.Settings.MaximumOperatingAirFlow_Lps));
                    return;
                }
            }
        }

        /// <summary>
        /// The published supply-air temperature as an equality table over outdoor dry bulb, entering dry
        /// bulb and entering airflow, in that column order, extrapolation off. <c>ArithmeticOperator.Modulus</c>
        /// is SAM's stated "equality" operator for a table modifier: the looked-up value replaces the base.
        /// Axes are addressed by name, so a table published in another axis order writes the same grid.
        /// </summary>
        /// <param name="base_C">The profile's base value - replaced by the equality table wherever it applies; stated as the table's lowest temperature.</param>
        public static TableModifier SupplyAirTemperatureModifier(this VentilationUnitPerformanceTable ventilationUnitPerformanceTable, out double base_C)
        {
            base_C = double.NaN;

            if (ventilationUnitPerformanceTable == null || !ventilationUnitPerformanceTable.IsValid)
            {
                return null;
            }

            int[] axisIndexes = new int[MechanicalVentilationCoolingSettings.AxisNames.Count];
            double[][] axisValues = new double[axisIndexes.Length][];

            for (int i = 0; i < axisIndexes.Length; i++)
            {
                axisIndexes[i] = ventilationUnitPerformanceTable.AxisIndex(MechanicalVentilationCoolingSettings.AxisNames[i]);
                if (axisIndexes[i] < 0)
                {
                    return null;
                }

                axisValues[i] = ventilationUnitPerformanceTable.Axis(axisIndexes[i])?.Values;
                if (axisValues[i] == null || axisValues[i].Length == 0)
                {
                    return null;
                }
            }

            TableModifier result = new TableModifier(
                ArithmeticOperator.Modulus,
                new[]
                {
                    CurveModifierVariableType.ODB.ToString(),
                    CurveModifierVariableType.EDB.ToString(),
                    CurveModifierVariableType.EFlow.ToString(),
                    VentilationUnitPerformanceOutput.Name_SupplyAirTemperature,
                })
            {
                Extrapolate = false,
            };

            int[] indices = new int[ventilationUnitPerformanceTable.AxisCount];
            double minimum = double.PositiveInfinity;

            for (int a = 0; a < axisValues[0].Length; a++)
            {
                for (int b = 0; b < axisValues[1].Length; b++)
                {
                    for (int c = 0; c < axisValues[2].Length; c++)
                    {
                        indices[axisIndexes[0]] = a;
                        indices[axisIndexes[1]] = b;
                        indices[axisIndexes[2]] = c;

                        double value = ventilationUnitPerformanceTable.PublishedValue(VentilationUnitPerformanceOutput.Name_SupplyAirTemperature, indices);
                        if (double.IsNaN(value) || double.IsInfinity(value))
                        {
                            return null;
                        }

                        minimum = Math.Min(minimum, value);

                        result.AddValues(new Dictionary<int, double>
                        {
                            [0] = axisValues[0][a],
                            [1] = axisValues[1][b],
                            [2] = axisValues[2][c],
                            [3] = value,
                        });
                    }
                }
            }

            base_C = minimum;

            return result;
        }

        /// <summary>
        /// An absolute flow [l/s] written so TAS keeps it: <c>SizingType.Value</c> on a
        /// <c>DesignConditionSizedFlowValue</c>, exactly as the ventilation duty carriers are written.
        /// </summary>
        private static DesignConditionSizedFlowValue AbsoluteFlow(double value_Lps, SizedFlowValue sizedFlowValue_Prototype)
        {
            return new DesignConditionSizedFlowValue(
                value_Lps,
                1.0,
                SizingType.Value,
                0.0,
                0.0,
                sizedFlowValue_Prototype is DesignConditionSizedFlowValue designConditionSizedFlowValue ? designConditionSizedFlowValue.SizedFlowMethod : SizedFlowMethod.PerMeterSquared,
                null);
        }

        private static SystemDamper RecirculationDamper(SystemDamper systemDamper_Prototype, Guid guid, string name, double share_Lps)
        {
            if (!(systemDamper_Prototype.Duplicate(guid) is SystemDamper result))
            {
                return null;
            }

            result.Name = name;
            result.Description = "Internal cooling recirculation - an absolute share of the recirculation ceiling, never a ventilation duty.";
            result.DesignFlowType = FlowRateType.Value;
            result.DesignFlowRate = AbsoluteFlow(share_Lps, systemDamper_Prototype.DesignFlowRate);

            //A minimum of nothing, stated rather than inherited from the template's leg.
            result.MinimumFlowRate = new SizedFlowValue(0.0, 0.0);
            result.MinimumFlowType = FlowRateType.None;
            result.MinimumFlowFraction = 0.0;

            return result;
        }

        /// <summary>
        /// One branch connection, wired the way the duty carriers are - both endpoints, the pair and the
        /// system - because both ends of a room already carry a ventilation connection. It states no design
        /// flow rate: it is not a ventilation leg.
        /// </summary>
        private static void AttachRecirculation(SystemPlantRoom systemPlantRoom, AirSystem airSystem, ISystemComponent systemComponent_1, int index_1, ISystemComponent systemComponent_2, int index_2, Guid guid)
        {
            SystemConnection systemConnection = (SystemConnection)new SystemConnection(new SystemType(airSystem), systemComponent_1, index_1, systemComponent_2, index_2).Duplicate(guid);

            systemPlantRoom.Add((ISystemComponent)systemConnection);

            systemPlantRoom.Connect(systemConnection, systemComponent_1);
            systemPlantRoom.Connect(systemConnection, systemComponent_2);
            systemPlantRoom.Connect(systemComponent_1, systemComponent_2);

            systemPlantRoom.Connect(airSystem, (ISystemComponent)systemConnection);
        }
    }
}
