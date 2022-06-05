﻿using CareTogether.Engines.PolicyEvaluation;
using CareTogether.Resources.Policies;
using CareTogether.Resources.Referrals;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Immutable;

namespace CareTogether.Core.Test.ReferralCalculationTests
{
    [TestClass]
    public class CalculateMissingSetupRequirements
    {
        public static ArrangementPolicy SetupRequirements(params string[] values) =>
            new ArrangementPolicy(string.Empty, ChildInvolvement.ChildHousing,
                ImmutableList<ArrangementFunction>.Empty,
                values.ToImmutableList(),
                ImmutableList<MonitoringRequirement>.Empty,
                ImmutableList<string>.Empty);

        [TestMethod]
        public void TestNoRequirementsCompleted()
        {
            var result = ReferralCalculations.CalculateMissingSetupRequirements(
                SetupRequirements("A", "B", "C"),
                new ArrangementEntry(Guid.Empty, "", DateTime.MinValue,
                    StartedAtUtc: null, EndedAtUtc: null,
                    null, Guid.Empty,
                    Helpers.Completed(),
                    Helpers.Exempted(),
                    ImmutableList<IndividualVolunteerAssignment>.Empty,
                    ImmutableList<FamilyVolunteerAssignment>.Empty,
                    Helpers.LocationHistoryEntries()),
                utcNow: new DateTime(2022, 2, 1));

            AssertEx.SequenceIs(result,
                new MissingArrangementRequirement(null, null, null, null, "A", null, null),
                new MissingArrangementRequirement(null, null, null, null, "B", null, null),
                new MissingArrangementRequirement(null, null, null, null, "C", null, null));
        }

        [TestMethod]
        public void TestPartialRequirementsCompleted()
        {
            var result = ReferralCalculations.CalculateMissingSetupRequirements(
                SetupRequirements("A", "B", "C"),
                new ArrangementEntry(Guid.Empty, "", DateTime.MinValue,
                    StartedAtUtc: null, EndedAtUtc: null,
                    null, Guid.Empty,
                    Helpers.Completed(("A", 1), ("A", 2), ("B", 3)),
                    Helpers.Exempted(),
                    ImmutableList<IndividualVolunteerAssignment>.Empty,
                    ImmutableList<FamilyVolunteerAssignment>.Empty,
                    Helpers.LocationHistoryEntries()),
                utcNow: new DateTime(2022, 2, 1));

            AssertEx.SequenceIs(result,
                new MissingArrangementRequirement(null, null, null, null, "C", null, null));
        }

        [TestMethod]
        public void TestAllRequirementsCompleted()
        {
            var result = ReferralCalculations.CalculateMissingSetupRequirements(
                SetupRequirements("A", "B", "C"),
                new ArrangementEntry(Guid.Empty, "", DateTime.MinValue,
                    StartedAtUtc: null, EndedAtUtc: null,
                    null, Guid.Empty,
                    Helpers.Completed(("A", 1), ("A", 2), ("B", 3), ("C", 12)),
                    Helpers.Exempted(),
                    ImmutableList<IndividualVolunteerAssignment>.Empty,
                    ImmutableList<FamilyVolunteerAssignment>.Empty,
                    Helpers.LocationHistoryEntries()),
                utcNow: new DateTime(2022, 2, 1));

            AssertEx.SequenceIs(result);
        }
    }
}
