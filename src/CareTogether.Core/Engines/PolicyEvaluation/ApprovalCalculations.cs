﻿using CareTogether.Resources;
using CareTogether.Resources.Approvals;
using CareTogether.Resources.Directory;
using CareTogether.Resources.Policies;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Timelines;

namespace CareTogether.Engines.PolicyEvaluation
{
    internal static class ApprovalCalculations
    {
        public static VolunteerFamilyApprovalStatus CalculateVolunteerFamilyApprovalStatus(
            VolunteerPolicy volunteerPolicy, Family family, DateTime utcNow,
            ImmutableList<CompletedRequirementInfo> completedFamilyRequirements,
            ImmutableList<ExemptedRequirementInfo> exemptedFamilyRequirements,
            ImmutableList<RemovedRole> removedFamilyRoles,
            ImmutableDictionary<Guid, ImmutableList<CompletedRequirementInfo>> completedIndividualRequirements,
            ImmutableDictionary<Guid, ImmutableList<ExemptedRequirementInfo>> exemptedIndividualRequirements,
            ImmutableDictionary<Guid, ImmutableList<RemovedRole>> removedIndividualRoles)
        {
            var individualResults = CalculateCombinedIndividualRoleStatusForFamilyMembers(
                volunteerPolicy.VolunteerRoles, family,
                completedIndividualRequirements, exemptedIndividualRequirements, removedIndividualRoles);

            var familyResult = CalculateCombinedFamilyRoleStatusForFamily(
                volunteerPolicy, family,
                completedFamilyRequirements, exemptedFamilyRequirements, removedFamilyRoles,
                completedIndividualRequirements, exemptedIndividualRequirements, removedIndividualRoles);

            var effectiveFamilyRoleVersionApprovals = familyResult.FamilyRoleVersionApprovals
                .Select(roleApprovals =>
                    (role: roleApprovals.Key,
                    effectiveApproval: CalculateEffectiveRoleVersionApproval(roleApprovals.Value, utcNow)))
                .Where(roleApproval => roleApproval.effectiveApproval != null)
                .ToImmutableDictionary(roleApproval => roleApproval.role, roleApproval => roleApproval.effectiveApproval!);

            return new VolunteerFamilyApprovalStatus(
                familyResult.FamilyRoleVersionApprovals,
                effectiveFamilyRoleVersionApprovals,
                familyResult.RemovedFamilyRoles,
                familyResult.MissingFamilyRequirements,
                familyResult.AvailableFamilyApplications,
                family.Adults.Select(adultFamilyEntry =>
                {
                    var (person, familyRelationship) = adultFamilyEntry;
                    var individualResult = individualResults.TryGetValue(person.Id, out var result)
                        ? result :
                        (IndividualRoleVersionApprovals: ImmutableDictionary<string, ImmutableList<RoleVersionApproval>>.Empty,
                        RemovedIndividualRoles: ImmutableList<RemovedRole>.Empty,
                        MissingIndividualRequirements: ImmutableList<string>.Empty,
                        AvailableIndividualApplications: ImmutableList<string>.Empty);

                    var mergedMissingIndividualRequirements = individualResult.MissingIndividualRequirements
                        .Concat(familyResult.MissingIndividualRequirementsForFamilyRoles.TryGetValue(person.Id, out var missing)
                            ? missing : ImmutableList<string>.Empty)
                        .Distinct()
                        .ToImmutableList();

                    var effectiveIndividualRoleVersionApprovals = individualResult.IndividualRoleVersionApprovals
                        .Select(roleApprovals =>
                            (role: roleApprovals.Key,
                            effectiveApproval: CalculateEffectiveRoleVersionApproval(roleApprovals.Value, utcNow)))
                        .Where(roleApproval => roleApproval.effectiveApproval != null)
                        .ToImmutableDictionary(roleApproval => roleApproval.role, roleApproval => roleApproval.effectiveApproval!);

                    return new KeyValuePair<Guid, VolunteerApprovalStatus>(person.Id, new VolunteerApprovalStatus(
                        IndividualRoleApprovals: individualResult.IndividualRoleVersionApprovals,
                        EffectiveIndividualRoleApprovals: effectiveIndividualRoleVersionApprovals,
                        RemovedIndividualRoles: individualResult.RemovedIndividualRoles,
                        MissingIndividualRequirements: mergedMissingIndividualRequirements,
                        AvailableIndividualApplications: individualResult.AvailableIndividualApplications));
                }).ToImmutableDictionary());
        }

