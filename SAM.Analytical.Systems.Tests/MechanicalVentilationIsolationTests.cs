// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical;
using SAM.Analytical.Systems;
using SAM.Core.Systems;
using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace SAM.Analytical.Systems.Tests
{
    /// <summary>
    /// That a materialisation cannot reach anything it was given, and that two materialisations cannot
    /// reach each other.
    /// <para>
    /// <b>Why sharing one object is a correctness failure and not a leak.</b> A materialised room's flow
    /// value that is the same instance as the template's would let writing one dwelling's design airflow
    /// silently change the next dwelling's, and the shipped topology's - and the model would still
    /// serialise, still simulate, and report success with the wrong numbers.
    /// </para>
    /// </summary>
    public class MechanicalVentilationIsolationTests
    {
        /// <summary>
        /// <b>The supplied template is structurally unchanged, and the negative control proves the
        /// comparison can fail.</b>
        /// <para>
        /// The template is serialised before and after; then the returned graph's design airflow, its
        /// connection parameter and its schedule are all mutated and the template is serialised a third
        /// time. If any of those writes reached the template the third comparison breaks - so the test
        /// cannot pass merely because nothing was written anywhere.
        /// </para>
        /// </summary>
        [Fact]
        public void SuppliedTemplate_IsUnchangedAndTheOutputCannotReachIt()
        {
            AdjacencyCluster adjacencyCluster = MechanicalVentilationTestModel.Dwelling(out AirHandlingUnit _);

            SystemEnergyCentre systemEnergyCentre_Template = MechanicalVentilationTestModel.Template();

            string json_Before = MechanicalVentilationTestModel.Json(systemEnergyCentre_Template);

            MechanicalVentilationMaterialisation mechanicalVentilationMaterialisation = adjacencyCluster.MechanicalVentilation(systemEnergyCentre_Template, new MechanicalVentilationSettings { Schedule = MechanicalVentilationDeterminismTests.Schedule("Operating") });

            MechanicalVentilationMaterialisationTests.AssertMaterialised(mechanicalVentilationMaterialisation);

            Assert.Equal(json_Before, MechanicalVentilationTestModel.Json(systemEnergyCentre_Template));

            //The negative control: write all over the result, and prove the writes really landed in it -
            //otherwise the comparison below would hold whatever the materialisation shared.
            string json_Result = MechanicalVentilationTestModel.Json(mechanicalVentilationMaterialisation.SystemEnergyCentre);

            Mutate(mechanicalVentilationMaterialisation);

            AssertMutated(mechanicalVentilationMaterialisation, json_Result);

            Assert.Equal(json_Before, MechanicalVentilationTestModel.Json(systemEnergyCentre_Template));
        }

        /// <summary>
        /// <b>The analytical model is strictly read-only.</b> Its serialisation is unchanged, and every
        /// design terminal still states the duty it stated - a materialisation that "tidied" a duty would
        /// have rewritten the design it was asked to read.
        /// </summary>
        [Fact]
        public void AnalyticalModel_IsUnchanged()
        {
            AdjacencyCluster adjacencyCluster = MechanicalVentilationTestModel.Dwelling(out AirHandlingUnit _);

            string json_Before = MechanicalVentilationTestModel.Json(adjacencyCluster);

            Dictionary<Guid, double?> dictionary = [];

            foreach (VentilationTerminal ventilationTerminal in adjacencyCluster.GetObjects<VentilationTerminal>())
            {
                dictionary[ventilationTerminal.Guid] = ventilationTerminal.DesignFlowRate_Lps;
            }

            int count_Before = adjacencyCluster.GetObjects().Count;

            MechanicalVentilationMaterialisation mechanicalVentilationMaterialisation = adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template());

            MechanicalVentilationMaterialisationTests.AssertMaterialised(mechanicalVentilationMaterialisation);

            string json_Result = MechanicalVentilationTestModel.Json(mechanicalVentilationMaterialisation.SystemEnergyCentre);

            Mutate(mechanicalVentilationMaterialisation);

            AssertMutated(mechanicalVentilationMaterialisation, json_Result);

            Assert.Equal(json_Before, MechanicalVentilationTestModel.Json(adjacencyCluster));
            Assert.Equal(count_Before, adjacencyCluster.GetObjects().Count);

            foreach (VentilationTerminal ventilationTerminal in adjacencyCluster.GetObjects<VentilationTerminal>())
            {
                Assert.Equal(dictionary[ventilationTerminal.Guid], ventilationTerminal.DesignFlowRate_Lps);
            }
        }

        /// <summary>
        /// <b>Two materialisations from one template instance share no mutable state.</b> Writing all over
        /// the first leaves the second untouched - and within one result, writing one unit's fan leaves the
        /// other unit's fan alone, because they are separate objects rather than one seen twice.
        /// </summary>
        [Fact]
        public void IndependentMaterialisations_ShareNoMutableState()
        {
            AdjacencyCluster adjacencyCluster = new();

            MechanicalVentilationTestModel.Dwelling(out AirHandlingUnit _, " 1", adjacencyCluster);
            MechanicalVentilationTestModel.Dwelling(out AirHandlingUnit _, " 2", adjacencyCluster);

            SystemEnergyCentre systemEnergyCentre_Template = MechanicalVentilationTestModel.Template();

            MechanicalVentilationMaterialisation mechanicalVentilationMaterialisation_A = adjacencyCluster.MechanicalVentilation(systemEnergyCentre_Template, new MechanicalVentilationSettings { Schedule = MechanicalVentilationDeterminismTests.Schedule("Operating") });
            MechanicalVentilationMaterialisation mechanicalVentilationMaterialisation_B = adjacencyCluster.MechanicalVentilation(systemEnergyCentre_Template, new MechanicalVentilationSettings { Schedule = MechanicalVentilationDeterminismTests.Schedule("Operating") });

            MechanicalVentilationMaterialisationTests.AssertMaterialised(mechanicalVentilationMaterialisation_A);
            MechanicalVentilationMaterialisationTests.AssertMaterialised(mechanicalVentilationMaterialisation_B);

            string json_A = MechanicalVentilationTestModel.Json(mechanicalVentilationMaterialisation_A.SystemEnergyCentre);
            string json_B = MechanicalVentilationTestModel.Json(mechanicalVentilationMaterialisation_B.SystemEnergyCentre);

            //The two are byte-identical to begin with, which is what makes the next assertion meaningful:
            //A really did change, and B really did not.
            Assert.Equal(json_A, json_B);

            Mutate(mechanicalVentilationMaterialisation_A);

            AssertMutated(mechanicalVentilationMaterialisation_A, json_A);

            Assert.Equal(json_B, MechanicalVentilationTestModel.Json(mechanicalVentilationMaterialisation_B.SystemEnergyCentre));

            //And within one result: the two units' re-keyed fans are separate instances.
            SystemPlantRoom systemPlantRoom = MechanicalVentilationTestModel.PlantRoom(mechanicalVentilationMaterialisation_B);

            List<AirSystem> airSystems = MechanicalVentilationTestModel.AirSystems(mechanicalVentilationMaterialisation_B);

            Assert.Equal(2, airSystems.Count);

            List<SystemFan> systemFans_1 = Fans(systemPlantRoom, airSystems[0]);
            List<SystemFan> systemFans_2 = Fans(systemPlantRoom, airSystems[1]);

            Assert.NotEmpty(systemFans_1);
            Assert.Equal(systemFans_1.Count, systemFans_2.Count);

            HashSet<Guid> guids = [];

            foreach (SystemFan systemFan in systemFans_1)
            {
                Assert.True(guids.Add(systemFan.Guid));
            }

            foreach (SystemFan systemFan in systemFans_2)
            {
                Assert.True(guids.Add(systemFan.Guid), "The two units share a fan identity.");
            }
        }

        /// <summary>
        /// <b>The shipped <c>MV.json</c> is a topology source and is never written.</b> Its bytes and its
        /// last-write time are unchanged across a materialisation that loaded it. A build that quietly
        /// rewrote a shipped resource would change every later run on the machine.
        /// </summary>
        [Fact]
        public void ShippedTemplateFile_IsNeverWritten()
        {
            string path = MechanicalVentilationTestModel.PathTemplate();

            Assert.True(File.Exists(path));

            byte[] bytes_Before = File.ReadAllBytes(path);
            DateTime dateTime_Before = File.GetLastWriteTimeUtc(path);

            AdjacencyCluster adjacencyCluster = MechanicalVentilationTestModel.Dwelling(out AirHandlingUnit _);

            SystemEnergyCentre systemEnergyCentre_Template = Analytical.Systems.Query.SystemEnergyCentre(path);

            MechanicalVentilationMaterialisation mechanicalVentilationMaterialisation = adjacencyCluster.MechanicalVentilation(systemEnergyCentre_Template);

            MechanicalVentilationMaterialisationTests.AssertMaterialised(mechanicalVentilationMaterialisation);

            string json_Result = MechanicalVentilationTestModel.Json(mechanicalVentilationMaterialisation.SystemEnergyCentre);

            Mutate(mechanicalVentilationMaterialisation);

            AssertMutated(mechanicalVentilationMaterialisation, json_Result);

            Assert.Equal(bytes_Before, File.ReadAllBytes(path));
            Assert.Equal(dateTime_Before, File.GetLastWriteTimeUtc(path));
        }

        /// <summary>
        /// <b>An annual operating schedule survives the whole route.</b> All 8760 distinct hours are on the
        /// result and survive a JSON round trip - and the schedule on the result is <b>not the caller's
        /// instance</b>, so writing the caller's own schedule afterwards cannot change the graph.
        /// </summary>
        [Fact]
        public void AnnualSchedule_SurvivesMaterialisationAndRoundTripAndIsNotTheCallersInstance()
        {
            AdjacencyCluster adjacencyCluster = MechanicalVentilationTestModel.Dwelling(out AirHandlingUnit _);

            YearlySchedule yearlySchedule = new("Operating");

            double[] values = new double[8760];
            for (int i = 0; i < values.Length; i++)
            {
                values[i] = i + 1;
            }

            yearlySchedule.Values = values;

            MechanicalVentilationMaterialisation mechanicalVentilationMaterialisation = adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template(), new MechanicalVentilationSettings { Schedule = yearlySchedule });

            MechanicalVentilationMaterialisationTests.AssertMaterialised(mechanicalVentilationMaterialisation);

            YearlySchedule yearlySchedule_Result = Assert.IsType<YearlySchedule>(Properties(mechanicalVentilationMaterialisation.SystemEnergyCentre).FindSchedule("Operating"));

            //Not the caller's instance.
            Assert.NotSame(yearlySchedule, yearlySchedule_Result);

            AssertAnnual(yearlySchedule_Result);

            //And through a JSON round trip of the whole energy centre.
            SystemEnergyCentre systemEnergyCentre = new(mechanicalVentilationMaterialisation.SystemEnergyCentre.ToJsonObject());

            AssertAnnual(Assert.IsType<YearlySchedule>(Properties(systemEnergyCentre).FindSchedule("Operating")));

            //The caller's own instance is untouched, and writing it now changes nothing in the graph.
            AssertAnnual(yearlySchedule);

            yearlySchedule[4000] = -1.0;

            Assert.Equal(4001, Assert.IsType<YearlySchedule>(Properties(mechanicalVentilationMaterialisation.SystemEnergyCentre).FindSchedule("Operating"))[4000]);

            static void AssertAnnual(YearlySchedule yearlySchedule)
            {
                double[] values = yearlySchedule.Values;

                Assert.Equal(8760, values.Length);

                for (int i = 0; i < values.Length; i++)
                {
                    Assert.Equal(i + 1, values[i]);
                }

                Assert.Equal(4001, yearlySchedule[4000]);
                Assert.Equal(8760, yearlySchedule[8759]);
            }
        }

        /// <summary>
        /// The materialised fans reference the operating schedule by name - which is how a component
        /// references a schedule at all, and what makes the annual series above reachable from the plant.
        /// </summary>
        [Fact]
        public void MaterialisedFans_ReferenceTheOperatingSchedule()
        {
            AdjacencyCluster adjacencyCluster = MechanicalVentilationTestModel.Dwelling(out AirHandlingUnit _);

            MechanicalVentilationMaterialisation mechanicalVentilationMaterialisation = adjacencyCluster.MechanicalVentilation(MechanicalVentilationTestModel.Template(), new MechanicalVentilationSettings { Schedule = MechanicalVentilationDeterminismTests.Schedule("Operating") });

            MechanicalVentilationMaterialisationTests.AssertMaterialised(mechanicalVentilationMaterialisation);

            int count = 0;

            foreach (ISystemComponent systemComponent in MechanicalVentilationTestModel.PlantRoom(mechanicalVentilationMaterialisation).GetSystemComponents())
            {
                if (systemComponent is SystemFan systemFan)
                {
                    count++;
                    Assert.Equal("Operating", systemFan.ScheduleName);
                }
            }

            Assert.NotEqual(0, count);
        }

        /// <summary>
        /// <b>Copying a <c>SystemSpace</c> copies its flow values rather than sharing them.</b>
        /// <para>
        /// Every sibling component - fan, damper, exchanger - clones its flow values, and
        /// <c>SizedFlowValue.Value</c> has a public setter that <c>Modify.UpdateSpaceAirflows</c> writes in
        /// place. A shared reference is therefore a live route from a copy back into whatever it was
        /// copied from: writing one dwelling's design airflow would change the template's, and the next
        /// dwelling's, silently.
        /// </para>
        /// <para>
        /// The materialisation does not depend on this - it assigns brand-new flow values to every room it
        /// builds - so this is hardening of the copy path itself, and it is what makes the guarantee hold
        /// for every other caller of that constructor too.
        /// </para>
        /// </summary>
        [Fact]
        public void CopyingASystemSpace_CopiesItsFlowValuesRatherThanSharingThem()
        {
            SystemSpace systemSpace = new(
                "Bedroom",
                12.0,
                30.0,
                null,
                null,
                null,
                false,
                false,
                true,
                new DesignConditionSizedFlowValue(30.0, 1.0, SizingType.Value, double.NaN, double.NaN, SizedFlowMethod.PerMeterSquared, null),
                new DesignConditionSizedFlowValue(30.0, 1.0, SizingType.Value, double.NaN, double.NaN, SizedFlowMethod.PerMeterSquared, null),
                double.NaN);

            foreach (SystemSpace systemSpace_Copy in new[] { new SystemSpace(systemSpace), new SystemSpace(Guid.NewGuid(), systemSpace) })
            {
                Assert.NotSame(systemSpace.FlowRate, systemSpace_Copy.FlowRate);
                Assert.NotSame(systemSpace.FreshAir, systemSpace_Copy.FreshAir);

                //The values are carried across, and writing the copy's does not reach the original.
                Assert.Equal(30.0, systemSpace_Copy.FlowRate.Value);
                Assert.Equal(30.0, systemSpace_Copy.FreshAir.Value);

                systemSpace_Copy.FlowRate.Value = -999.0;
                systemSpace_Copy.FreshAir.Value = -999.0;

                Assert.Equal(30.0, systemSpace.FlowRate.Value);
                Assert.Equal(30.0, systemSpace.FreshAir.Value);
            }
        }

        // -------------------------------------------------------------------------------------------------

        private static AnalyticalSystemsProperties Properties(SystemEnergyCentre systemEnergyCentre)
        {
            return systemEnergyCentre.GetValue<AnalyticalSystemsProperties>(SystemEnergyCentreParameter.AnalyticalSystemsProperties);
        }

        private static List<SystemFan> Fans(SystemPlantRoom systemPlantRoom, AirSystem airSystem)
        {
            List<SystemFan> result = [];

            foreach (ISystemJSAMObject systemJSAMObject in systemPlantRoom.GetRelatedObjects(airSystem) ?? [])
            {
                if (systemJSAMObject is SystemFan systemFan)
                {
                    result.Add(systemFan);
                }
            }

            return result;
        }

        private const double Overwritten = -999.0;
        private const string OverwrittenName = "Overwritten";

        /// <summary>
        /// Writes all over a returned graph - the design airflow of every room, the design flow rate of
        /// every leg, every fan's schedule reference and the operating schedule - and <b>writes the result
        /// back into the returned energy centre</b>.
        /// <para>
        /// <b>The write-back is what makes this a negative control at all.</b> Every getter on the graph
        /// hands out a clone: <c>GetSystemPlantRooms</c>, <c>GetSystemComponents</c> and
        /// <c>GetSystemConnections</c> all copy on the way out. Mutating what they return and stopping
        /// there would change a throwaway, so the "the template did not change" assertion that follows
        /// would hold no matter what the materialisation shared. Adding each mutated object back - keyed
        /// by guid, so it replaces its entry - is what puts the change into the graph the caller holds.
        /// </para>
        /// <para>
        /// This is a property of the graph's copy-on-read semantics and nothing here changes them:
        /// <see cref="AssertMutated"/> reads the graph back afterwards and fails if the writes did not land.
        /// </para>
        /// </summary>
        private static void Mutate(MechanicalVentilationMaterialisation mechanicalVentilationMaterialisation)
        {
            SystemEnergyCentre systemEnergyCentre = mechanicalVentilationMaterialisation.SystemEnergyCentre;

            SystemPlantRoom systemPlantRoom = MechanicalVentilationTestModel.PlantRoom(mechanicalVentilationMaterialisation);

            foreach (ISystemComponent systemComponent in systemPlantRoom.GetSystemComponents() ?? [])
            {
                if (systemComponent is SystemSpace systemSpace && systemSpace.FlowRate != null)
                {
                    systemSpace.FlowRate.Value = Overwritten;
                    systemSpace.FreshAir.Value = Overwritten;

                    systemPlantRoom.Add(systemSpace);
                }

                if (systemComponent is SystemFan systemFan)
                {
                    systemFan.ScheduleName = OverwrittenName;

                    systemPlantRoom.Add((ISystemComponent)systemFan);
                }
            }

            foreach (ISystemConnection systemConnection in systemPlantRoom.GetSystemConnections() ?? [])
            {
                if (systemConnection is Core.SAMObject sAMObject)
                {
                    sAMObject.SetValue(SystemConnectionParameter.DesignFlowRate, Overwritten);

                    systemPlantRoom.Add((ISystemComponent)systemConnection);
                }
            }

            //Keyed by guid, so this replaces the plant room the energy centre holds rather than adding a
            //second one.
            systemEnergyCentre.Add(systemPlantRoom);

            AnalyticalSystemsProperties analyticalSystemsProperties = Properties(systemEnergyCentre);

            if (analyticalSystemsProperties != null)
            {
                foreach (ISchedule schedule in analyticalSystemsProperties.Schedules ?? [])
                {
                    if (schedule is YearlySchedule yearlySchedule)
                    {
                        yearlySchedule[0] = Overwritten;

                        analyticalSystemsProperties.Add(yearlySchedule);
                    }
                }

                systemEnergyCentre.SetValue(SystemEnergyCentreParameter.AnalyticalSystemsProperties, analyticalSystemsProperties);
            }
        }

        /// <summary>
        /// That <see cref="Mutate"/>'s writes actually landed in the graph the caller holds. Without this
        /// the isolation assertions would pass vacuously.
        /// </summary>
        private static void AssertMutated(MechanicalVentilationMaterialisation mechanicalVentilationMaterialisation, string json_Before)
        {
            //The whole graph is different from what it was.
            Assert.NotEqual(json_Before, MechanicalVentilationTestModel.Json(mechanicalVentilationMaterialisation.SystemEnergyCentre));

            List<SystemSpace> systemSpaces = MechanicalVentilationTestModel.SystemSpaces(mechanicalVentilationMaterialisation);

            Assert.NotEmpty(systemSpaces);

            foreach (SystemSpace systemSpace in systemSpaces)
            {
                Assert.Equal(Overwritten, systemSpace.FlowRate.Value);
            }

            Dictionary<Guid, double> dictionary = MechanicalVentilationTestModel.DesignFlowRates(mechanicalVentilationMaterialisation);

            Assert.NotEmpty(dictionary);

            foreach (double value in dictionary.Values)
            {
                Assert.Equal(Overwritten, value);
            }
        }
    }
}
