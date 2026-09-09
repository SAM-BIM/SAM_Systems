// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core.Systems;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using Xunit;

namespace SAM.Analytical.Systems.Tests
{
    /// <summary>
    /// A <see cref="YearlySchedule"/> holds 8760 hourly values, and every seam that copies one has to
    /// carry all 8760.
    /// <para>
    /// <b>Why this is a correctness test and not tidiness.</b> A truncating copy is silent: an annual
    /// operating schedule of constant 1.0 becomes one day of 1.0 followed by 8736 hours of 0.0, so the
    /// ventilation runs for a single day of the year and every stage downstream reports success. Nothing
    /// throws, nothing is refused, and the number the assessment ends on is simply wrong.
    /// </para>
    /// <para>
    /// <b>The seams are the ones production actually uses.</b> The copy constructor is the one
    /// <c>Core.Query.Clone</c> resolves to - <see cref="YearlySchedule"/> declares no instance
    /// <c>Clone()</c> - and that clone is what <see cref="ScheduleModifier"/> takes on both of its
    /// constructors and what <see cref="Modify.Merge"/> carries between energy centres. A test that only
    /// exercised the JSON round trip would pass while every in-memory copy in the library truncated.
    /// </para>
    /// </summary>
    public class YearlyScheduleCopyTests
    {
        /// <summary>
        /// The direct copy constructor keeps every hour of the year.
        /// <para>
        /// Each hour carries a distinct value, so a truncation cannot hide behind a constant schedule -
        /// the failure this catches leaves hours 24 to 8759 at 0.0, which a constant-0.0 schedule would
        /// have matched by accident.
        /// </para>
        /// </summary>
        [Fact]
        public void YearlyScheduleCopyConstructor_KeepsAllEightThousandSevenHundredAndSixtyValues()
        {
            YearlySchedule yearlySchedule = DistinctPerHour("Operating");

            YearlySchedule result = new(yearlySchedule);

            AssertDistinctPerHour(result);
        }

        /// <summary>
        /// <c>Core.Query.Clone</c> - the seam every other copy in the library goes through - keeps every
        /// hour. It resolves to the copy constructor above, so this is the same defect reached the way
        /// production reaches it.
        /// </summary>
        [Fact]
        public void CoreQueryClone_KeepsAllEightThousandSevenHundredAndSixtyValues()
        {
            YearlySchedule yearlySchedule = DistinctPerHour("Operating");

            ISchedule result = Core.Query.Clone<ISchedule>(yearlySchedule);

            YearlySchedule yearlySchedule_Result = Assert.IsType<YearlySchedule>(result);

            AssertDistinctPerHour(yearlySchedule_Result);
        }

        /// <summary>
        /// A <see cref="ScheduleModifier"/> keeps every hour - on the value constructor, which clones the
        /// schedule handed to it, and again on its own copy constructor.
        /// </summary>
        [Fact]
        public void ScheduleModifier_KeepsAllEightThousandSevenHundredAndSixtyValues()
        {
            YearlySchedule yearlySchedule = DistinctPerHour("Operating");

            ScheduleModifier scheduleModifier = new(Core.ArithmeticOperator.Multiplication, yearlySchedule, 0);

            AssertDistinctPerHour(Assert.IsType<YearlySchedule>(scheduleModifier.Schedule));

            ScheduleModifier scheduleModifier_Copy = new(scheduleModifier);

            AssertDistinctPerHour(Assert.IsType<YearlySchedule>(scheduleModifier_Copy.Schedule));
        }

        /// <summary>
        /// Merging one energy centre's properties into another keeps every hour of the schedules it
        /// carries across - <see cref="Modify.Merge"/> clones each incoming schedule.
        /// <para>
        /// <b>The merge's own return value is deliberately not asserted.</b> <c>Modify.Merge</c> ends in
        /// an unconditional <c>return false</c> whatever it did, which is separate pre-existing debt and
        /// not this seam - asserting it would fail for a reason that has nothing to do with the schedule.
        /// What is asserted is what the merge actually wrote.
        /// </para>
        /// </summary>
        [Fact]
        public void MergingEnergyCentres_KeepsAllEightThousandSevenHundredAndSixtyValues()
        {
            YearlySchedule yearlySchedule = DistinctPerHour("Operating");

            AnalyticalSystemsProperties analyticalSystemsProperties = new();
            analyticalSystemsProperties.Add(yearlySchedule);

            SystemEnergyCentre systemEnergyCentre_Source = new("Source");
            systemEnergyCentre_Source.SetValue(SystemEnergyCentreParameter.AnalyticalSystemsProperties, analyticalSystemsProperties);

            SystemEnergyCentre systemEnergyCentre_Destination = new("Destination");
            systemEnergyCentre_Destination.SetValue(SystemEnergyCentreParameter.AnalyticalSystemsProperties, new AnalyticalSystemsProperties());

            systemEnergyCentre_Destination.Merge(new List<SystemEnergyCentre> { systemEnergyCentre_Source });

            AnalyticalSystemsProperties analyticalSystemsProperties_Result = systemEnergyCentre_Destination.GetValue<AnalyticalSystemsProperties>(SystemEnergyCentreParameter.AnalyticalSystemsProperties);

            Assert.NotNull(analyticalSystemsProperties_Result);

            AssertDistinctPerHour(Assert.IsType<YearlySchedule>(analyticalSystemsProperties_Result.FindSchedule("Operating")));
        }

        /// <summary>
        /// The JSON round trip keeps every hour. It already did - this is the control that says the
        /// truncation the other cases catch is in the copy path and not in the serialisation.
        /// </summary>
        [Fact]
        public void YearlyScheduleJsonRoundTrip_KeepsAllEightThousandSevenHundredAndSixtyValues()
        {
            YearlySchedule yearlySchedule = DistinctPerHour("Operating");

            JsonObject jsonObject = yearlySchedule.ToJsonObject();

            Assert.NotNull(jsonObject);

            AssertDistinctPerHour(new YearlySchedule(jsonObject));
        }

        /// <summary>An annual schedule whose every hour holds a different, non-zero value.</summary>
        private static YearlySchedule DistinctPerHour(string name)
        {
            YearlySchedule result = new(name);

            double[] values = new double[8760];

            for (int i = 0; i < values.Length; i++)
            {
                values[i] = i + 1;
            }

            result.Values = values;

            return result;
        }

        /// <summary>Every one of the 8760 hours still holds the value <see cref="DistinctPerHour"/> gave it.</summary>
        private static void AssertDistinctPerHour(YearlySchedule yearlySchedule)
        {
            Assert.NotNull(yearlySchedule);

            double[] values = yearlySchedule.Values;

            Assert.NotNull(values);
            Assert.Equal(8760, values.Length);

            for (int i = 0; i < values.Length; i++)
            {
                Assert.Equal(i + 1, values[i]);
            }

            //Named explicitly, because these are the hours a 24-value copy leaves at zero.
            Assert.Equal(4001, yearlySchedule[4000]);
            Assert.Equal(8760, yearlySchedule[8759]);
        }
    }
}
