﻿using CareTogether.Resources;
using CareTogether.Resources.Policies;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Timelines;

namespace CareTogether.Engines.PolicyEvaluation
{
    internal static class SharedCalculations
    {
        public sealed record RequirementCheckResult(bool IsMetOrExempted, DateTime? ExpiresAtUtc);

        //NOTE: This is currently being used by Referral calculations.
        internal static RequirementCheckResult RequirementMetOrExempted(string requirementName,
            DateTime? policySupersededAtUtc, DateTime utcNow,
            ImmutableList<CompletedRequirementInfo> completedRequirements,
            ImmutableList<ExemptedRequirementInfo> exemptedRequirements)
        {
            var bestCompletion = completedRequirements
                .Where(completed =>
                    completed.RequirementName == requirementName &&
                    (policySupersededAtUtc == null || completed.CompletedAtUtc < policySupersededAtUtc) &&
                    (completed.ExpiresAtUtc == null || completed.ExpiresAtUtc > utcNow))
                .MaxBy(completed => completed.ExpiresAtUtc ?? DateTime.MaxValue);

            if (bestCompletion != null)
                return new RequirementCheckResult(true, bestCompletion.ExpiresAtUtc);

            var bestExemption = exemptedRequirements
                .Where(exempted =>
                    exempted.RequirementName == requirementName &&
                    (exempted.ExemptionExpiresAtUtc == null || exempted.ExemptionExpiresAtUtc > utcNow))
                .MaxBy(exempted => exempted.ExemptionExpiresAtUtc ?? DateTime.MaxValue);

            if (bestExemption != null)
                return new RequirementCheckResult(true, bestExemption.ExemptionExpiresAtUtc);

            return new RequirementCheckResult(false, null);
        }

        /// <summary>
        /// Given potentially multiple calculated role version approvals (due to
        /// having multiple policies or perhaps multiple ways that the approval
        /// was qualified for), merge the approval values to give a single
        /// effective approval value for the overall role.
        /// 
        /// The way this method is implemented here is to provide the most
        /// "positive" approval value possible at any given time. For example,
        /// if there is a Prospective approval in one role version and an Approved
        /// approval in another version of the same role, the result is Approved.
        /// </summary>
        internal static DateOnlyTimeline<RoleApprovalStatus>?
            CalculateEffectiveRoleApprovalStatus(
            ImmutableList<DateOnlyTimeline<RoleApprovalStatus>?> roleVersionApprovals)
        {
            if (roleVersionApprovals.Count == 0)
                return null;

            DateOnlyTimeline? AllRangesWith(
                RoleApprovalStatus value)
            {
                var matchingRanges = roleVersionApprovals
                    .SelectMany(rva => rva?.Ranges
                        .Where(range => range.Tag == value)
                        .Select(range => new DateRange(range.Start, range.End))
                        ?? ImmutableList<DateRange>.Empty)
                    .ToImmutableList();

                return DateOnlyTimeline.UnionOf(matchingRanges);
            }

            var allOnboarded = AllRangesWith(RoleApprovalStatus.Onboarded);
            var allApproved = AllRangesWith(RoleApprovalStatus.Approved);
            var allExpired = AllRangesWith(RoleApprovalStatus.Expired);
            var allProspective = AllRangesWith(RoleApprovalStatus.Prospective);

            // Now evaluate the impact of role approval status precedence.
            //TODO: Handle this logic generically via IComparable<T> as a
            //      method directly on DateOnlyTimeline<T>?
            var onboarded = allOnboarded;
            var approved = allApproved
                ?.Difference(onboarded);
            var expired = allExpired
                ?.Difference(approved)
                ?.Difference(onboarded);
            var prospective = allProspective
                ?.Difference(expired)
                ?.Difference(approved)
                ?.Difference(onboarded);

            // Merge the results (onboarded, approved, expired, prospective) into a tagged timeline.
            var taggedRanges = ImmutableList.Create(
                (RoleApprovalStatus.Onboarded, onboarded),
                (RoleApprovalStatus.Approved, approved),
                (RoleApprovalStatus.Expired, expired),
                (RoleApprovalStatus.Prospective, prospective)
            ).SelectMany(x => x.Item2?.Ranges
                .Select(y => new DateRange<RoleApprovalStatus>(y.Start, y.End, x.Item1))
                ?? ImmutableList<DateRange<RoleApprovalStatus>>.Empty)
            .ToImmutableList();

            var result = taggedRanges.Count > 0
                ? new DateOnlyTimeline<RoleApprovalStatus>(taggedRanges)
                : null;

            return result;
        }

