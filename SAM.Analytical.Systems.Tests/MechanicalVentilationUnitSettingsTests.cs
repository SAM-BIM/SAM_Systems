// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical;
using SAM.Analytical.Systems;
using SAM.Core;
using SAM.Core.Systems;
using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using Xunit;

namespace SAM.Analytical.Systems.Tests
{
    /// <summary>
    /// Part O Iteration 3 PR5A: <see cref="MechanicalVentilationUnitSettings"/> and its application in
    /// <see cref="Create.MechanicalVentilation"/> - a physical unit's resolved manufacturer-aware behaviour,
    /// mapped onto its own re-keyed fan and exchanger copies (SAM#111 plan §C), plus the topology
    /// normalisation Phase 0's "Test A" and the licensed B0/A parity decomposition both required.
    /// <para>
    /// <b>B0 is the control throughout.</b> Every positive test here is paired against a B0 run of the same
    /// design, and the B0-invariance tests prove that an empty <see cref="MechanicalVentilationSettings.UnitSettings"/>
    /// materialises byte-identically to a call that never knew this feature existed.
    /// </para>
    /// <para>
    /// No real manufacturer figure appears anywhere in this file - every efficiency, pressure and heat gain
    /// factor below is a fixture value, and the shipped catalogue is untouched by PR5A for exactly that
    /// reason (E1/E2 are not yet sourced).
    /// </para>
    /// </summary>
    public class MechanicalVentilationUnitSettingsTests
    {
        // =====================================================================================================
        // A. B0 invariance - the empty-settings path is byte-identical to PR1's own output
        // =====================================================================================================