        /// <summary>
        /// Given potentially multiple calculated role version approvals (due to having multiple policies or
        /// perhaps multiple ways that the approval was qualified for), select the one that gives the best
        /// (most-approved) status for the overall role, since that will always be the one of interest.
        /// </summary>
        internal static RoleVersionApproval? CalculateEffectiveRoleVersionApproval(
            ImmutableList<RoleVersionApproval> roleVersionApprovals, DateTime utcNow)
        {
            if (roleVersionApprovals.Count == 0)
                return null;

            // Based on the current timestamp, treat any expired approvals (approved or onboarded status) as expired.
            // (Note that the raw calculations which serve as inputs for this method don't ever return 'expired' themselves.)
            // Then, sort the status values by the numeric value of the ApprovalStatus enum cases.
            // This means that Onboarded trumps Approved, which trumps Expired, which trumps Prospective.
            // Within each status level, return the expiration date that is furthest in the future.
            var bestCurrentApproval = roleVersionApprovals
                //TODO: Is there a more straightforward way to treat a Prospective status that has expired?
                .Select(rva => rva.ApprovalStatus > RoleApprovalStatus.Expired && rva.ExpiresAt != null && rva.ExpiresAt < utcNow
                    ? rva with { ApprovalStatus = RoleApprovalStatus.Expired }
                    : rva)
                .OrderByDescending(rva => rva.ApprovalStatus)
                .ThenByDescending(rva => rva.ExpiresAt ?? DateTime.MaxValue)
                .First();

            return bestCurrentApproval;
        }

        internal static ImmutableDictionary<Guid,
            (ImmutableDictionary<string, ImmutableList<RoleVersionApproval>> IndividualRoleVersionApprovals,
            ImmutableList<RemovedRole> RemovedIndividualRoles,
            ImmutableList<string> MissingIndividualRequirements,
            ImmutableList<string> AvailableIndividualApplications)>
            CalculateCombinedIndividualRoleStatusForFamilyMembers(
            ImmutableDictionary<string, VolunteerRolePolicy> volunteerRoles, Family family,
            ImmutableDictionary<Guid, ImmutableList<CompletedRequirementInfo>> completedIndividualRequirements,
            ImmutableDictionary<Guid, ImmutableList<ExemptedRequirementInfo>> exemptedIndividualRequirements,
            ImmutableDictionary<Guid, ImmutableList<RemovedRole>> removedIndividualRoles)
        {
            var allAdultsVolunteerApprovalStatus = family.Adults.Select(adultFamilyEntry =>
            {
                var (person, familyRelationship) = adultFamilyEntry;

                var completedRequirements = completedIndividualRequirements.GetValueOrEmptyList(person.Id);
                var exemptedRequirements = exemptedIndividualRequirements.GetValueOrEmptyList(person.Id);
                var removedRoles = removedIndividualRoles.GetValueOrEmptyList(person.Id);

                var allIndividualRoleApprovals = volunteerRoles
                    .Where(rolePolicy => !removedRoles.Any(x => x.RoleName == rolePolicy.Key))
                    .Select(rolePolicy => (RoleName: rolePolicy.Key,
                        StatusByVersions: rolePolicy.Value.PolicyVersions.Select(policyVersion =>
                        {
                            var (Status, ExpiresAtUtc, MissingRequirements, AvailableApplications) =
                                CalculateIndividualVolunteerRoleApprovalStatus(
                                    policyVersion, completedRequirements, exemptedRequirements);
                            return (PolicyVersion: policyVersion, Status, ExpiresAtUtc, MissingRequirements, AvailableApplications);
                        })
                        .ToImmutableList()))
                    .ToImmutableList();

                var volunteerApprovalStatus = (person.Id, (
                    IndividualRoleVersionApprovals: allIndividualRoleApprovals
                        .Where(x => x.StatusByVersions.Any(y => y.Status.HasValue))
                        .ToImmutableDictionary(
                            x => x.RoleName,
                            x => x.StatusByVersions
                                .Where(y => y.Status.HasValue)
                                .Select(y => new RoleVersionApproval(y.PolicyVersion.Version, y.Status!.Value, y.ExpiresAtUtc))
                                .ToImmutableList()),
                    RemovedIndividualRoles: removedRoles,
                    MissingIndividualRequirements: allIndividualRoleApprovals
                        .SelectMany(x => x.StatusByVersions.SelectMany(y => y.MissingRequirements))
                        .Distinct()
                        .ToImmutableList(),
                    AvailableIndividualApplications: allIndividualRoleApprovals
                        .SelectMany(x => x.StatusByVersions.SelectMany(y => y.AvailableApplications))
                        .Distinct()
                        .ToImmutableList()));

                return volunteerApprovalStatus;
            }).ToImmutableDictionary(x => x.Id, x => x.Item2);
            return allAdultsVolunteerApprovalStatus;
        }

