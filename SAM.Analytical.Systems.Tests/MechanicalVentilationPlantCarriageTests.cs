// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical;
using SAM.Analytical.Systems;
using SAM.Core.Systems;
using System;
using System.Collections.Generic;
using Xunit;

namespace SAM.Analytical.Systems.Tests
{
    /// <summary>
    /// <b>The plant the template brings with it is carried verbatim: nothing is invented, and nothing is
    /// dropped.</b>
    ///
    /// <para><b>Why this file exists</b></para>
    /// <para>
    /// Materialising mechanical ventilation onto a shipped <c>SystemEnergyCentre</c> also carries the
    /// template's <i>non-air</i> plant - its boilers, its circuit groups, its declared design conditions -
    /// into every document built from it. None of that is read by the ventilation route, which makes it
    /// exactly the sort of data a later change could quietly "fix".
    /// </para>
    /// <para>
    /// It must not. Two behaviours were investigated against licensed TAS after the Approved Document O
    /// Iteration 3 foundation was frozen, and both turned out to be faithful carriage of the shipped
    /// template rather than defects in this seam:
    /// </para>
    /// <list type="number">
    /// <item>
    /// The shipped templates state a design flow delta-T of <b>zero</b> on the DHW circuit group and on
    /// its boiler. TAS cannot derive a design flow from zero, so a native plant-room sizing of the
    /// generated document refuses with "Unknown design flow rate into component Multi Boiler 1". The
    /// correct delta-T is engineering data and belongs to whoever owns the template; <b>inventing one
    /// here would put a number nobody stated into every document SAM produces</b>.
    /// </item>
    /// <item>
    /// The shipped templates declare design conditions of their own, named for a weather file that has
    /// nothing to do with the model being materialised. They travel into the result because the template
    /// declares them, and the template's own plant duties reference them by name - so dropping them here
    /// would break the template rather than tidy it.
    /// </item>
    /// </list>
    /// <para>
    /// So the assertions below deliberately compare the result against <b>the template's own values</b>
    /// rather than against constants. A template refresh that states a real delta-T keeps them green; a
    /// change in this seam that substitutes, defaults or drops one does not.
    /// </para>
    /// </summary>
    public class MechanicalVentilationPlantCarriageTests
    {
        /// <summary>
        /// Every plant design property the template states arrives on the result unchanged - including the
        /// zero the DHW circuit states, which is the one a well-meaning default would replace.
        /// </summary>
        [Fact]
        public void TemplatePlant_IsCarriedVerbatim()
        {
            AdjacencyCluster adjacencyCluster = MechanicalVentilationTestModel.Dwelling(out AirHandlingUnit _);

            SystemEnergyCentre systemEnergyCentre_Template = MechanicalVentilationTestModel.Template();

            Dictionary<string, string> dictionary_Template = Plant(systemEnergyCentre_Template);

            //The comparison below is only worth anything if it covers something. The shipped template
            //carries a DHW circuit and its boiler, which is precisely the plant the investigation was
            //about, so a reader of a failure knows what was being compared.
            Assert.Contains(dictionary_Template.Keys, x => x.StartsWith("DHWGroup ", StringComparison.Ordinal));
            Assert.Contains(dictionary_Template.Keys, x => x.StartsWith("MultiBoiler ", StringComparison.Ordinal));
            Assert.Contains(dictionary_Template.Keys, x => x.StartsWith("HeatingGroup ", StringComparison.Ordinal));
            Assert.Contains(dictionary_Template.Keys, x => x.StartsWith("CoolingGroup ", StringComparison.Ordinal));

            MechanicalVentilationMaterialisation mechanicalVentilationMaterialisation = adjacencyCluster.MechanicalVentilation(systemEnergyCentre_Template);

            MechanicalVentilationMaterialisationTests.AssertMaterialised(mechanicalVentilationMaterialisation);

            Dictionary<string, string> dictionary_Result = Plant(mechanicalVentilationMaterialisation.SystemEnergyCentre);

            foreach (KeyValuePair<string, string> keyValuePair in dictionary_Template)
            {
                Assert.True(dictionary_Result.ContainsKey(keyValuePair.Key), string.Format("the materialisation dropped the template's '{0}'", keyValuePair.Key));

                Assert.Equal(keyValuePair.Value, dictionary_Result[keyValuePair.Key]);
            }

            //And nothing was added to the plant either: a materialisation that grew a boiler would be
            //just as wrong as one that changed a duty.
            Assert.Equal(dictionary_Template.Count, dictionary_Result.Count);
        }

