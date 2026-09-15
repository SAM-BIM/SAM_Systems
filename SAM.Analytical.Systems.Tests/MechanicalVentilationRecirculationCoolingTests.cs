// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical;
using SAM.Analytical.Systems;
using SAM.Core;
using SAM.Core.Systems;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Xunit;

namespace SAM.Analytical.Systems.Tests
{
    /// <summary>
    /// Part O Iteration 3 PR5B (SAM#111): <see cref="MechanicalVentilationCoolingSettings"/> materialised as an
    /// internal recirculation cooling branch inside the unit's own air system, on its own existing rooms.
    /// <para>
    /// <b>B0 is the control throughout</b>, and every value below is a fixture value - no manufacturer figure
    /// appears in this file.
    /// </para>
    /// </summary>
    public class MechanicalVentilationRecirculationCoolingTests
    {
        private const double Ceiling_Lps = 100.0;
        private const double Gate_C = 21.0;

        // =====================================================================================================
        // A. B0 invariance
        // =====================================================================================================

        [Fact]
        public void EmptyCoolingSettings_MaterialisesByteIdenticalToB0()
        {
            AdjacencyCluster adjacencyCluster = MechanicalVentilationTestModel.Dwelling(out AirHandlingUnit _);

            MechanicalVentilationMaterialisation b0 = adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template());
            MechanicalVentilationMaterialisation empty = adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template(), new MechanicalVentilationSettings { CoolingSettings = new Dictionary<Guid, MechanicalVentilationCoolingSettings>() });