        internal static
            (ImmutableDictionary<string, ImmutableList<RoleVersionApproval>> FamilyRoleVersionApprovals,
            ImmutableList<RemovedRole> RemovedFamilyRoles,
            ImmutableList<string> MissingFamilyRequirements,
            ImmutableList<string> AvailableFamilyApplications,
            ImmutableDictionary<Guid, ImmutableList<string>> MissingIndividualRequirementsForFamilyRoles)
            CalculateCombinedFamilyRoleStatusForFamily(
            VolunteerPolicy volunteerPolicy, Family family,
            ImmutableList<CompletedRequirementInfo> completedFamilyRequirements,
            ImmutableList<ExemptedRequirementInfo> exemptedFamilyRequirements,
            ImmutableList<RemovedRole> removedFamilyRoles,
            ImmutableDictionary<Guid, ImmutableList<CompletedRequirementInfo>> completedIndividualRequirements,
            ImmutableDictionary<Guid, ImmutableList<ExemptedRequirementInfo>> exemptedIndividualRequirements,
            ImmutableDictionary<Guid, ImmutableList<RemovedRole>> removedIndividualRoles)
        {
            var allFamilyRoleApprovals = volunteerPolicy.VolunteerFamilyRoles
                .Where(rolePolicy => !removedFamilyRoles.Any(x => x.RoleName == rolePolicy.Key))
                .Select(rolePolicy => (RoleName: rolePolicy.Key,
                    StatusByVersions: rolePolicy.Value.PolicyVersions.Select(policyVersion =>
                    {
                        var (Status, ExpiresAtUtc, MissingRequirements, AvailableApplications, MissingIndividualRequirements) =
                            CalculateFamilyVolunteerRoleApprovalStatus(
                                rolePolicy.Key, policyVersion, family,
                                completedFamilyRequirements, exemptedFamilyRequirements,
                                completedIndividualRequirements, exemptedIndividualRequirements,
                                removedIndividualRoles);
                        return (PolicyVersion: policyVersion, Status, ExpiresAtUtc, MissingRequirements, AvailableApplications, MissingIndividualRequirements);
                    })
                    .ToImmutableList()))
                .ToImmutableList();

            var volunteerFamilyApprovalStatus = (
                FamilyRoleVersionApprovals: allFamilyRoleApprovals
                    .Where(x => x.StatusByVersions.Any(y => y.Status.HasValue))
                    .ToImmutableDictionary(
                        x => x.RoleName,
                        x => x.StatusByVersions
                            .Where(y => y.Status.HasValue)
                            .Select(y => new RoleVersionApproval(y.PolicyVersion.Version, y.Status!.Value, y.ExpiresAtUtc))
                            .ToImmutableList()),
                RemovedFamilyRoles: removedFamilyRoles,
                MissingFamilyRequirements: allFamilyRoleApprovals
                        .SelectMany(x => x.StatusByVersions.SelectMany(y => y.MissingRequirements))
                        .Distinct()
                        .ToImmutableList(),
                AvailableFamilyApplications: allFamilyRoleApprovals
                        .SelectMany(x => x.StatusByVersions.SelectMany(y => y.AvailableApplications))
                        .Distinct()
                        .ToImmutableList(),
                MissingIndividualRequirementsForFamilyRoles: allFamilyRoleApprovals
                        .SelectMany(x => x.StatusByVersions
                            .SelectMany(y => y.MissingIndividualRequirements
                                .SelectMany(z => z.Value
                                    .Select(q => (PersonId: z.Key, MissingRequirement: q)))))
                        .Distinct()
                        .GroupBy(x => x.PersonId)
                        .ToImmutableDictionary(
                            x => x.Key,
                            x => x.Select(y => y.MissingRequirement).ToImmutableList()));

            return volunteerFamilyApprovalStatus;
        }