        /// <summary>
        /// The design conditions the template declares arrive on the result, in the same order, and the
        /// materialisation declares none of its own.
        /// </summary>
        [Fact]
        public void TemplateDesignConditions_AreCarriedVerbatim()
        {
            AdjacencyCluster adjacencyCluster = MechanicalVentilationTestModel.Dwelling(out AirHandlingUnit _);

            SystemEnergyCentre systemEnergyCentre_Template = MechanicalVentilationTestModel.Template();

            List<string> names_Template = DesignConditions(systemEnergyCentre_Template);

            //Again: an equality between two empty lists proves nothing.
            Assert.NotEmpty(names_Template);

            MechanicalVentilationMaterialisation mechanicalVentilationMaterialisation = adjacencyCluster.MechanicalVentilation(
                systemEnergyCentre_Template,
                new MechanicalVentilationSettings { Schedule = MechanicalVentilationDeterminismTests.Schedule("Operating") });

            MechanicalVentilationMaterialisationTests.AssertMaterialised(mechanicalVentilationMaterialisation);

            Assert.Equal(names_Template, DesignConditions(mechanicalVentilationMaterialisation.SystemEnergyCentre));
        }

        /// <summary>
        /// The operating schedule the caller supplies is added to the template's declared properties
        /// <b>without disturbing them</b> - the case the design-condition carriage has to survive, because
        /// that is the only write the materialisation makes to this object.
        /// </summary>
        [Fact]
        public void SupplyingAnOperatingSchedule_LeavesTheDeclaredDesignConditionsAlone()
        {
            AdjacencyCluster adjacencyCluster = MechanicalVentilationTestModel.Dwelling(out AirHandlingUnit _);

            SystemEnergyCentre systemEnergyCentre_Template = MechanicalVentilationTestModel.Template();

            List<string> names_Template = DesignConditions(systemEnergyCentre_Template);

            Assert.NotEmpty(names_Template);

            MechanicalVentilationMaterialisation mechanicalVentilationMaterialisation = adjacencyCluster.MechanicalVentilation(
                systemEnergyCentre_Template,
                new MechanicalVentilationSettings { Schedule = MechanicalVentilationDeterminismTests.Schedule("Part O continuous operation") });

            MechanicalVentilationMaterialisationTests.AssertMaterialised(mechanicalVentilationMaterialisation);

            AnalyticalSystemsProperties analyticalSystemsProperties = mechanicalVentilationMaterialisation.SystemEnergyCentre.GetValue<AnalyticalSystemsProperties>(SystemEnergyCentreParameter.AnalyticalSystemsProperties);

            Assert.NotNull(analyticalSystemsProperties);
            Assert.NotNull(analyticalSystemsProperties.FindSchedule("Part O continuous operation"));

            Assert.Equal(names_Template, DesignConditions(mechanicalVentilationMaterialisation.SystemEnergyCentre));

            //And the template still declares no operating schedule of its own.
            Assert.Null(MechanicalVentilationTestModel.Template().GetValue<AnalyticalSystemsProperties>(SystemEnergyCentreParameter.AnalyticalSystemsProperties)?.FindSchedule("Part O continuous operation"));
        }

