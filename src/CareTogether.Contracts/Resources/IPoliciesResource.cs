﻿using JsonPolymorph;
using System;
using System.Collections.Immutable;
using System.Threading.Tasks;

namespace CareTogether.Resources
{
    public sealed record OrganizationConfiguration(string OrganizationName,
        ImmutableList<LocationConfiguration> Locations,
        ImmutableDictionary<Guid, UserAccessConfiguration> Users);

    public sealed record LocationConfiguration(Guid Id, string Name,
        ImmutableList<string> Ethnicities, ImmutableList<string> AdultFamilyRelationships);

    public sealed record UserAccessConfiguration(Guid PersonId,
        ImmutableList<UserLocationRole> LocationRoles);

    public sealed record UserLocationRole(Guid LocationId, string RoleName);

    public sealed record EffectiveLocationPolicy(
        ImmutableDictionary<string, ActionRequirement> ActionDefinitions,
        ReferralPolicy ReferralPolicy,
        VolunteerPolicy VolunteerPolicy);

    public sealed record ReferralPolicy(
        ImmutableList<string> RequiredIntakeActionNames,
        ImmutableList<ArrangementPolicy> ArrangementPolicies);
        //TODO: Include referral close reasons

    public sealed record ArrangementPolicy(string ArrangementType,
        ChildInvolvement ChildInvolvement,
        ImmutableList<VolunteerFunction> VolunteerFunctions,
        ImmutableList<string> RequiredSetupActionNames,
        //TODO: Include draft note approval policy
        ImmutableList<(string ActionName, RecurrencePolicy Recurrence)> RequiredMonitoringActionNames,
        ImmutableList<string> RequiredCloseoutActionNames);
    public enum ChildInvolvement { ChildHousing, DaytimeChildCareOnly, NoChildInvolvement };

    public enum FunctionRequirement { ZeroOrMore, ExactlyOne, OneOrMore };
    public sealed record VolunteerFunction(string ArrangementFunction, FunctionRequirement Requirement,
        ImmutableList<string> EligibleIndividualVolunteerRoles, ImmutableList<string> EligibleVolunteerFamilyRoles);

    public sealed record RecurrencePolicy(ImmutableList<RecurrencePolicyStage> Stages);
    public sealed record RecurrencePolicyStage(TimeSpan Delay, int? MaxOccurrences);


    public sealed record VolunteerPolicy(
        ImmutableDictionary<string, VolunteerRolePolicy> VolunteerRoles,
        ImmutableDictionary<string, VolunteerFamilyRolePolicy> VolunteerFamilyRoles);

    public sealed record VolunteerRolePolicy(string VolunteerRoleType, string CurrentVersion,
        ImmutableDictionary<string, ImmutableList<VolunteerApprovalRequirement>> ApprovalRequirementsByPolicyVersion);

    public sealed record VolunteerFamilyRolePolicy(string VolunteerFamilyRoleType, string CurrentVersion,
        ImmutableDictionary<string, ImmutableList<VolunteerFamilyApprovalRequirement>> ApprovalRequirementsByPolicyVersion);

    public sealed record VolunteerApprovalRequirement(
        RequirementStage Stage, string ActionName);

    public enum RequirementStage { Application, Approval, Onboarding }

    public sealed record VolunteerFamilyApprovalRequirement(
        RequirementStage Stage, string ActionName, VolunteerFamilyRequirementScope Scope);

    public enum VolunteerFamilyRequirementScope { OncePerFamily, AllAdultsInTheFamily };


    [JsonHierarchyBase]
    public abstract partial record ActionRequirement(); //TODO: Include the arrangement function (who can perform the action)
    public sealed record FormUploadRequirement(string FormName, string? Instructions, Uri? TemplateLink)
        : ActionRequirement;
    public sealed record ActivityRequirement(string ActivityName)
        : ActionRequirement;


    /// <summary>
    /// The <see cref="IPoliciesResource"/> is responsible for the configuration aspects of CareTogether.
    /// An organization can be comprised of multiple locations or chapters, each of which may implement
    /// policies that can inherit from the organization's base policies as well as global or regional policies.
    /// </summary>
    public interface IPoliciesResource
    {
        Task<OrganizationConfiguration> GetConfigurationAsync(Guid organizationId);

        Task<EffectiveLocationPolicy> GetCurrentPolicy(Guid organizationId, Guid locationId);
    }
}