        internal static
            (RoleApprovalStatus? Status, DateTime? ExpiresAtUtc, ImmutableList<string> MissingRequirements, ImmutableList<string> AvailableApplications)
            CalculateIndividualVolunteerRoleApprovalStatus(
            ImmutableDictionary<string, ActionRequirement> actionDefinitions, VolunteerRolePolicyVersion policyVersion,
            ImmutableList<CompletedRequirementInfo> completedRequirements, ImmutableList<ExemptedRequirementInfo> exemptedRequirements)
        {
            var supersededAtUtc = policyVersion.SupersededAtUtc;

            // For each requirement of the policy version for this role, find the dates for which it was met or exempted.
            // If there are none, the resulting timeline will be 'null'.
            var requirementCompletionStatus = policyVersion.Requirements.Select(requirement =>
            {
                var actionDefinition = actionDefinitions[requirement.ActionName];
                return (requirement.ActionName, requirement.Stage, RequirementApprovals:
                    SharedCalculations.FindRequirementApprovals(requirement.ActionName, actionDefinition.Validity,
                        completedRequirements, exemptedRequirements));
            }).ToImmutableList();

            var simpleRequirementCompletionStatus = requirementCompletionStatus
                .Select(status => (status.ActionName, status.Stage, RequirementMetOrExempted: status.RequirementApprovals != null))
                .ToImmutableList();

            var status = CalculateRoleApprovalStatusesFromRequirementCompletions(requirementCompletionStatus);
            //TODO: This is simplistic. Do we want to instead calculate 'missing as of date'? If so, that feels like a separate calculation.
            //TODO: Do we want to return 'expiredRequirements' as well?
            var missingRequirements = CalculateMissingIndividualRequirementsFromRequirementCompletion(status.Status, simpleRequirementCompletionStatus);
            var availableApplications = CalculateAvailableIndividualApplicationsFromRequirementCompletion(status.Status, simpleRequirementCompletionStatus);

            return (status.Status, status.ExpiresAtUtc, MissingRequirements: missingRequirements, AvailableApplications: availableApplications);
        }