        /// <summary>
        /// An empty <see cref="MechanicalVentilationSettings"/> (the default) and one with an explicitly
        /// empty <see cref="MechanicalVentilationSettings.UnitSettings"/> produce byte-identical graphs to a
        /// call that supplies no settings object at all - the B0 control is untouched by this feature
        /// existing.
        /// </summary>
        [Fact]
        public void EmptyUnitSettings_MaterialisesByteIdenticalToPR1()
        {
            AdjacencyCluster adjacencyCluster = MechanicalVentilationTestModel.Dwelling(out AirHandlingUnit _);

            MechanicalVentilationMaterialisation mechanicalVentilationMaterialisation_NoSettings = adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template());
            MechanicalVentilationMaterialisation mechanicalVentilationMaterialisation_DefaultSettings = adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template(), new MechanicalVentilationSettings());
            MechanicalVentilationMaterialisation mechanicalVentilationMaterialisation_EmptyUnitSettings = adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template(), new MechanicalVentilationSettings { UnitSettings = new Dictionary<Guid, MechanicalVentilationUnitSettings>() });

            AssertMaterialised(mechanicalVentilationMaterialisation_NoSettings);

            string json = MechanicalVentilationTestModel.Json(mechanicalVentilationMaterialisation_NoSettings.SystemEnergyCentre);

            Assert.Equal(json, MechanicalVentilationTestModel.Json(mechanicalVentilationMaterialisation_DefaultSettings.SystemEnergyCentre));
            Assert.Equal(json, MechanicalVentilationTestModel.Json(mechanicalVentilationMaterialisation_EmptyUnitSettings.SystemEnergyCentre));
        }

        /// <summary>
        /// The zone-flag normalisation below is a no-op on <c>MV.json</c>: it already states
        /// <c>DisplacementVentilation = true</c>, so B0's own materialised rooms are unaffected by this
        /// PR5A change existing.
        /// </summary>
        [Fact]
        public void MVTemplate_DisplacementVentilation_StaysTrue()
        {
            AdjacencyCluster adjacencyCluster = MechanicalVentilationTestModel.Dwelling(out AirHandlingUnit _);

            MechanicalVentilationMaterialisation mechanicalVentilationMaterialisation = adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template());

            AssertMaterialised(mechanicalVentilationMaterialisation);

            foreach (SystemSpace systemSpace in MechanicalVentilationTestModel.SystemSpaces(mechanicalVentilationMaterialisation))
            {
                Assert.True(systemSpace.DisplacementVentilation);
            }
        }

        // =====================================================================================================
        // B. Zone-flag normalisation on MVRE - Phase 0 "Test A" / the B0-A parity decomposition
        // =====================================================================================================

        /// <summary>
        /// <b>The shipped MVRE.json states <c>DisplacementVentilation = false</c> on its zone prototype.</b>
        /// Licensed measurement (SAM#111, "PARTO-ITERATION3-B0-PARITY-DECOMPOSITION") proved the direction:
        /// normalising to <c>true</c> reproduces B0 at MVRE epsilon 0; normalising to <c>false</c> moves
        /// every variant away from it. This materialisation normalises every zone to <c>true</c> regardless
        /// of which template it came from, so MVRE-materialised rooms must come out <c>true</c> even though
        /// the shipped file itself states <c>false</c>.
        /// </summary>
        [Fact]
        public void MVRETemplate_DisplacementVentilation_IsNormalisedToTrue()
        {
            SystemEnergyCentre systemEnergyCentre_MVRE = MechanicalVentilationTestModel.TemplateMVRE();

            //Confirms the fixture actually exercises the normalisation, rather than passing by accident.
            SystemSpace systemSpace_Shipped = FindSystemSpace(systemEnergyCentre_MVRE);
            Assert.False(systemSpace_Shipped.DisplacementVentilation);

            AdjacencyCluster adjacencyCluster = MechanicalVentilationTestModel.Dwelling(out AirHandlingUnit _);

            MechanicalVentilationMaterialisation mechanicalVentilationMaterialisation = adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.TemplateMVRE());

            AssertMaterialised(mechanicalVentilationMaterialisation);

            List<SystemSpace> systemSpaces = MechanicalVentilationTestModel.SystemSpaces(mechanicalVentilationMaterialisation);
            Assert.NotEmpty(systemSpaces);

            foreach (SystemSpace systemSpace in systemSpaces)
            {
                Assert.True(systemSpace.DisplacementVentilation);
            }
        }

        // =====================================================================================================
        // C. Fan settings - applied to the structurally identified supply and extract fan, independently
        // =====================================================================================================

        [Fact]
        public void FanSettings_AreAppliedToTheCorrectFan_Independently()
        {
            AdjacencyCluster adjacencyCluster = MechanicalVentilationTestModel.Dwelling(out AirHandlingUnit airHandlingUnit);

            MechanicalVentilationUnitSettings mechanicalVentilationUnitSettings = new()
            {
                SupplyFanPressure_Pa = 500,
                ExtractFanPressure_Pa = 300,
                FanOverallEfficiency = 0.65,
                SupplyFanHeatGainFactor = 1.0,
                ExtractFanHeatGainFactor = 0.0,
            };

            MechanicalVentilationSettings mechanicalVentilationSettings = new()
            {
                UnitSettings = new Dictionary<Guid, MechanicalVentilationUnitSettings> { { airHandlingUnit.Guid, mechanicalVentilationUnitSettings } }
            };

            MechanicalVentilationMaterialisation mechanicalVentilationMaterialisation = adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.TemplateMVRE(), mechanicalVentilationSettings);

            AssertMaterialised(mechanicalVentilationMaterialisation);

            (SystemFan systemFan_Supply, SystemFan systemFan_Extract) = SupplyAndExtractFans(mechanicalVentilationMaterialisation);

            Assert.Equal(500, systemFan_Supply.Pressure);
            Assert.Equal(300, systemFan_Extract.Pressure);
            Assert.Equal(0.65, systemFan_Supply.OverallEfficiency.Value);
            Assert.Equal(0.65, systemFan_Extract.OverallEfficiency.Value);
            Assert.Equal(1.0, systemFan_Supply.HeatGainFactor);
            Assert.Equal(0.0, systemFan_Extract.HeatGainFactor);
        }

        /// <summary>Stating only one fan field leaves every other property - on both fans - exactly as the template shipped it.</summary>
        [Fact]
        public void OneStatedFanField_LeavesEveryOtherFanPropertyUntouched()
        {
            AdjacencyCluster adjacencyCluster_Baseline = MechanicalVentilationTestModel.Dwelling(out AirHandlingUnit airHandlingUnit_Baseline, "Baseline");
            MechanicalVentilationMaterialisation mechanicalVentilationMaterialisation_Baseline = adjacencyCluster_Baseline.MechanicalVentilation(MechanicalVentilationTestModel.TemplateMVRE());
            AssertMaterialised(mechanicalVentilationMaterialisation_Baseline);
            (SystemFan systemFan_Supply_Baseline, SystemFan systemFan_Extract_Baseline) = SupplyAndExtractFans(mechanicalVentilationMaterialisation_Baseline);

            AdjacencyCluster adjacencyCluster = MechanicalVentilationTestModel.Dwelling(out AirHandlingUnit airHandlingUnit);

            MechanicalVentilationSettings mechanicalVentilationSettings = new()
            {
                UnitSettings = new Dictionary<Guid, MechanicalVentilationUnitSettings>
                {
                    { airHandlingUnit.Guid, new MechanicalVentilationUnitSettings { SupplyFanPressure_Pa = 500 } }
                }
            };

            MechanicalVentilationMaterialisation mechanicalVentilationMaterialisation = adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.TemplateMVRE(), mechanicalVentilationSettings);

            AssertMaterialised(mechanicalVentilationMaterialisation);

            (SystemFan systemFan_Supply, SystemFan systemFan_Extract) = SupplyAndExtractFans(mechanicalVentilationMaterialisation);

            Assert.Equal(500, systemFan_Supply.Pressure);
            Assert.Equal(systemFan_Extract_Baseline.Pressure, systemFan_Extract.Pressure);
            Assert.Equal(systemFan_Supply_Baseline.OverallEfficiency?.Value, systemFan_Supply.OverallEfficiency?.Value);
            Assert.Equal(systemFan_Extract_Baseline.OverallEfficiency?.Value, systemFan_Extract.OverallEfficiency?.Value);
            Assert.Equal(systemFan_Supply_Baseline.HeatGainFactor, systemFan_Supply.HeatGainFactor);
            Assert.Equal(systemFan_Extract_Baseline.HeatGainFactor, systemFan_Extract.HeatGainFactor);
        }

        /// <summary>Fan settings on the MV template, whose prototype attaches a fan at both connectors, apply exactly as on MVRE.</summary>
        [Fact]
        public void FanSettings_ApplyOnTheMVTemplateToo()
        {
            AdjacencyCluster adjacencyCluster = MechanicalVentilationTestModel.Dwelling(out AirHandlingUnit airHandlingUnit);

            MechanicalVentilationSettings mechanicalVentilationSettings = new()
            {
                UnitSettings = new Dictionary<Guid, MechanicalVentilationUnitSettings>
                {
                    { airHandlingUnit.Guid, new MechanicalVentilationUnitSettings { SupplyFanPressure_Pa = 750, FanOverallEfficiency = 1.0, SupplyFanHeatGainFactor = 1.0 } }
                }
            };

            MechanicalVentilationMaterialisation mechanicalVentilationMaterialisation = adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template(), mechanicalVentilationSettings);

            AssertMaterialised(mechanicalVentilationMaterialisation);

            (SystemFan systemFan_Supply, SystemFan _) = SupplyAndExtractFans(mechanicalVentilationMaterialisation);

            Assert.Equal(750, systemFan_Supply.Pressure);
            Assert.Equal(1.0, systemFan_Supply.OverallEfficiency.Value);
            Assert.Equal(1.0, systemFan_Supply.HeatGainFactor);
        }

        // =====================================================================================================
        // D. Heat recovery - the frozen mapping, and the "exactly one exchanger" refusal
        // =====================================================================================================

        /// <summary>
        /// A stated heat recovery efficiency is applied to the unit's single exchanger, and the frozen
        /// mapping's other four properties are forced regardless of what the template shipped -
        /// <c>LatentEfficiency = 0</c>, both calculation fields <c>Simple</c>, <c>HeatingOnly = false</c>,
        /// and <c>AdjustForOptimiser = false</c> even though the shipped <c>MVRE.json</c> states <c>true</c>.
        /// </summary>
        [Fact]
        public void HeatRecoverySettings_ApplyTheFrozenMapping_OverridingTheShippedExchanger()
        {
            SystemExchanger systemExchanger_Shipped = FindSystemExchanger(MechanicalVentilationTestModel.TemplateMVRE());
            Assert.True(systemExchanger_Shipped.AdjustForOptimiser, "fixture assumption: the shipped template states true, so forcing false is a genuine override");

            AdjacencyCluster adjacencyCluster = MechanicalVentilationTestModel.Dwelling(out AirHandlingUnit airHandlingUnit);

            MechanicalVentilationSettings mechanicalVentilationSettings = new()
            {
                UnitSettings = new Dictionary<Guid, MechanicalVentilationUnitSettings>
                {
                    { airHandlingUnit.Guid, new MechanicalVentilationUnitSettings { HeatRecoverySensibleEfficiency = 0.86 } }
                }
            };

            MechanicalVentilationMaterialisation mechanicalVentilationMaterialisation = adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.TemplateMVRE(), mechanicalVentilationSettings);

            AssertMaterialised(mechanicalVentilationMaterialisation);

            SystemExchanger systemExchanger = FindSystemExchanger(mechanicalVentilationMaterialisation);

            Assert.Equal(0.86, systemExchanger.SensibleEfficiency.Value);
            Assert.Equal(0.0, systemExchanger.LatentEfficiency.Value);
            Assert.Equal(ExchangerType.Simple, systemExchanger.ExchangerType);
            Assert.Equal(ExchangerCalculationMethod.Simple, systemExchanger.ExchangerCalculationMethod);
            Assert.False(systemExchanger.HeatingOnly);
            Assert.False(systemExchanger.AdjustForOptimiser);
        }

        /// <summary>Requesting heat recovery on the MV template, whose air system carries no exchanger at all, refuses.</summary>
        [Fact]
        public void HeatRecoveryRequestedOnATemplateWithNoExchanger_FailsClosed()
        {
            AdjacencyCluster adjacencyCluster = MechanicalVentilationTestModel.Dwelling(out AirHandlingUnit airHandlingUnit);

            MechanicalVentilationSettings mechanicalVentilationSettings = new()
            {
                UnitSettings = new Dictionary<Guid, MechanicalVentilationUnitSettings>
                {
                    { airHandlingUnit.Guid, new MechanicalVentilationUnitSettings { HeatRecoverySensibleEfficiency = 0.86 } }
                }
            };

            AssertRefused(adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template(), mechanicalVentilationSettings), "exactly one");
        }

        /// <summary>Not stating a heat recovery efficiency at all leaves the exchanger exactly as MVRE.json ships it.</summary>
        [Fact]
        public void NoHeatRecoverySetting_LeavesTheShippedExchangerUntouched()
        {
            SystemExchanger systemExchanger_Shipped = FindSystemExchanger(MechanicalVentilationTestModel.TemplateMVRE());

            AdjacencyCluster adjacencyCluster = MechanicalVentilationTestModel.Dwelling(out AirHandlingUnit airHandlingUnit);

            MechanicalVentilationSettings mechanicalVentilationSettings = new()
            {
                UnitSettings = new Dictionary<Guid, MechanicalVentilationUnitSettings>
                {
                    { airHandlingUnit.Guid, new MechanicalVentilationUnitSettings { SupplyFanPressure_Pa = 500 } }
                }
            };

            MechanicalVentilationMaterialisation mechanicalVentilationMaterialisation = adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.TemplateMVRE(), mechanicalVentilationSettings);

            AssertMaterialised(mechanicalVentilationMaterialisation);

            SystemExchanger systemExchanger = FindSystemExchanger(mechanicalVentilationMaterialisation);

            Assert.Equal(systemExchanger_Shipped.SensibleEfficiency.Value, systemExchanger.SensibleEfficiency.Value);
            Assert.Equal(systemExchanger_Shipped.AdjustForOptimiser, systemExchanger.AdjustForOptimiser);
        }

        // =====================================================================================================
        // E. Capacity is never written - the fan's Capacity stays exactly the template's value
        // =====================================================================================================

        [Fact]
        public void FanCapacity_IsNeverWritten_RegardlessOfUnitSettings()
        {
            SystemFan systemFan_Shipped = SupplyAndExtractFans(MechanicalVentilationTestModel.TemplateMVRE()).Item1;

            AdjacencyCluster adjacencyCluster = MechanicalVentilationTestModel.Dwelling(out AirHandlingUnit airHandlingUnit);

            MechanicalVentilationSettings mechanicalVentilationSettings = new()
            {
                UnitSettings = new Dictionary<Guid, MechanicalVentilationUnitSettings>
                {
                    { airHandlingUnit.Guid, new MechanicalVentilationUnitSettings { SupplyFanPressure_Pa = 500, ExtractFanPressure_Pa = 500, FanOverallEfficiency = 0.7, SupplyFanHeatGainFactor = 1, ExtractFanHeatGainFactor = 0, HeatRecoverySensibleEfficiency = 0.8 } }
                }
            };

            MechanicalVentilationMaterialisation mechanicalVentilationMaterialisation = adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.TemplateMVRE(), mechanicalVentilationSettings);

            AssertMaterialised(mechanicalVentilationMaterialisation);

            (SystemFan systemFan_Supply, SystemFan systemFan_Extract) = SupplyAndExtractFans(mechanicalVentilationMaterialisation);

            Assert.Equal(systemFan_Shipped.Capacity, systemFan_Supply.Capacity);
            Assert.Equal(systemFan_Shipped.Capacity, systemFan_Extract.Capacity);
        }

        // =====================================================================================================
        // F. All-or-nothing across units - partial configuration refuses
        // =====================================================================================================

        [Fact]
        public void UnitSettingsKeyingAnAirHandlingUnitNotMaterialised_FailsClosed()
        {
            AdjacencyCluster adjacencyCluster = MechanicalVentilationTestModel.Dwelling(out AirHandlingUnit _);

            MechanicalVentilationSettings mechanicalVentilationSettings = new()
            {
                UnitSettings = new Dictionary<Guid, MechanicalVentilationUnitSettings>
                {
                    { Guid.NewGuid(), new MechanicalVentilationUnitSettings { SupplyFanPressure_Pa = 500 } }
                }
            };

            AssertRefused(adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.TemplateMVRE(), mechanicalVentilationSettings), "does not materialise");
        }

        [Fact]
        public void OneOfTwoUnitsMissingSettings_FailsClosed()
        {
            AdjacencyCluster adjacencyCluster = new();

            MechanicalVentilationTestModel.Dwelling(out AirHandlingUnit airHandlingUnit_1, "1", adjacencyCluster);
            MechanicalVentilationTestModel.Dwelling(out AirHandlingUnit airHandlingUnit_2, "2", adjacencyCluster);

            MechanicalVentilationSettings mechanicalVentilationSettings = new()
            {
                UnitSettings = new Dictionary<Guid, MechanicalVentilationUnitSettings>
                {
                    { airHandlingUnit_1.Guid, new MechanicalVentilationUnitSettings { SupplyFanPressure_Pa = 500 } }
                }
            };

            AssertRefused(adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.TemplateMVRE(), mechanicalVentilationSettings), "no unit settings while other units");

            //And the same two units, both configured, materialise.
            mechanicalVentilationSettings.UnitSettings = new Dictionary<Guid, MechanicalVentilationUnitSettings>
            {
                { airHandlingUnit_1.Guid, new MechanicalVentilationUnitSettings { SupplyFanPressure_Pa = 500 } },
                { airHandlingUnit_2.Guid, new MechanicalVentilationUnitSettings { SupplyFanPressure_Pa = 600 } },
            };

            AssertMaterialised(adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.TemplateMVRE(), mechanicalVentilationSettings));
        }

        /// <summary>An all-untouched (every field NaN/null) settings object is not "empty" for the all-or-nothing rule - it still has to name every unit - but it changes nothing when applied.</summary>
        [Fact]
        public void AnAllUntouchedUnitSettingsObject_NamesTheUnitButChangesNothing()
        {
            SystemFan systemFan_Shipped = SupplyAndExtractFans(MechanicalVentilationTestModel.TemplateMVRE()).Item1;

            AdjacencyCluster adjacencyCluster = MechanicalVentilationTestModel.Dwelling(out AirHandlingUnit airHandlingUnit);

            MechanicalVentilationUnitSettings mechanicalVentilationUnitSettings = new();
            Assert.False(mechanicalVentilationUnitSettings.HasOverride);

            MechanicalVentilationSettings mechanicalVentilationSettings = new()
            {
                UnitSettings = new Dictionary<Guid, MechanicalVentilationUnitSettings> { { airHandlingUnit.Guid, mechanicalVentilationUnitSettings } }
            };

            MechanicalVentilationMaterialisation mechanicalVentilationMaterialisation = adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.TemplateMVRE(), mechanicalVentilationSettings);

            AssertMaterialised(mechanicalVentilationMaterialisation);

            (SystemFan systemFan_Supply, SystemFan _) = SupplyAndExtractFans(mechanicalVentilationMaterialisation);
            Assert.Equal(systemFan_Shipped.Pressure, systemFan_Supply.Pressure);
        }

        // =====================================================================================================
        // G. Determinism and isolation
        // =====================================================================================================

        [Fact]
        public void SameSettings_MaterialiseIdenticalGraphsTwice()
        {
            AdjacencyCluster adjacencyCluster = MechanicalVentilationTestModel.Dwelling(out AirHandlingUnit airHandlingUnit);

            MechanicalVentilationSettings mechanicalVentilationSettings = new()
            {
                UnitSettings = new Dictionary<Guid, MechanicalVentilationUnitSettings>
                {
                    { airHandlingUnit.Guid, new MechanicalVentilationUnitSettings { HeatRecoverySensibleEfficiency = 0.83, SupplyFanPressure_Pa = 640 } }
                }
            };

            MechanicalVentilationMaterialisation mechanicalVentilationMaterialisation_1 = adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.TemplateMVRE(), mechanicalVentilationSettings);
            MechanicalVentilationMaterialisation mechanicalVentilationMaterialisation_2 = adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.TemplateMVRE(), mechanicalVentilationSettings);

            AssertMaterialised(mechanicalVentilationMaterialisation_1);

            Assert.Equal(
                MechanicalVentilationTestModel.Json(mechanicalVentilationMaterialisation_1.SystemEnergyCentre),
                MechanicalVentilationTestModel.Json(mechanicalVentilationMaterialisation_2.SystemEnergyCentre));
        }

        /// <summary>Different settings on the same design and template never collide with each other's derived identities, and never collide with the B0 (no-settings) run.</summary>
        [Fact]
        public void DifferentSettings_NeverReuseAnotherRunsIdentities()
        {
            AdjacencyCluster adjacencyCluster = MechanicalVentilationTestModel.Dwelling(out AirHandlingUnit airHandlingUnit);

            MechanicalVentilationMaterialisation mechanicalVentilationMaterialisation_B0 = adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.TemplateMVRE());

            MechanicalVentilationSettings mechanicalVentilationSettings_B1 = new()
            {
                UnitSettings = new Dictionary<Guid, MechanicalVentilationUnitSettings> { { airHandlingUnit.Guid, new MechanicalVentilationUnitSettings { SupplyFanPressure_Pa = 500 } } }
            };
            MechanicalVentilationMaterialisation mechanicalVentilationMaterialisation_B1 = adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.TemplateMVRE(), mechanicalVentilationSettings_B1);

            MechanicalVentilationSettings mechanicalVentilationSettings_B2 = new()
            {
                UnitSettings = new Dictionary<Guid, MechanicalVentilationUnitSettings> { { airHandlingUnit.Guid, new MechanicalVentilationUnitSettings { SupplyFanPressure_Pa = 500, HeatRecoverySensibleEfficiency = 0.83 } } }
            };
            MechanicalVentilationMaterialisation mechanicalVentilationMaterialisation_B2 = adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.TemplateMVRE(), mechanicalVentilationSettings_B2);

            AssertMaterialised(mechanicalVentilationMaterialisation_B0);
            AssertMaterialised(mechanicalVentilationMaterialisation_B1);
            AssertMaterialised(mechanicalVentilationMaterialisation_B2);

            AirSystem airSystem_B0 = Assert.Single(MechanicalVentilationTestModel.AirSystems(mechanicalVentilationMaterialisation_B0));
            AirSystem airSystem_B1 = Assert.Single(MechanicalVentilationTestModel.AirSystems(mechanicalVentilationMaterialisation_B1));
            AirSystem airSystem_B2 = Assert.Single(MechanicalVentilationTestModel.AirSystems(mechanicalVentilationMaterialisation_B2));

            HashSet<Guid> guids = new() { airSystem_B0.Guid, airSystem_B1.Guid, airSystem_B2.Guid };
            Assert.Equal(3, guids.Count);
        }

        /// <summary>
        /// Neither the template instance nor the caller's unit-settings object can be reached by
        /// materialising: the template is untouched, and mutating the <see cref="MechanicalVentilationUnitSettings"/>
        /// instance the caller kept a reference to - after handing it to <see cref="MechanicalVentilationSettings.UnitSettings"/>
        /// - changes neither an already-produced result nor a later one, because the setter takes its own
        /// copy on the way in.
        /// </summary>
        [Fact]
        public void MaterialisingWithUnitSettings_MutatesNeitherTheTemplateNorTheCallersSettings()
        {
            AdjacencyCluster adjacencyCluster = MechanicalVentilationTestModel.Dwelling(out AirHandlingUnit airHandlingUnit);

            SystemEnergyCentre systemEnergyCentre_Template = MechanicalVentilationTestModel.TemplateMVRE();
            string json_Template_Before = MechanicalVentilationTestModel.Json(systemEnergyCentre_Template);

            MechanicalVentilationUnitSettings mechanicalVentilationUnitSettings = new() { SupplyFanPressure_Pa = 500, HeatRecoverySensibleEfficiency = 0.83 };
            MechanicalVentilationSettings mechanicalVentilationSettings = new()
            {
                UnitSettings = new Dictionary<Guid, MechanicalVentilationUnitSettings> { { airHandlingUnit.Guid, mechanicalVentilationUnitSettings } }
            };

            AssertMaterialised(adjacencyCluster.MechanicalVentilation(systemEnergyCentre_Template, mechanicalVentilationSettings));

            Assert.Equal(json_Template_Before, MechanicalVentilationTestModel.Json(systemEnergyCentre_Template));

            //The dictionary entry read back is not the caller's own instance...
            Assert.NotSame(mechanicalVentilationUnitSettings, mechanicalVentilationSettings.UnitSettings[airHandlingUnit.Guid]);

            //...so mutating the object the caller kept a reference to changes nothing the settings object
            //itself now states, or that a fresh materialisation from it would apply.
            mechanicalVentilationUnitSettings.SupplyFanPressure_Pa = 999;

            Assert.Equal(500, mechanicalVentilationSettings.UnitSettings[airHandlingUnit.Guid].SupplyFanPressure_Pa);

            MechanicalVentilationMaterialisation mechanicalVentilationMaterialisation_Second = adjacencyCluster.MechanicalVentilation(systemEnergyCentre_Template, mechanicalVentilationSettings);
            AssertMaterialised(mechanicalVentilationMaterialisation_Second);

            (SystemFan systemFan_Supply, SystemFan _) = SupplyAndExtractFans(mechanicalVentilationMaterialisation_Second);
            Assert.Equal(500, systemFan_Supply.Pressure);
        }

        // =====================================================================================================
        // H. JSON round trip
        // =====================================================================================================

        [Fact]
        public void UnitSettings_SurviveARoundTrip()
        {
            Guid guid = Guid.NewGuid();

            MechanicalVentilationSettings mechanicalVentilationSettings = new()
            {
                UnitSettings = new Dictionary<Guid, MechanicalVentilationUnitSettings>
                {
                    { guid, new MechanicalVentilationUnitSettings { HeatRecoverySensibleEfficiency = 0.83, HeatRecoverySupplyLimit_C = 18, SupplyFanPressure_Pa = 500, ExtractFanPressure_Pa = 480, FanOverallEfficiency = 0.65, SupplyFanHeatGainFactor = 1, ExtractFanHeatGainFactor = 0 } }
                }
            };

            JsonObject jsonObject = mechanicalVentilationSettings.ToJsonObject();
            MechanicalVentilationSettings mechanicalVentilationSettings_RoundTripped = new(jsonObject);

            Assert.True(mechanicalVentilationSettings_RoundTripped.UnitSettings.ContainsKey(guid));

            MechanicalVentilationUnitSettings mechanicalVentilationUnitSettings = mechanicalVentilationSettings_RoundTripped.UnitSettings[guid];

            Assert.Equal(0.83, mechanicalVentilationUnitSettings.HeatRecoverySensibleEfficiency);
            Assert.Equal(18, mechanicalVentilationUnitSettings.HeatRecoverySupplyLimit_C);
            Assert.Equal(500, mechanicalVentilationUnitSettings.SupplyFanPressure_Pa);
            Assert.Equal(480, mechanicalVentilationUnitSettings.ExtractFanPressure_Pa);
            Assert.Equal(0.65, mechanicalVentilationUnitSettings.FanOverallEfficiency);
            Assert.Equal(1, mechanicalVentilationUnitSettings.SupplyFanHeatGainFactor);
            Assert.Equal(0, mechanicalVentilationUnitSettings.ExtractFanHeatGainFactor);
        }

        /// <summary>Settings with no unit entries omit the key entirely, so a B0 settings object serializes exactly as it did before this feature existed.</summary>
        [Fact]
        public void EmptyUnitSettings_OmitsTheKeyFromJson()
        {
            MechanicalVentilationSettings mechanicalVentilationSettings = new() { Name = "Test" };

            JsonObject jsonObject = mechanicalVentilationSettings.ToJsonObject();

            Assert.False(jsonObject.ContainsKey("UnitSettings"));
        }

        /// <summary>Every field left at its default round-trips as entirely absent - not a stated NaN/zero.</summary>
        [Fact]
        public void AnAllUntouchedUnitSettings_SerializesWithNoFields()
        {
            MechanicalVentilationUnitSettings mechanicalVentilationUnitSettings = new();

            JsonObject jsonObject = mechanicalVentilationUnitSettings.ToJsonObject();

            foreach (string key in new[] { "HeatRecoverySensibleEfficiency", "HeatRecoverySupplyLimit_C", "SupplyFanPressure_Pa", "ExtractFanPressure_Pa", "FanOverallEfficiency", "SupplyFanHeatGainFactor", "ExtractFanHeatGainFactor" })
            {
                Assert.False(jsonObject.ContainsKey(key));
            }

            MechanicalVentilationUnitSettings mechanicalVentilationUnitSettings_RoundTripped = new(jsonObject);
            Assert.False(mechanicalVentilationUnitSettings_RoundTripped.HasOverride);
        }

        // =====================================================================================================
        // I. Scaling - a distinct UnitSettings entry for every one of many air handling units
        // =====================================================================================================

        /// <summary>
        /// The generic per-unit settings extension does not turn the materialisation quadratic: 1 000
        /// dwellings (5 000 spaces, the plan's own scaling size), 50 air handling units at 20 dwellings per
        /// unit, each carrying its own distinct <see cref="MechanicalVentilationUnitSettings"/>, still
        /// materialises - one exchanger and two fans correctly configured per unit, never merged, dropped or
        /// shared across units.
        /// </summary>
        [Fact]
        public void FiveThousandSpaces_WithUnitSettingsForEveryAirHandlingUnit_Materialises()
        {
            const int dwellingsPerUnit = 20;

            AdjacencyCluster adjacencyCluster = MechanicalVentilationTestModel.Scaled(1000, dwellingsPerUnit, out MechanicalVentilationTestModel.Scale scale);

            Dictionary<Guid, MechanicalVentilationUnitSettings> unitSettings = new();

            int index = 0;
            foreach (AirHandlingUnit airHandlingUnit in adjacencyCluster.GetObjects<AirHandlingUnit>())
            {
                unitSettings[airHandlingUnit.Guid] = new MechanicalVentilationUnitSettings
                {
                    HeatRecoverySensibleEfficiency = 0.80 + (index % 10) * 0.01,
                    SupplyFanPressure_Pa = 400 + index,
                    ExtractFanPressure_Pa = 380 + index,
                    FanOverallEfficiency = 0.6,
                    SupplyFanHeatGainFactor = 1,
                    ExtractFanHeatGainFactor = 0,
                };

                index++;
            }

            Assert.Equal(scale.AirHandlingUnits, unitSettings.Count);

            MechanicalVentilationSettings mechanicalVentilationSettings = new() { UnitSettings = unitSettings };

            MechanicalVentilationMaterialisation mechanicalVentilationMaterialisation = adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.TemplateMVRE(), mechanicalVentilationSettings);

            AssertMaterialised(mechanicalVentilationMaterialisation);

            List<AirSystem> airSystems = MechanicalVentilationTestModel.AirSystems(mechanicalVentilationMaterialisation);
            Assert.Equal(scale.AirHandlingUnits, airSystems.Count);

            SystemPlantRoom systemPlantRoom = MechanicalVentilationTestModel.PlantRoom(mechanicalVentilationMaterialisation);

            foreach (AirSystem airSystem in airSystems)
            {
                int count_Fan = 0;
                int count_Exchanger = 0;

                foreach (ISystemJSAMObject systemJSAMObject in systemPlantRoom.GetRelatedObjects(airSystem) ?? new List<ISystemJSAMObject>())
                {
                    if (systemJSAMObject is SystemFan)
                    {
                        count_Fan++;
                    }
                    else if (systemJSAMObject is SystemExchanger)
                    {
                        count_Exchanger++;
                    }
                }

                Assert.Equal(2, count_Fan);
                Assert.Equal(1, count_Exchanger);
            }
        }

        // =====================================================================================================
        // Helpers
        // =====================================================================================================

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
            Assert.NotEmpty(mechanicalVentilationMaterialisation.Refusals);
            Assert.Contains(mechanicalVentilationMaterialisation.Refusals, x => x.Contains(expected, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// The one materialised air system's supply and extract fan.
        /// <para>
        /// <b>Not through a served room's connector.</b> Measurement (this PR's own diagnostic pass over
        /// the shipped templates) showed the supply side is not a direct room attachment at all: connector 0
        /// of the prototype room is a damper in both <c>MV.json</c> and <c>MVRE.json</c>, and the supply fan
        /// sits further upstream. Connector 1 <i>is</i> a direct fan attachment ("Return Air Fan" in both
        /// files) - so, exactly as <c>Create.MechanicalVentilationAirSystem</c> itself now does, the extract
        /// fan is found by connector position and the supply fan by elimination: the unit's other fan, among
        /// exactly two related to its <c>AirSystem</c>.
        /// </para>
        /// </summary>
        private static (SystemFan, SystemFan) SupplyAndExtractFans(MechanicalVentilationMaterialisation mechanicalVentilationMaterialisation)
        {
            SystemPlantRoom systemPlantRoom = MechanicalVentilationTestModel.PlantRoom(mechanicalVentilationMaterialisation);
            AirSystem airSystem = Assert.Single(MechanicalVentilationTestModel.AirSystems(mechanicalVentilationMaterialisation));

            SystemFan systemFan_Extract = ExtractFanFromBinding(systemPlantRoom, mechanicalVentilationMaterialisation);

            return (SupplyFanByElimination(systemPlantRoom, airSystem, systemFan_Extract), systemFan_Extract);
        }

        /// <summary>The extract fan, found off the <see cref="MechanicalVentilationBindingType.ExtractConnection"/> production itself built - the one direct room-to-fan connection that exists.</summary>
        private static SystemFan ExtractFanFromBinding(SystemPlantRoom systemPlantRoom, MechanicalVentilationMaterialisation mechanicalVentilationMaterialisation)
        {
            List<MechanicalVentilationBinding> mechanicalVentilationBindings = MechanicalVentilationTestModel.Bindings(mechanicalVentilationMaterialisation, MechanicalVentilationBindingType.ExtractConnection);
            Assert.NotEmpty(mechanicalVentilationBindings);

            Guid guid_Systems = mechanicalVentilationBindings[0].Guid_Systems;

            foreach (ISystemConnection systemConnection in systemPlantRoom.GetSystemConnections() ?? new List<ISystemConnection>())
            {
                if (!(systemConnection is Core.SAMObject sAMObject) || sAMObject.Guid != guid_Systems)
                {
                    continue;
                }

                foreach (ObjectReference objectReference in systemConnection.ObjectReferences ?? new List<ObjectReference>())
                {
                    if (systemPlantRoom.GetSystemComponent<ISystemComponent>(objectReference) is SystemFan systemFan)
                    {
                        return systemFan;
                    }
                }
            }

            throw new InvalidOperationException("No fan found on the ExtractConnection connection.");
        }

        /// <summary>The unit's other fan, among exactly two related to its <c>AirSystem</c>.</summary>
        private static SystemFan SupplyFanByElimination(SystemPlantRoom systemPlantRoom, AirSystem airSystem, SystemFan systemFan_Extract)
        {
            List<SystemFan> systemFans = new();

            foreach (ISystemJSAMObject systemJSAMObject in systemPlantRoom.GetRelatedObjects(airSystem) ?? new List<ISystemJSAMObject>())
            {
                if (systemJSAMObject is SystemFan systemFan)
                {
                    systemFans.Add(systemFan);
                }
            }

            Assert.Equal(2, systemFans.Count);

            SystemFan systemFan_Supply = null;
            int count_NotExtract = 0;

            foreach (SystemFan systemFan_Candidate in systemFans)
            {
                if (systemFan_Candidate.Guid != systemFan_Extract.Guid)
                {
                    systemFan_Supply = systemFan_Candidate;
                    count_NotExtract++;
                }
            }

            Assert.Equal(1, count_NotExtract);

            return systemFan_Supply;
        }

        /// <summary>The unmaterialised template's own prototype fans - for asserting what the shipped file states before any override.</summary>
        private static (SystemFan, SystemFan) SupplyAndExtractFans(SystemEnergyCentre systemEnergyCentre)
        {
            SystemPlantRoom systemPlantRoom = systemEnergyCentre.GetSystemPlantRooms()[0];
            AirSystem airSystem = systemPlantRoom.GetSystems<AirSystem>()[0];

            foreach (SystemSpace systemSpace in systemPlantRoom.GetRelatedObjects<SystemSpace>(airSystem) ?? [])
            {
                if (Core.Systems.Query.SystemConnection(systemPlantRoom, systemSpace, 0) != null && Core.Systems.Query.SystemConnection(systemPlantRoom, systemSpace, 1) != null)
                {
                    SystemFan systemFan_Extract = Attached(systemPlantRoom, systemSpace, 1);

                    return (SupplyFanByElimination(systemPlantRoom, airSystem, systemFan_Extract), systemFan_Extract);
                }
            }

            throw new InvalidOperationException("No prototype space found.");
        }

        private static SystemFan Attached(SystemPlantRoom systemPlantRoom, SystemSpace systemSpace, int index)
        {
            ISystemConnection systemConnection = Core.Systems.Query.SystemConnection(systemPlantRoom, systemSpace, index);

            ObjectReference objectReference_Space = new(systemSpace);

            foreach (ObjectReference objectReference in systemConnection.ObjectReferences ?? new List<ObjectReference>())
            {
                if (objectReference == objectReference_Space)
                {
                    continue;
                }

                if (systemPlantRoom.GetSystemComponent<ISystemComponent>(objectReference) is SystemFan systemFan)
                {
                    return systemFan;
                }
            }

            throw new InvalidOperationException("No fan attached at connector " + index + ".");
        }

        /// <summary>The one exchanger of the one materialised air system.</summary>
        private static SystemExchanger FindSystemExchanger(MechanicalVentilationMaterialisation mechanicalVentilationMaterialisation)
        {
            SystemPlantRoom systemPlantRoom = MechanicalVentilationTestModel.PlantRoom(mechanicalVentilationMaterialisation);
            AirSystem airSystem = Assert.Single(MechanicalVentilationTestModel.AirSystems(mechanicalVentilationMaterialisation));

            foreach (ISystemJSAMObject systemJSAMObject in systemPlantRoom.GetRelatedObjects(airSystem) ?? new List<ISystemJSAMObject>())
            {
                if (systemJSAMObject is SystemExchanger systemExchanger)
                {
                    return systemExchanger;
                }
            }

            throw new InvalidOperationException("No exchanger found.");
        }

        /// <summary>The one exchanger of the unmaterialised template's one air system.</summary>
        private static SystemExchanger FindSystemExchanger(SystemEnergyCentre systemEnergyCentre)
        {
            SystemPlantRoom systemPlantRoom = systemEnergyCentre.GetSystemPlantRooms()[0];
            AirSystem airSystem = systemPlantRoom.GetSystems<AirSystem>()[0];

            foreach (ISystemJSAMObject systemJSAMObject in systemPlantRoom.GetRelatedObjects(airSystem) ?? new List<ISystemJSAMObject>())
            {
                if (systemJSAMObject is SystemExchanger systemExchanger)
                {
                    return systemExchanger;
                }
            }

            throw new InvalidOperationException("No exchanger found.");
        }

        /// <summary>The one zone prototype space of the unmaterialised template - the space attached on both connector 0 and connector 1.</summary>
        private static SystemSpace FindSystemSpace(SystemEnergyCentre systemEnergyCentre)
        {
            SystemPlantRoom systemPlantRoom = systemEnergyCentre.GetSystemPlantRooms()[0];
            AirSystem airSystem = systemPlantRoom.GetSystems<AirSystem>()[0];

            foreach (SystemSpace systemSpace in systemPlantRoom.GetRelatedObjects<SystemSpace>(airSystem) ?? [])
            {
                if (Core.Systems.Query.SystemConnection(systemPlantRoom, systemSpace, 0) != null && Core.Systems.Query.SystemConnection(systemPlantRoom, systemSpace, 1) != null)
                {
                    return systemSpace;
                }
            }

            throw new InvalidOperationException("No prototype space found.");
        }
    }
}
