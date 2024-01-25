﻿using CareTogether.Resources;
using CareTogether.Resources.Approvals;
using CareTogether.Resources.Directory;
using CareTogether.Resources.Policies;
using System;
using System.Collections.Immutable;
using System.Linq;

namespace CareTogether.Engines.PolicyEvaluation
{
    internal static class ApprovalCalculations
    {
        public static FamilyApprovalStatus CalculateCombinedFamilyApprovals(
            VolunteerPolicy volunteerPolicy, Family family,
            ImmutableList<CompletedRequirementInfo> completedFamilyRequirements,
            ImmutableList<ExemptedRequirementInfo> exemptedFamilyRequirements,
            ImmutableList<RemovedRole> removedFamilyRoles,
            ImmutableDictionary<Guid, ImmutableList<CompletedRequirementInfo>> completedIndividualRequirements,
            ImmutableDictionary<Guid, ImmutableList<ExemptedRequirementInfo>> exemptedIndividualRequirements,
            ImmutableDictionary<Guid, ImmutableList<RemovedRole>> removedIndividualRoles)
        {
            var allAdultsIndividualApprovalStatus = family.Adults
                .Select(adultFamilyEntry =>
                {
                    var (person, familyRelationship) = adultFamilyEntry;

                    var completedRequirements = completedIndividualRequirements
                        .GetValueOrEmptyList(person.Id);
                    var exemptedRequirements = exemptedIndividualRequirements
                        .GetValueOrEmptyList(person.Id);
                    var removedRoles = removedIndividualRoles
                        .GetValueOrEmptyList(person.Id);

                    var individualApprovalStatus =
                        IndividualApprovalCalculations.CalculateIndividualApprovalStatus(
                            volunteerPolicy.VolunteerRoles,
                            completedRequirements, exemptedRequirements, removedRoles);

                    return (person.Id, individualApprovalStatus);
                })
                .ToImmutableDictionary(x => x.Id, x => x.Item2);

            var familyRoleApprovalStatuses =
                FamilyApprovalCalculations.CalculateAllFamilyRoleApprovalStatuses(
                    volunteerPolicy.VolunteerFamilyRoles,
                    family,
                    completedFamilyRequirements, exemptedFamilyRequirements,
                    removedFamilyRoles,
                    completedIndividualRequirements, exemptedIndividualRequirements,
                    removedIndividualRoles);

            return new FamilyApprovalStatus(
                allAdultsIndividualApprovalStatus,
                familyRoleApprovalStatuses);
        }
    }
}