        internal static
            (RoleApprovalStatus? Status, DateTime? ExpiresAtUtc, ImmutableList<string> MissingRequirements, ImmutableList<string> AvailableApplications,
            ImmutableDictionary<Guid, ImmutableList<string>> MissingIndividualRequirements)
            CalculateFamilyVolunteerRoleApprovalStatus
            (string roleName, VolunteerFamilyRolePolicyVersion policyVersion, Family family,
            ImmutableList<CompletedRequirementInfo> completedFamilyRequirements, ImmutableList<ExemptedRequirementInfo> exemptedFamilyRequirements,
            ImmutableDictionary<Guid, ImmutableList<CompletedRequirementInfo>> completedIndividualRequirements,
            ImmutableDictionary<Guid, ImmutableList<ExemptedRequirementInfo>> exemptedIndividualRequirements,
            ImmutableDictionary<Guid, ImmutableList<RemovedRole>> removedIndividualRoles)
        {
            var supersededAtUtc = policyVersion.SupersededAtUtc;

            var activeAdults = family.Adults
                .Where(a => a.Item1.Active)
                .Select(a =>
                {
                    var (person, _) = a;
                    var completedRequirements = completedIndividualRequirements.GetValueOrEmptyList(person.Id);
                    var exemptedRequirements = exemptedIndividualRequirements.GetValueOrEmptyList(person.Id);
                    return (a.Item1.Id, CompletedRequirements: completedRequirements, ExemptedRequirements: exemptedRequirements);
                })
                .ToImmutableList();

            var requirementsMet = policyVersion.Requirements.Select(requirement =>
                (requirement.ActionName, requirement.Stage, requirement.Scope,
                    RequirementMetOrExempted: FamilyRequirementMetOrExempted(roleName, requirement.ActionName, requirement.Scope, supersededAtUtc,
                        completedFamilyRequirements, exemptedFamilyRequirements, removedIndividualRoles,
                        activeAdults),
                RequirementMissingForIndividuals: FamilyRequirementMissingForIndividuals(roleName, requirement, supersededAtUtc,
                    removedIndividualRoles,
                    activeAdults)))
                .ToImmutableList();

            var simpleRequirementsMet = requirementsMet
                .Select(status => (status.ActionName, status.Stage, status.Scope, RequirementMetOrExempted: status.RequirementMetOrExempted.IsMetOrExempted, status.RequirementMissingForIndividuals))
                .ToImmutableList();

            var status = CalculateRoleApprovalStatusesFromRequirementCompletions(
                requirementsMet.Select(x => (x.ActionName, x.Stage, x.RequirementMetOrExempted)).ToImmutableList());
            var missingRequirements = CalculateMissingFamilyRequirementsFromRequirementCompletion(status.Status, simpleRequirementsMet);
            var availableApplications = CalculateAvailableFamilyApplicationsFromRequirementCompletion(status.Status, simpleRequirementsMet);
            var missingIndividualRequirements = CalculateMissingFamilyIndividualRequirementsFromRequirementCompletion(status.Status, simpleRequirementsMet);

            return (status.Status, status.ExpiresAtUtc,
                MissingRequirements: missingRequirements,
                AvailableApplications: availableApplications,
                MissingIndividualRequirements: missingIndividualRequirements);
        }