            AssertMaterialised(b0);
            Assert.Equal(MechanicalVentilationTestModel.Json(b0.SystemEnergyCentre), MechanicalVentilationTestModel.Json(empty.SystemEnergyCentre));
            Assert.Empty(b0.RecirculationCoolings);
            Assert.Empty(empty.RecirculationCoolings);
        }

        // =====================================================================================================
        // B. The branch: same air system, same rooms, no new room, no new outdoor path
        // =====================================================================================================

        [Fact]
        public void Branch_IsInsideTheSameAirSystem_OnTheSameRooms()
        {
            AdjacencyCluster adjacencyCluster = MechanicalVentilationTestModel.Dwelling(out AirHandlingUnit airHandlingUnit);

            MechanicalVentilationMaterialisation b0 = adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template());
            MechanicalVentilationMaterialisation b4 = adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template(), Settings(airHandlingUnit, CoolingSettings()));

            AssertMaterialised(b0);
            AssertMaterialised(b4);

            //One air system, the same number of rooms, and the same analytical rooms bound.
            Assert.Single(MechanicalVentilationTestModel.AirSystems(b4));
            Assert.Equal(MechanicalVentilationTestModel.SystemSpaces(b0).Count, MechanicalVentilationTestModel.SystemSpaces(b4).Count);
            Assert.Equal(
                MechanicalVentilationTestModel.Bindings(b0, MechanicalVentilationBindingType.SystemSpace).Select(x => x.Guid_Analytical).OrderBy(x => x),
                MechanicalVentilationTestModel.Bindings(b4, MechanicalVentilationBindingType.SystemSpace).Select(x => x.Guid_Analytical).OrderBy(x => x));

            MechanicalVentilationRecirculationCooling recirculationCooling = Assert.Single(b4.RecirculationCoolings);
            AirSystem airSystem = Assert.Single(MechanicalVentilationTestModel.AirSystems(b4));
            Assert.Equal(airSystem.Guid, recirculationCooling.Guid_AirSystem);
            Assert.Equal(airHandlingUnit.Guid, recirculationCooling.Guid_AirHandlingUnit);

            //Every room of the unit, each on the SystemSpace the lineage binds.
            List<MechanicalVentilationBinding> bindings_SystemSpace = MechanicalVentilationTestModel.Bindings(b4, MechanicalVentilationBindingType.SystemSpace);
            Assert.Equal(bindings_SystemSpace.Count, recirculationCooling.Rooms.Count);
            foreach (MechanicalVentilationRecirculationRoom room in recirculationCooling.Rooms)
            {
                Assert.Contains(bindings_SystemSpace, x => x.Guid_Analytical == room.Guid_Space && x.Guid_Systems == room.Guid_SystemSpace);
            }

            SystemPlantRoom systemPlantRoom = MechanicalVentilationTestModel.PlantRoom(b4);
            List<SystemComponent> components = systemPlantRoom.GetSystemComponents<SystemComponent>(airSystem);
            HashSet<Guid> guids = new(components.Select(x => x.Guid));
            Assert.Superset(recirculationCooling.Guids_Component, guids);

            //One coil, one more fan than B0, and two dampers per room more than B0.
            SystemPlantRoom systemPlantRoom_B0 = MechanicalVentilationTestModel.PlantRoom(b0);
            AirSystem airSystem_B0 = Assert.Single(MechanicalVentilationTestModel.AirSystems(b0));
            List<SystemComponent> components_B0 = systemPlantRoom_B0.GetSystemComponents<SystemComponent>(airSystem_B0);

            Assert.Single(components.OfType<SystemDXCoil>());
            Assert.Empty(components_B0.OfType<SystemDXCoil>());
            Assert.Equal(components_B0.OfType<SystemFan>().Count() + 1, components.OfType<SystemFan>().Count());
            Assert.Equal(components_B0.OfType<SystemDamper>().Count() + 2 * recirculationCooling.Rooms.Count, components.OfType<SystemDamper>().Count());

            //4 connections per room plus coil-to-fan, and none of them carries a design flow rate.
            Assert.Equal(4 * recirculationCooling.Rooms.Count + 1, recirculationCooling.Guids_Connection.Count);
            Dictionary<Guid, double> designFlowRates = MechanicalVentilationTestModel.DesignFlowRates(b4);
            foreach (Guid guid in recirculationCooling.Guids_Connection)
            {
                Assert.DoesNotContain(guid, designFlowRates.Keys);
            }
        }

        [Fact]
        public void VentilationDesign_IsExactlyB0s()
        {
            AdjacencyCluster adjacencyCluster = MechanicalVentilationTestModel.Dwelling(out AirHandlingUnit airHandlingUnit);

            MechanicalVentilationMaterialisation b0 = adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template());
            MechanicalVentilationMaterialisation b4 = adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template(), Settings(airHandlingUnit, CoolingSettings()));

            AssertMaterialised(b4);

            //The same leg duties (identities differ - the settings are folded into them - the duties do not).
            Assert.Equal(
                MechanicalVentilationTestModel.DesignFlowRates(b0).Values.OrderBy(x => x),
                MechanicalVentilationTestModel.DesignFlowRates(b4).Values.OrderBy(x => x));

            //The same room duty on every room, found through the lineage.
            foreach (Space space in adjacencyCluster.GetObjects<Space>())
            {
                SystemSpace systemSpace_B0 = MechanicalVentilationTestModel.SystemSpace(b0, space);
                SystemSpace systemSpace_B4 = MechanicalVentilationTestModel.SystemSpace(b4, space);

                if (systemSpace_B0 == null)
                {
                    Assert.Null(systemSpace_B4);
                    continue;
                }

                Assert.Equal(systemSpace_B0.FlowRate.Value, systemSpace_B4.FlowRate.Value);
                Assert.Equal(systemSpace_B0.FreshAir.Value, systemSpace_B4.FreshAir.Value);
            }

            //The same binding rows, by kind and source.
            foreach (MechanicalVentilationBindingType bindingType in Enum.GetValues(typeof(MechanicalVentilationBindingType)))
            {
                Assert.Equal(
                    MechanicalVentilationTestModel.Bindings(b0, bindingType).Select(x => x.Guid_Analytical).OrderBy(x => x),
                    MechanicalVentilationTestModel.Bindings(b4, bindingType).Select(x => x.Guid_Analytical).OrderBy(x => x));
            }
        }

        // =====================================================================================================
        // C. The components
        // =====================================================================================================

        [Fact]
        public void CoolingCoil_StatesTheTable_TheGate_AndNoHeating()
        {
            AdjacencyCluster adjacencyCluster = MechanicalVentilationTestModel.Dwelling(out AirHandlingUnit airHandlingUnit);
            MechanicalVentilationMaterialisation b4 = adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template(), Settings(airHandlingUnit, CoolingSettings()));

            AssertMaterialised(b4);

            SystemDXCoil systemDXCoil = Component<SystemDXCoil>(b4, b4.RecirculationCoolings[0].Guid_DXCoil);

            TableModifier tableModifier = Assert.IsType<TableModifier>(systemDXCoil.CoolingSetpoint.Modifier);
            Assert.Equal(new[] { "ODB", "EDB", "EFlow", VentilationUnitPerformanceOutput.Name_SupplyAirTemperature }, tableModifier.Headers.ToArray());
            Assert.False(tableModifier.Extrapolate);
            Assert.Equal(ArithmeticOperator.Modulus, tableModifier.ArithmeticOperator);
            Assert.Equal(2 * 2 * 3, tableModifier.RowCount);

            //Every published cell, addressed by coordinate.
            VentilationUnitPerformanceTable table = Table();
            for (int row = 0; row < tableModifier.RowCount; row++)
            {
                Dictionary<int, double> values = tableModifier.GetDictionary(row);
                double expected = table.PublishedValue(
                    VentilationUnitPerformanceOutput.Name_SupplyAirTemperature,
                    Array.IndexOf(table.Axis(0).Values, values[0]),
                    Array.IndexOf(table.Axis(1).Values, values[1]),
                    Array.IndexOf(table.Axis(2).Values, values[2]));

                Assert.Equal(expected, values[3]);
            }

            Assert.Equal(Gate_C, systemDXCoil.HeatingSetpoint.Value);
            Assert.Null(systemDXCoil.HeatingSetpoint.Modifier);

            SizableValue heatingDuty = Assert.IsType<SizableValue>(systemDXCoil.HeatingDuty);
            Assert.Equal(SizingType.Value, heatingDuty.SizingType);
            Assert.Equal(0.0, heatingDuty.ModifiableValue.Value);
        }

        [Fact]
        public void RecirculationFan_IsTheSupplyFansCopy_AtTheCeiling_VariableSpeed_NoHeatGain_CapacityUntouched()
        {
            AdjacencyCluster adjacencyCluster = MechanicalVentilationTestModel.Dwelling(out AirHandlingUnit airHandlingUnit);
            MechanicalVentilationMaterialisation b4 = adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template(), Settings(airHandlingUnit, CoolingSettings()));

            AssertMaterialised(b4);

            MechanicalVentilationRecirculationCooling recirculationCooling = b4.RecirculationCoolings[0];
            SystemFan systemFan = Component<SystemFan>(b4, recirculationCooling.Guid_Fan);
            SystemFan systemFan_Supply = Component<SystemFan>(b4, recirculationCooling.Guid_Fan_Source);

            Assert.Equal(0.0, systemFan.HeatGainFactor);
            Assert.Equal(FanControlType.VariableSpeed, systemFan.FanControlType);
            Assert.Equal(FlowRateType.Value, systemFan.DesignFlowType);
            Assert.Equal(Ceiling_Lps, systemFan.DesignFlowRate.Value);
            Assert.Equal(SizingType.Value, Assert.IsType<DesignConditionSizedFlowValue>(systemFan.DesignFlowRate).SizingType);

            //The pressure and efficiency TAS needs come from the supply fan; the capacity is never written.
            Assert.Equal(systemFan_Supply.Pressure, systemFan.Pressure);
            Assert.Equal(systemFan_Supply.OverallEfficiency.Value, systemFan.OverallEfficiency.Value);
            Assert.Equal(systemFan_Supply.Capacity, systemFan.Capacity);
            Assert.Equal(systemFan_Supply.ScheduleName, systemFan.ScheduleName);

            //And the unit's own supply fan is untouched by the branch.
            MechanicalVentilationMaterialisation b0 = adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template());
            SystemFan systemFan_Supply_B0 = MechanicalVentilationTestModel.PlantRoom(b0).GetSystemComponents<SystemFan>().Single(x => x.Pressure == systemFan_Supply.Pressure && x.HeatGainFactor == systemFan_Supply.HeatGainFactor);
            Assert.Equal(systemFan_Supply_B0.DesignFlowType, systemFan_Supply.DesignFlowType);
            Assert.Equal(systemFan_Supply_B0.FanControlType, systemFan_Supply.FanControlType);
        }

        [Fact]
        public void Dampers_CarryFloorAreaShares_OfTheCeiling_AsAbsoluteValues()
        {
            AdjacencyCluster adjacencyCluster = MechanicalVentilationTestModel.Dwelling(out AirHandlingUnit airHandlingUnit);

            //Uneven areas, so an even split would fail.
            Space space_Big = adjacencyCluster.GetObjects<Space>().Single(x => x.Name == "Living");
            space_Big.SetValue(SpaceParameter.Area, 36.0);
            adjacencyCluster.AddObject(space_Big);

            MechanicalVentilationMaterialisation b4 = adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template(), Settings(airHandlingUnit, CoolingSettings()));

            AssertMaterialised(b4);

            MechanicalVentilationRecirculationCooling recirculationCooling = b4.RecirculationCoolings[0];

            double area_Total = 0;
            foreach (MechanicalVentilationRecirculationRoom room in recirculationCooling.Rooms)
            {
                area_Total += adjacencyCluster.GetObject<Space>(room.Guid_Space).TryGetValue(SpaceParameter.Area, out double area) ? area : double.NaN;
            }

            double share_Total = 0;
            foreach (MechanicalVentilationRecirculationRoom room in recirculationCooling.Rooms)
            {
                double area = adjacencyCluster.GetObject<Space>(room.Guid_Space).TryGetValue(SpaceParameter.Area, out double area_Temp) ? area_Temp : double.NaN;
                double expected = Ceiling_Lps * area / area_Total;

                Assert.Equal(expected, room.DesignFlowRate_Lps, 9);

                foreach (Guid guid_Damper in new[] { room.Guid_Damper_Return, room.Guid_Damper_Supply })
                {
                    SystemDamper systemDamper = Component<SystemDamper>(b4, guid_Damper);
                    Assert.Equal(FlowRateType.Value, systemDamper.DesignFlowType);
                    Assert.Equal(expected, systemDamper.DesignFlowRate.Value, 9);
                    Assert.Equal(SizingType.Value, Assert.IsType<DesignConditionSizedFlowValue>(systemDamper.DesignFlowRate).SizingType);
                    Assert.Equal(FlowRateType.None, systemDamper.MinimumFlowType);
                }

                share_Total += room.DesignFlowRate_Lps;
            }

            Assert.Equal(Ceiling_Lps, share_Total, 9);
            Assert.Equal(Ceiling_Lps * 0.4, recirculationCooling.Settings.MinimumOperatingAirFlow_Lps, 9);
        }

        [Fact]
        public void Topology_RunsRoomReturnDamperCoilFanSupplyDamperRoom()
        {
            AdjacencyCluster adjacencyCluster = MechanicalVentilationTestModel.Dwelling(out AirHandlingUnit airHandlingUnit);
            MechanicalVentilationMaterialisation b4 = adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template(), Settings(airHandlingUnit, CoolingSettings()));

            AssertMaterialised(b4);

            SystemPlantRoom systemPlantRoom = MechanicalVentilationTestModel.PlantRoom(b4);
            Dictionary<Guid, ISystemConnection> connections = (systemPlantRoom.GetSystemConnections() ?? new List<ISystemConnection>()).ToDictionary(x => x.Guid);

            MechanicalVentilationRecirculationCooling recirculationCooling = b4.RecirculationCoolings[0];

            AssertConnection(connections[recirculationCooling.Guid_Connection_DXCoilToFan], recirculationCooling.Guid_DXCoil, 1, recirculationCooling.Guid_Fan, 0);

            foreach (MechanicalVentilationRecirculationRoom room in recirculationCooling.Rooms)
            {
                AssertConnection(connections[room.Guid_Connection_RoomToReturnDamper], room.Guid_SystemSpace, 1, room.Guid_Damper_Return, 0);
                AssertConnection(connections[room.Guid_Connection_ReturnDamperToDXCoil], room.Guid_Damper_Return, 1, recirculationCooling.Guid_DXCoil, 0);
                AssertConnection(connections[room.Guid_Connection_FanToSupplyDamper], recirculationCooling.Guid_Fan, 1, room.Guid_Damper_Supply, 0);
                AssertConnection(connections[room.Guid_Connection_SupplyDamperToRoom], room.Guid_Damper_Supply, 1, room.Guid_SystemSpace, 0);
            }
        }

        [Fact]
        public void TwoUnits_EachGetTheirOwnBranch_InTheirOwnAirSystem()
        {
            AdjacencyCluster adjacencyCluster = MechanicalVentilationTestModel.Dwelling(out AirHandlingUnit airHandlingUnit_1, " 1");
            MechanicalVentilationTestModel.Dwelling(out AirHandlingUnit airHandlingUnit_2, " 2", adjacencyCluster);

            MechanicalVentilationSettings settings = new()
            {
                CoolingSettings = new Dictionary<Guid, MechanicalVentilationCoolingSettings>
                {
                    { airHandlingUnit_1.Guid, CoolingSettings() },
                    { airHandlingUnit_2.Guid, CoolingSettings() },
                },
            };

            MechanicalVentilationMaterialisation b4 = adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template(), settings);

            AssertMaterialised(b4);
            Assert.Equal(2, MechanicalVentilationTestModel.AirSystems(b4).Count);
            Assert.Equal(2, b4.RecirculationCoolings.Count);
            Assert.NotEqual(b4.RecirculationCoolings[0].Guid_AirSystem, b4.RecirculationCoolings[1].Guid_AirSystem);
            Assert.Empty(b4.RecirculationCoolings[0].Guids_Component.Intersect(b4.RecirculationCoolings[1].Guids_Component));
        }

        [Fact]
        public void AlsoMaterialisesOnMVRE()
        {
            AdjacencyCluster adjacencyCluster = MechanicalVentilationTestModel.Dwelling(out AirHandlingUnit airHandlingUnit);
            MechanicalVentilationMaterialisation b4 = adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.TemplateMVRE(), Settings(airHandlingUnit, CoolingSettings()));

            AssertMaterialised(b4);
            Assert.Single(b4.RecirculationCoolings);
        }

        // =====================================================================================================
        // D. Refusals - every one before a graph exists
        // =====================================================================================================

        [Fact]
        public void OneOfTwoUnitsWithoutACoolingModule_FailsClosed()
        {
            AdjacencyCluster adjacencyCluster = MechanicalVentilationTestModel.Dwelling(out AirHandlingUnit airHandlingUnit_1, " 1");
            MechanicalVentilationTestModel.Dwelling(out AirHandlingUnit _, " 2", adjacencyCluster);

            AssertRefused(adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template(), Settings(airHandlingUnit_1, CoolingSettings())), "has no cooling module while other units");
        }

        [Fact]
        public void CoolingSettingsForAUnitNotMaterialised_FailsClosed()
        {
            AdjacencyCluster adjacencyCluster = MechanicalVentilationTestModel.Dwelling(out AirHandlingUnit airHandlingUnit);

            MechanicalVentilationSettings settings = Settings(airHandlingUnit, CoolingSettings());
            Dictionary<Guid, MechanicalVentilationCoolingSettings> dictionary = new(settings.CoolingSettings) { { Guid.NewGuid(), CoolingSettings() } };
            settings.CoolingSettings = dictionary;

            AssertRefused(adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template(), settings), "which this call does not materialise");
        }

        [Theory]
        [InlineData("ceiling-above-table", "above the")]
        [InlineData("ceiling-zero", "maximum operating airflow")]
        [InlineData("no-table", "no supply-air temperature table")]
        [InlineData("missing-axis", "axis")]
        [InlineData("nan-value", "not valid")]
        [InlineData("airflow-unit", "litres per second")]
        [InlineData("one-point-curve", "two-point linear law")]
        [InlineData("falling-curve", "flow-fraction control curve")]
        [InlineData("fraction-above-one", "flow-fraction control curve")]
        [InlineData("no-gate", "cooling-enable temperature")]
        public void InvalidCoolingData_FailsClosed(string defect, string expected)
        {
            AdjacencyCluster adjacencyCluster = MechanicalVentilationTestModel.Dwelling(out AirHandlingUnit airHandlingUnit);

            MechanicalVentilationCoolingSettings coolingSettings = CoolingSettings();

            switch (defect)
            {
                case "ceiling-above-table":
                    coolingSettings.MaximumOperatingAirFlow_Lps = 150.0;
                    break;
                case "ceiling-zero":
                    coolingSettings.MaximumOperatingAirFlow_Lps = 0.0;
                    break;
                case "no-table":
                    coolingSettings.SupplyAirTemperatureTable = null;
                    break;
                case "missing-axis":
                    coolingSettings.SupplyAirTemperatureTable = new VentilationUnitPerformanceTable(
                        [Axis(VentilationUnitPerformanceAxis.Name_ExternalDryBulbTemperature, "degC", 20, 30), Axis("Humidity", "-", 0.2, 0.8), Axis(VentilationUnitPerformanceAxis.Name_AirFlowRate, "l/s", 10, 50, 100)],
                        [new VentilationUnitPerformanceOutput(VentilationUnitPerformanceOutput.Name_SupplyAirTemperature, "degC", Values())]);
                    break;
                case "nan-value":
                    double[] values = Values();
                    values[5] = double.NaN;
                    coolingSettings.SupplyAirTemperatureTable = Table(values);
                    break;
                case "airflow-unit":
                    coolingSettings.SupplyAirTemperatureTable = new VentilationUnitPerformanceTable(
                        [Axis(VentilationUnitPerformanceAxis.Name_ExternalDryBulbTemperature, "degC", 20, 30), Axis(VentilationUnitPerformanceAxis.Name_EnteringDryBulbTemperature, "degC", 22, 26), Axis(VentilationUnitPerformanceAxis.Name_AirFlowRate, "m3/s", 10, 50, 100)],
                        [new VentilationUnitPerformanceOutput(VentilationUnitPerformanceOutput.Name_SupplyAirTemperature, "degC", Values())]);
                    break;
                case "one-point-curve":
                    coolingSettings.FlowFractionByControlTemperature = new FlowFractionControlCurve([21.0], [1.0]);
                    break;
                case "falling-curve":
                    coolingSettings.FlowFractionByControlTemperature = new FlowFractionControlCurve([25.0, 21.0], [0.4, 1.0]);
                    break;
                case "fraction-above-one":
                    coolingSettings.FlowFractionByControlTemperature = new FlowFractionControlCurve([21.0, 25.0], [0.4, 1.2]);
                    break;
                case "no-gate":
                    coolingSettings.CoolingEnableTemperature_C = double.NaN;
                    break;
            }

            AssertRefused(adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template(), Settings(airHandlingUnit, coolingSettings)), expected);
        }

        [Fact]
        public void ARoomWithNoFloorArea_FailsClosed_RatherThanSplittingEvenly()
        {
            AdjacencyCluster adjacencyCluster = MechanicalVentilationTestModel.Dwelling(out AirHandlingUnit airHandlingUnit);

            Space space = adjacencyCluster.GetObjects<Space>().Single(x => x.Name == "Bedroom");
            space.SetValue(SpaceParameter.Area, 0.0);
            adjacencyCluster.AddObject(space);

            AssertRefused(adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template(), Settings(airHandlingUnit, CoolingSettings())), "floor area");
        }

        // =====================================================================================================
        // E. Determinism, isolation, persistence
        // =====================================================================================================

        [Fact]
        public void SameSettings_MaterialiseIdenticalGraphsTwice_AndDifferentSettingsNeverReuseIdentities()
        {
            AdjacencyCluster adjacencyCluster = MechanicalVentilationTestModel.Dwelling(out AirHandlingUnit airHandlingUnit);

            MechanicalVentilationMaterialisation first = adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template(), Settings(airHandlingUnit, CoolingSettings()));
            MechanicalVentilationMaterialisation second = adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template(), Settings(airHandlingUnit, CoolingSettings()));

            AssertMaterialised(first);
            Assert.Equal(MechanicalVentilationTestModel.Json(first.SystemEnergyCentre), MechanicalVentilationTestModel.Json(second.SystemEnergyCentre));
            Assert.Equal(first.RecirculationCoolings[0].Guids_Component.OrderBy(x => x), second.RecirculationCoolings[0].Guids_Component.OrderBy(x => x));

            MechanicalVentilationCoolingSettings coolingSettings_Other = CoolingSettings();
            coolingSettings_Other.CoolingEnableTemperature_C = Gate_C + 1;
            MechanicalVentilationMaterialisation other = adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template(), Settings(airHandlingUnit, coolingSettings_Other));

            AssertMaterialised(other);
            Assert.Empty(first.RecirculationCoolings[0].Guids_Component.Intersect(other.RecirculationCoolings[0].Guids_Component));

            MechanicalVentilationMaterialisation b0 = adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template());
            Assert.NotEqual(Assert.Single(MechanicalVentilationTestModel.AirSystems(b0)).Guid, Assert.Single(MechanicalVentilationTestModel.AirSystems(first)).Guid);
        }

        [Fact]
        public void AxisOrder_DoesNotChangeTheWrittenTable()
        {
            AdjacencyCluster adjacencyCluster = MechanicalVentilationTestModel.Dwelling(out AirHandlingUnit airHandlingUnit);

            //The same grid, published airflow-first.
            VentilationUnitPerformanceTable table = Table();
            double[] permuted = new double[table.PointCount];
            int n = 0;
            for (int c = 0; c < 3; c++)
            {
                for (int a = 0; a < 2; a++)
                {
                    for (int b = 0; b < 2; b++)
                    {
                        permuted[n++] = table.PublishedValue(VentilationUnitPerformanceOutput.Name_SupplyAirTemperature, a, b, c);
                    }
                }
            }

            MechanicalVentilationCoolingSettings coolingSettings_Permuted = CoolingSettings();
            coolingSettings_Permuted.SupplyAirTemperatureTable = new VentilationUnitPerformanceTable(
                [Axis(VentilationUnitPerformanceAxis.Name_AirFlowRate, "l/s", 10, 50, 100), Axis(VentilationUnitPerformanceAxis.Name_ExternalDryBulbTemperature, "degC", 20, 30), Axis(VentilationUnitPerformanceAxis.Name_EnteringDryBulbTemperature, "degC", 22, 26)],
                [new VentilationUnitPerformanceOutput(VentilationUnitPerformanceOutput.Name_SupplyAirTemperature, "degC", permuted)]);

            TableModifier tableModifier = Create.SupplyAirTemperatureModifier(Table(), out double _);
            TableModifier tableModifier_Permuted = Create.SupplyAirTemperatureModifier(coolingSettings_Permuted.SupplyAirTemperatureTable, out double _);

            Assert.Equal(tableModifier.RowCount, tableModifier_Permuted.RowCount);
            for (int row = 0; row < tableModifier.RowCount; row++)
            {
                Assert.Equal(tableModifier.GetDictionary(row), tableModifier_Permuted.GetDictionary(row));
            }

            AssertMaterialised(adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template(), Settings(airHandlingUnit, coolingSettings_Permuted)));
        }

        [Fact]
        public void Materialising_MutatesNeitherTheCallersSettingsNorTheTemplate()
        {
            AdjacencyCluster adjacencyCluster = MechanicalVentilationTestModel.Dwelling(out AirHandlingUnit airHandlingUnit);

            MechanicalVentilationCoolingSettings coolingSettings = CoolingSettings();
            MechanicalVentilationSettings settings = Settings(airHandlingUnit, coolingSettings);
            SystemEnergyCentre template = MechanicalVentilationTestModel.Template();

            string json_Settings = MechanicalVentilationTestModel.Json(settings);
            string json_Cooling = MechanicalVentilationTestModel.Json(coolingSettings);
            string json_Template = MechanicalVentilationTestModel.Json(template);

            MechanicalVentilationMaterialisation b4 = adjacencyCluster.MechanicalVentilation(template, settings);
            AssertMaterialised(b4);

            //Mutating the result's copy of the settings reaches nothing either.
            b4.RecirculationCoolings[0].Settings.MaximumOperatingAirFlow_Lps = 1.0;

            Assert.Equal(json_Settings, MechanicalVentilationTestModel.Json(settings));
            Assert.Equal(json_Cooling, MechanicalVentilationTestModel.Json(coolingSettings));
            Assert.Equal(json_Template, MechanicalVentilationTestModel.Json(template));
            Assert.Equal(Ceiling_Lps, b4.RecirculationCoolings[0].Settings.MaximumOperatingAirFlow_Lps);
        }

        [Fact]
        public void CoolingSettings_SurviveARoundTrip_AndEmptyOmitsTheKey()
        {
            AdjacencyCluster adjacencyCluster = MechanicalVentilationTestModel.Dwelling(out AirHandlingUnit airHandlingUnit);

            MechanicalVentilationSettings settings = Settings(airHandlingUnit, CoolingSettings());
            MechanicalVentilationSettings roundTrip = new(settings.ToJsonObject());

            Assert.Equal(MechanicalVentilationTestModel.Json(settings), MechanicalVentilationTestModel.Json(roundTrip));
            Assert.Null(roundTrip.CoolingSettings[airHandlingUnit.Guid].Refusal());

            Assert.False(new MechanicalVentilationSettings().ToJsonObject().ContainsKey("CoolingSettings"));
        }

        // =====================================================================================================
        // F. Scaling
        // =====================================================================================================

        /// <summary>5 000 spaces, 50 units, a cooling module on every unit - linear, and one branch per unit.</summary>
        [Fact]
        public void FiveThousandSpaces_WithACoolingModuleOnEveryUnit_Materialises()
        {
            AdjacencyCluster adjacencyCluster = MechanicalVentilationTestModel.Scaled(1000, 20, out MechanicalVentilationTestModel.Scale scale);

            Dictionary<Guid, MechanicalVentilationCoolingSettings> coolingSettings = new();
            foreach (AirHandlingUnit airHandlingUnit in adjacencyCluster.GetObjects<AirHandlingUnit>())
            {
                coolingSettings[airHandlingUnit.Guid] = CoolingSettings();
            }

            Stopwatch stopwatch = Stopwatch.StartNew();
            MechanicalVentilationMaterialisation b4 = adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template(), new MechanicalVentilationSettings { CoolingSettings = coolingSettings });
            stopwatch.Stop();

            AssertMaterialised(b4);
            Assert.Equal(scale.AirHandlingUnits, MechanicalVentilationTestModel.AirSystems(b4).Count);
            Assert.Equal(scale.AirHandlingUnits, b4.RecirculationCoolings.Count);
            Assert.Equal(scale.Spaces, b4.RecirculationCoolings.Sum(x => x.Rooms.Count));
            Assert.True(stopwatch.Elapsed < TimeSpan.FromMinutes(2), string.Format("took {0}", stopwatch.Elapsed));
        }

        // =====================================================================================================
        // Fixtures and helpers
        // =====================================================================================================

        private static double[] Values()
        {
            //ODB (2) x EDB (2) x airflow (3), the last axis fastest - fixture values.
            return [14, 15, 16, 16, 17, 18, 15, 16, 17, 17, 18, 19];
        }

        private static VentilationUnitPerformanceAxis Axis(string name, string unit, params double[] values)
        {
            return new VentilationUnitPerformanceAxis(name, unit, values);
        }

        private static VentilationUnitPerformanceTable Table(double[] values = null)
        {
            return new VentilationUnitPerformanceTable(
                [
                    Axis(VentilationUnitPerformanceAxis.Name_ExternalDryBulbTemperature, "degC", 20, 30),
                    Axis(VentilationUnitPerformanceAxis.Name_EnteringDryBulbTemperature, "degC", 22, 26),
                    Axis(VentilationUnitPerformanceAxis.Name_AirFlowRate, "l/s", 10, 50, 100),
                ],
                [new VentilationUnitPerformanceOutput(VentilationUnitPerformanceOutput.Name_SupplyAirTemperature, "degC", values ?? Values())]);
        }

        private static MechanicalVentilationCoolingSettings CoolingSettings()
        {
            return new MechanicalVentilationCoolingSettings
            {
                SupplyAirTemperatureTable = Table(),
                FlowFractionByControlTemperature = new FlowFractionControlCurve([21.0, 25.0], [0.4, 1.0]),
                MaximumOperatingAirFlow_Lps = Ceiling_Lps,
                CoolingEnableTemperature_C = Gate_C,
            };
        }

        private static MechanicalVentilationSettings Settings(AirHandlingUnit airHandlingUnit, MechanicalVentilationCoolingSettings coolingSettings)
        {
            return new MechanicalVentilationSettings
            {
                CoolingSettings = new Dictionary<Guid, MechanicalVentilationCoolingSettings> { { airHandlingUnit.Guid, coolingSettings } },
            };
        }

        private static T Component<T>(MechanicalVentilationMaterialisation mechanicalVentilationMaterialisation, Guid guid) where T : class
        {
            foreach (ISystemComponent systemComponent in MechanicalVentilationTestModel.PlantRoom(mechanicalVentilationMaterialisation).GetSystemComponents() ?? [])
            {
                if (systemComponent is SAMObject sAMObject && sAMObject.Guid == guid)
                {
                    return Assert.IsAssignableFrom<T>(systemComponent);
                }
            }

            throw new InvalidOperationException("No component " + guid);
        }

        private static void AssertConnection(ISystemConnection systemConnection, Guid guid_Upstream, int index_Upstream, Guid guid_Downstream, int index_Downstream)
        {
            Dictionary<Guid, int> indexes = [];
            foreach (ObjectReference objectReference in systemConnection.ObjectReferences)
            {
                Assert.True(Guid.TryParse(objectReference.Reference?.ToString(), out Guid guid));
                Assert.True(systemConnection.TryGetIndex(objectReference, out int index));
                indexes[guid] = index;
            }

            Assert.Equal(2, indexes.Count);
            Assert.Equal(index_Upstream, indexes[guid_Upstream]);
            Assert.Equal(index_Downstream, indexes[guid_Downstream]);
        }

        private static void AssertMaterialised(MechanicalVentilationMaterialisation mechanicalVentilationMaterialisation)
        {
            Assert.NotNull(mechanicalVentilationMaterialisation);
            Assert.True(mechanicalVentilationMaterialisation.IsMaterialised, string.Join("; ", mechanicalVentilationMaterialisation.Refusals));
            Assert.NotNull(mechanicalVentilationMaterialisation.SystemEnergyCentre);
        }

        private static void AssertRefused(MechanicalVentilationMaterialisation mechanicalVentilationMaterialisation, string expected)
        {
            Assert.NotNull(mechanicalVentilationMaterialisation);
            Assert.Null(mechanicalVentilationMaterialisation.SystemEnergyCentre);
            Assert.False(mechanicalVentilationMaterialisation.IsMaterialised);
            Assert.Empty(mechanicalVentilationMaterialisation.RecirculationCoolings);
            Assert.Contains(mechanicalVentilationMaterialisation.Refusals, x => x.Contains(expected, StringComparison.OrdinalIgnoreCase));
        }
    }
}