        /// <summary>
        /// Every plant design property worth carrying, keyed by the object it belongs to, as text - so one
        /// failed assertion names the object and shows both values rather than reporting that two graphs
        /// differ somewhere.
        /// </summary>
        private static Dictionary<string, string> Plant(SystemEnergyCentre systemEnergyCentre)
        {
            Dictionary<string, string> result = new(StringComparer.Ordinal);

            List<SystemPlantRoom> systemPlantRooms = systemEnergyCentre?.GetSystemPlantRooms() ?? [];

            foreach (SystemPlantRoom systemPlantRoom in systemPlantRooms)
            {
                foreach (SystemMultiBoiler systemMultiBoiler in systemPlantRoom.GetSystemComponents<SystemMultiBoiler>() ?? [])
                {
                    result[string.Format("MultiBoiler '{0}'", systemMultiBoiler.Name)] = string.Format(
                        System.Globalization.CultureInfo.InvariantCulture,
                        "dT={0} dP={1} capacity={2} setpoint={3} isDHW={4} lossesInSizing={5} sequence={6} duty={7}",
                        systemMultiBoiler.DesignTemperatureDifference,
                        systemMultiBoiler.DesignPressureDrop,
                        systemMultiBoiler.Capacity,
                        systemMultiBoiler.Setpoint?.Value,
                        systemMultiBoiler.IsDomesticHotWater,
                        systemMultiBoiler.LossesInSizing,
                        systemMultiBoiler.Sequence,
                        Duty(systemMultiBoiler.Duty));
                }

                foreach (DomesticHotWaterSystemCollection domesticHotWaterSystemCollection in systemPlantRoom.GetSystemComponents<DomesticHotWaterSystemCollection>() ?? [])
                {
                    result[string.Format("DHWGroup '{0}'", domesticHotWaterSystemCollection.Name)] = string.Format(
                        System.Globalization.CultureInfo.InvariantCulture,
                        "dT={0} dP={1} minimumReturn={2} loadDistribution={3}",
                        domesticHotWaterSystemCollection.DesignTemperatureDifference,
                        domesticHotWaterSystemCollection.DesignPressureDrop,
                        domesticHotWaterSystemCollection.MinimumReturnTemperature,
                        domesticHotWaterSystemCollection.LoadDistribution);
                }

                foreach (HeatingSystemCollection heatingSystemCollection in systemPlantRoom.GetSystemComponents<HeatingSystemCollection>() ?? [])
                {
                    result[string.Format("HeatingGroup '{0}'", heatingSystemCollection.Name)] = string.Format(
                        System.Globalization.CultureInfo.InvariantCulture,
                        "dT={0} dP={1} peakDemand={2}",
                        heatingSystemCollection.DesignTemperatureDifference,
                        heatingSystemCollection.DesignPressureDrop,
                        heatingSystemCollection.PeakDemand);
                }

                foreach (CoolingSystemCollection coolingSystemCollection in systemPlantRoom.GetSystemComponents<CoolingSystemCollection>() ?? [])
                {
                    result[string.Format("CoolingGroup '{0}'", coolingSystemCollection.Name)] = string.Format(
                        System.Globalization.CultureInfo.InvariantCulture,
                        "dT={0} dP={1} peakDemand={2}",
                        coolingSystemCollection.DesignTemperatureDifference,
                        coolingSystemCollection.DesignPressureDrop,
                        coolingSystemCollection.PeakDemand);
                }
            }

            return result;
        }

        /// <summary>A sized duty as text, including which design conditions it is sized against.</summary>
        private static string Duty(ISizableValue iSizableValue)
        {
            if (iSizableValue is not SizableValue sizableValue)
            {
                return iSizableValue is null ? "<none>" : iSizableValue.GetType().Name;
            }

            List<string> names = [.. (sizableValue as DesignConditionSizableValue)?.DesignConditionNames ?? []];

            names.Sort(StringComparer.Ordinal);

            return string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "{0} sizing={1} method={2} on [{3}]",
                sizableValue.ModifiableValue?.Value,
                sizableValue.SizingType,
                sizableValue.SizeMethod,
                string.Join(", ", names));
        }

        private static List<string> DesignConditions(SystemEnergyCentre systemEnergyCentre)
        {
            List<string> result = [];

            AnalyticalSystemsProperties analyticalSystemsProperties = systemEnergyCentre?.GetValue<AnalyticalSystemsProperties>(SystemEnergyCentreParameter.AnalyticalSystemsProperties);

            foreach (DesignCondition designCondition in analyticalSystemsProperties?.DesignConditions ?? [])
            {
                result.Add(string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "{0} [{1}..{2}] precond {3}",
                    designCondition.Name,
                    designCondition.StartHour,
                    designCondition.EndHour,
                    designCondition.PrecondHours));
            }

            return result;
        }
    }
}