        internal static DateOnlyTimeline<RoleApprovalStatus> CalculateRoleApprovalStatusesFromRequirementCompletions(
            ImmutableList<(string ActionName, RequirementStage Stage, DateOnlyTimeline? RequirementApprovals)> requirementCompletionStatus)
        {
            // Instead of a single status and an expiration, return a set of *each* RoleApprovalStatus and DateOnlyTimeline?
            // so that the caller gets a full picture of the role's approval history.

            static DateOnlyTimeline? FindRangesWhereAllAreSatisfied(
                IEnumerable<(string ActionName, RequirementStage Stage, DateOnlyTimeline? RequirementApprovals)> values)
            {
                return DateOnlyTimeline.IntersectionOf(
                    values.Select(value => value.RequirementApprovals).ToImmutableList());
            }

            var onboarded = FindRangesWhereAllAreSatisfied(requirementCompletionStatus);

            var approvedOrOnboarded = FindRangesWhereAllAreSatisfied(requirementCompletionStatus
                .Where(x => x.Stage == RequirementStage.Application || x.Stage == RequirementStage.Approval));

            // Approved-only is the difference of approvedOrOnboarded and onboarded.
            var approvedOnly = approvedOrOnboarded?.Difference(onboarded);

            // Expired is a special case. It starts *after* any ranges from 'approvedOrOnboarded' (so it's the
            // forward-only complement of 'approvedOrOnboarded'), and ends at the end of time. If there are no
            // ranges from 'approvedOrOnboarded', then it is null.
            var expired = approvedOrOnboarded?.ForwardOnlyComplement();

            var prospectiveOrExpiredOrApprovedOrOnboarded = FindRangesWhereAllAreSatisfied(requirementCompletionStatus
                .Where(x => x.Stage == RequirementStage.Application));

            // Prospective-only is the difference of prospectiveOrExpiredOrApprovedOrOnboarded and approvedOrOnboarded,
            // subsequently also subtracting out 'expired'.
            var prospectiveOnly = prospectiveOrExpiredOrApprovedOrOnboarded
                ?.Difference(approvedOrOnboarded)
                ?.Difference(expired);

            // Merge the results (onboarded, approved, expired, prospective) into a tagged timeline.
            var taggedRanges = ImmutableList.Create(
                (RoleApprovalStatus.Onboarded, onboarded),
                (RoleApprovalStatus.Approved, approvedOnly),
                (RoleApprovalStatus.Expired, expired),
                (RoleApprovalStatus.Prospective, prospectiveOnly)
            ).SelectMany(x => x.Item2?.Ranges
                .Select(y => new DateRange<RoleApprovalStatus>(y.Start, y.End, x.Item1))
                ?? ImmutableList<DateRange<RoleApprovalStatus>>.Empty)
            .ToImmutableList();
            var result = new DateOnlyTimeline<RoleApprovalStatus>(taggedRanges);

            return result;
        }

        internal static ImmutableList<string> CalculateAvailableIndividualApplicationsFromRequirementCompletion(RoleApprovalStatus? status,
            ImmutableList<(string ActionName, RequirementStage Stage, bool RequirementMetOrExempted)> requirementCompletionStatus) =>
            status switch
            {
                RoleApprovalStatus.Onboarded => ImmutableList<string>.Empty,
                RoleApprovalStatus.Approved => ImmutableList<string>.Empty,
                RoleApprovalStatus.Prospective => ImmutableList<string>.Empty,
                null => requirementCompletionStatus
                    .Where(x => !x.RequirementMetOrExempted && x.Stage == RequirementStage.Application)
                    .Select(x => x.ActionName)
                    .ToImmutableList(),
                _ => throw new NotImplementedException(
                    $"The volunteer role approval status '{status}' has not been implemented.")
            };

        internal static ImmutableList<string> CalculateMissingIndividualRequirementsFromRequirementCompletion(RoleApprovalStatus? status,
            ImmutableList<(string ActionName, RequirementStage Stage, bool RequirementMetOrExempted)> requirementCompletionStatus) =>
            status switch
            {
                RoleApprovalStatus.Onboarded => ImmutableList<string>.Empty,
                RoleApprovalStatus.Approved => requirementCompletionStatus
                        .Where(x => !x.RequirementMetOrExempted && x.Stage == RequirementStage.Onboarding)
                        .Select(x => x.ActionName)
                        .ToImmutableList(),
                RoleApprovalStatus.Prospective => requirementCompletionStatus
                        .Where(x => !x.RequirementMetOrExempted && x.Stage == RequirementStage.Approval)
                        .Select(x => x.ActionName)
                        .ToImmutableList(),
                null => ImmutableList<string>.Empty,
                _ => throw new NotImplementedException(
                    $"The volunteer role approval status '{status}' has not been implemented.")
            };