        internal static DateOnlyTimeline<RoleApprovalStatus>?
            CalculateRoleVersionApprovalStatus(
            ImmutableList<(RequirementStage Stage, DateOnlyTimeline? WhenMet)>
                requirementCompletionStatus)
        {
            // Instead of a single status and an expiration, return a tagged timeline with
            // *every* date range for each effective RoleApprovalStatus, so that the
            // caller gets a full picture of the role's approval history.

            static DateOnlyTimeline? FindRangesWhereAllAreSatisfied(
                IEnumerable<(RequirementStage Stage, DateOnlyTimeline? WhenMet)> values)
            {
                return DateOnlyTimeline.IntersectionOf(
                    values.Select(value => value.WhenMet).ToImmutableList());
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

            var result = taggedRanges.Count > 0
                ? new DateOnlyTimeline<RoleApprovalStatus>(taggedRanges)
                : null;

            return result;
        }

        //NOTE: This is currently being used by Approval calculations.
        //      The two major differences are the removal of the utcNow parameter and the
        //      change of the return type to a Timeline. That allows returning all times when
        //      the requirement was met or exempted, not just the current one.
        //      The reason for this is to subsequently be able to determine if a role
        //      approval *was* met or exempted, even if it is now expired.
        //      A return value of 'null' indicates no approval.
        //      Further note: action validity was previously not being handled but now is.
        //TODO: Eventually this should be used for referral calculations as well!
        //      Maybe rename it to 'FindWhenRequirementIsMet' or something like that?
        internal static DateOnlyTimeline? FindRequirementApprovals(
            string requirementName, DateTime? policyVersionSupersededAtUtc,
            ImmutableList<CompletedRequirementInfo> completedRequirementsInScope,
            ImmutableList<ExemptedRequirementInfo> exemptedRequirementsInScope)
        {
            // Policy supersedence means that, as of the 'SupersededAtUtc' date, the policy version is no longer in effect.
            // As a result, while approvals granted under that policy version continue to be valid, any requirements that
            // were completed or exempted *on or after* that date cannot be taken into account for the purposes of determining
            // role approval status under this policy version.

            var matchingCompletions = completedRequirementsInScope
                .Where(completed => completed.RequirementName == requirementName &&
                    (policyVersionSupersededAtUtc == null || completed.CompletedAtUtc < policyVersionSupersededAtUtc))
                .Select(completed => new DateRange(
                    DateOnly.FromDateTime(completed.CompletedAtUtc),
                    completed.ExpiresAtUtc == null
                        ? DateOnly.MaxValue
                        : DateOnly.FromDateTime(completed.ExpiresAtUtc.Value)))
                .ToImmutableList();

            var matchingExemptions = exemptedRequirementsInScope
                .Where(exempted => exempted.RequirementName == requirementName)
                //TODO: Exemptions currently cannot be backdated, which may need to change in order to
                //      fully support handling policy exemptions correctly within the supersedence constraint.
                //      && (policyVersionSupersededAtUtc == null || exempted.TimestampUtc < policyVersionSupersededAtUtc))
                .Select(exempted => new DateRange(
                    //NOTE: This limits exemptions to being valid as of the time they were created.
                    //      If we want to allow backdating or postdating exemptions, we'll need to change this.
                    DateOnly.FromDateTime(exempted.TimestampUtc),
                    exempted.ExemptionExpiresAtUtc == null
                        ? DateOnly.MaxValue
                        : DateOnly.FromDateTime(exempted.ExemptionExpiresAtUtc.Value)))
                .ToImmutableList();

            return DateOnlyTimeline.UnionOf(matchingCompletions.Concat(matchingExemptions).ToImmutableList());
        }
    }
}