        internal static ImmutableList<string> CalculateMissingFamilyRequirementsFromRequirementCompletion(RoleApprovalStatus? status,
            ImmutableList<(string ActionName, RequirementStage Stage, VolunteerFamilyRequirementScope Scope, bool RequirementMetOrExempted, List<Guid> RequirementMissingForIndividuals)> requirementsMet)
        {
            return status switch
            {
                RoleApprovalStatus.Onboarded => ImmutableList<string>.Empty,
                RoleApprovalStatus.Approved => requirementsMet
                        .Where(x => !x.RequirementMetOrExempted && x.Stage == RequirementStage.Onboarding
                            && x.Scope == VolunteerFamilyRequirementScope.OncePerFamily)
                        .Select(x => x.ActionName)
                        .ToImmutableList(),
                RoleApprovalStatus.Prospective => requirementsMet
                        .Where(x => !x.RequirementMetOrExempted && x.Stage == RequirementStage.Approval
                            && x.Scope == VolunteerFamilyRequirementScope.OncePerFamily)
                        .Select(x => x.ActionName)
                        .ToImmutableList(),
                null => ImmutableList<string>.Empty,
                _ => throw new NotImplementedException(
                    $"The volunteer role approval status '{status}' has not been implemented.")
            };
        }

        internal static ImmutableList<string> CalculateAvailableFamilyApplicationsFromRequirementCompletion(RoleApprovalStatus? status,
            ImmutableList<(string ActionName, RequirementStage Stage, VolunteerFamilyRequirementScope Scope, bool RequirementMetOrExempted, List<Guid> RequirementMissingForIndividuals)> requirementsMet)
        {
            return status switch
            {
                RoleApprovalStatus.Onboarded => ImmutableList<string>.Empty,
                RoleApprovalStatus.Approved => ImmutableList<string>.Empty,
                RoleApprovalStatus.Prospective => ImmutableList<string>.Empty,
                null => requirementsMet
                        .Where(x => !x.RequirementMetOrExempted && x.Stage == RequirementStage.Application
                            && x.Scope == VolunteerFamilyRequirementScope.OncePerFamily)
                        .Select(x => x.ActionName)
                        .ToImmutableList(),
                _ => throw new NotImplementedException(
                    $"The volunteer role approval status '{status}' has not been implemented.")
            };
        }

        internal static ImmutableDictionary<Guid, ImmutableList<string>> CalculateMissingFamilyIndividualRequirementsFromRequirementCompletion(RoleApprovalStatus? status,
            ImmutableList<(string ActionName, RequirementStage Stage, VolunteerFamilyRequirementScope Scope, bool RequirementMetOrExempted, List<Guid> RequirementMissingForIndividuals)> requirementsMet)
        {
            return status switch
            {
                RoleApprovalStatus.Onboarded => ImmutableDictionary<Guid, ImmutableList<string>>.Empty,
                RoleApprovalStatus.Approved => requirementsMet
                        .Where(x => x.Stage == RequirementStage.Onboarding)
                        .SelectMany(x => x.RequirementMissingForIndividuals.Select(y => (PersonId: y, x.ActionName)))
                        .GroupBy(x => x.PersonId)
                        .ToImmutableDictionary(x => x.Key, x => x.Select(y => y.ActionName).ToImmutableList()),
                RoleApprovalStatus.Prospective => requirementsMet
                        .Where(x => x.Stage == RequirementStage.Approval)
                        .SelectMany(x => x.RequirementMissingForIndividuals.Select(y => (PersonId: y, x.ActionName)))
                        .GroupBy(x => x.PersonId)
                        .ToImmutableDictionary(x => x.Key, x => x.Select(y => y.ActionName).ToImmutableList()),
                null => ImmutableDictionary<Guid, ImmutableList<string>>.Empty,
                _ => throw new NotImplementedException(
                    $"The volunteer role approval status '{status}' has not been implemented.")
            };
        }

        internal static SharedCalculations.RequirementCheckResult FamilyRequirementMetOrExempted(string roleName,
            string requirementActionName, VolunteerFamilyRequirementScope requirementScope,
            DateTime? supersededAtUtc,
            ImmutableList<CompletedRequirementInfo> completedFamilyRequirements,
            ImmutableList<ExemptedRequirementInfo> exemptedFamilyRequirements,
            ImmutableDictionary<Guid, ImmutableList<RemovedRole>> removedIndividualRoles,
            ImmutableList<(Guid Id, ImmutableList<CompletedRequirementInfo> CompletedRequirements,
                ImmutableList<ExemptedRequirementInfo> ExemptedRequirements)> activeAdults)
        {
            if (activeAdults.Count == 0)
                return new SharedCalculations.RequirementCheckResult(false, null);

            static SharedCalculations.RequirementCheckResult Combine(IEnumerable<SharedCalculations.RequirementCheckResult> values)
            {
                if (values.All(value => value.IsMetOrExempted))
                    return new SharedCalculations.RequirementCheckResult(true,
                        values.MinBy(value => value.ExpiresAtUtc ?? DateTime.MinValue)?.ExpiresAtUtc);
                else
                    return new SharedCalculations.RequirementCheckResult(false, null);
            }

            switch (requirementScope)
            {
                case VolunteerFamilyRequirementScope.AllAdultsInTheFamily:
                    {
                        var results = activeAdults
                            .Select(a => SharedCalculations.RequirementMetOrExempted(requirementActionName, supersededAtUtc,
                                a.CompletedRequirements, a.ExemptedRequirements));
                        return Combine(results);
                    }
                case VolunteerFamilyRequirementScope.AllParticipatingAdultsInTheFamily:
                    {
                        var results = activeAdults
                            .Where(a => !removedIndividualRoles.TryGetValue(a.Id, out var removedRoles) ||
                                removedRoles.All(x => x.RoleName != roleName))
                            .Select(a =>
                                SharedCalculations.RequirementMetOrExempted(requirementActionName, supersededAtUtc,
                                    a.CompletedRequirements, a.ExemptedRequirements));
                        return Combine(results);
                    }
                case VolunteerFamilyRequirementScope.OncePerFamily:
                    {
                        return SharedCalculations.RequirementMetOrExempted(requirementActionName, supersededAtUtc,
                            completedFamilyRequirements, exemptedFamilyRequirements);
                    }
                default:
                    throw new NotImplementedException(
                        $"The volunteer family requirement scope '{requirementScope}' has not been implemented.");
            }
        }

        internal static List<Guid> FamilyRequirementMissingForIndividuals(string roleName,
            VolunteerFamilyApprovalRequirement requirement,
            DateTime? supersededAtUtc,
            ImmutableDictionary<Guid, ImmutableList<RemovedRole>> removedIndividualRoles,
            ImmutableList<(Guid Id, ImmutableList<CompletedRequirementInfo> CompletedRequirements, ImmutableList<ExemptedRequirementInfo> ExemptedRequirements)> activeAdults) =>
            requirement.Scope switch
            {
                VolunteerFamilyRequirementScope.AllAdultsInTheFamily => activeAdults.Where(a =>
                    !SharedCalculations.RequirementMetOrExempted(requirement.ActionName, supersededAtUtc,
                        a.CompletedRequirements, a.ExemptedRequirements).IsMetOrExempted)
                    .Select(a => a.Id).ToList(),
                VolunteerFamilyRequirementScope.AllParticipatingAdultsInTheFamily => activeAdults.Where(a =>
                    !(removedIndividualRoles.TryGetValue(a.Id, out var removedRoles)
                        && removedRoles.Any(x => x.RoleName == roleName)) &&
                    !SharedCalculations.RequirementMetOrExempted(requirement.ActionName, supersededAtUtc,
                        a.CompletedRequirements, a.ExemptedRequirements).IsMetOrExempted)
                    .Select(a => a.Id).ToList(),
                VolunteerFamilyRequirementScope.OncePerFamily => new List<Guid>(),
                _ => throw new NotImplementedException(
                    $"The volunteer family requirement scope '{requirement.Scope}' has not been implemented.")
            };
    }
}
