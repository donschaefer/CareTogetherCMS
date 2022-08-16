import { selector } from "recoil";
import { ConfigurationClient, RequirementStage, VolunteerFamilyRequirementScope } from "../GeneratedClient";
import { accessTokenFetchQuery } from "../Authentication/AuthenticatedHttp";
import { currentLocationState, currentOrganizationIdQuery, currentOrganizationState, selectedLocationIdState } from "./SessionModel";
import { useLoadable } from "../Hooks/useLoadable";

export const organizationConfigurationQuery = selector({
  key: 'organizationConfigurationQuery',
  get: async ({get}) => {
    const organizationId = get(currentOrganizationIdQuery);
    const accessTokenFetch = get(accessTokenFetchQuery);
    const configurationClient = new ConfigurationClient(process.env.REACT_APP_API_HOST, accessTokenFetch);
    const dataResponse = await configurationClient.getOrganizationConfiguration(organizationId);
    return dataResponse;
  }});

export const organizationConfigurationData = selector({//TODO: Deprecated
  key: 'COMPATIBILITY__organizationConfigurationData',
  get: async ({get}) => {
    const organizationConfiguration = get(organizationConfigurationQuery);
    return organizationConfiguration;
  }});

export const organizationNameQuery = selector({
  key: 'organizationNameQuery',
  get: ({get}) => {
    const organizationConfiguration = get(organizationConfigurationQuery);
    return organizationConfiguration.organizationName!;
  }
})

export const locationConfigurationQuery = selector({
  key: 'locationConfigurationQuery',
  get: ({get}) => {
    const organizationConfiguration = get(organizationConfigurationQuery);
    const selectedLocation = get(selectedLocationIdState);
    return organizationConfiguration.locations!.find(x => x.id === selectedLocation)!;
  }
});

export const locationNameQuery = selector({
  key: 'locationNameQuery',
  get: ({get}) => {
    const locationConfiguration = get(locationConfigurationQuery);
    return locationConfiguration.name!;
  }
})

export const ethnicitiesData = selector({//TODO: Rename to 'query'
  key: 'COMPATIBILITY__ethnicitiesData',
  get: ({get}) => {
    const locationConfiguration = get(locationConfigurationQuery);
    return locationConfiguration.ethnicities!;
  }
})

export const adultFamilyRelationshipsData = selector({//TODO: Rename to 'query'
  key: 'COMPATIBILITY__adultFamilyRelationshipsData',
  get: ({get}) => {
    const locationConfiguration = get(locationConfigurationQuery);
    return locationConfiguration.adultFamilyRelationships!;
  }
})

export const policyData = selector({
  key: 'policyData',
  get: async ({get}) => {
    const organizationId = get(currentOrganizationState);
    const locationId = get(currentLocationState);
    const accessTokenFetch = get(accessTokenFetchQuery);
    const configurationClient = new ConfigurationClient(process.env.REACT_APP_API_HOST, accessTokenFetch);
    const dataResponse = await configurationClient.getEffectiveLocationPolicy(organizationId, locationId);
    return dataResponse;
  }});

export const allApprovalAndOnboardingRequirementsData = selector({
  key: 'allApprovalAndOnboardingRequirementsData',
  get: ({get}) => {
    const policy = get(policyData);
    const sortedActionNames = (policy.actionDefinitions && Object.entries(policy.actionDefinitions)
      .map(([actionName,]) => actionName)
      .sort((a, b) => a < b ? -1 : a > b ? 1 : 0)) || [];
    return sortedActionNames.filter(actionName =>
      (policy.volunteerPolicy?.volunteerFamilyRoles && Object.entries(policy.volunteerPolicy.volunteerFamilyRoles).some(([role, rolePolicy]) =>
        rolePolicy.policyVersions && Object.entries(rolePolicy.policyVersions).some(([version, rolePolicyVersion]) =>
          rolePolicyVersion.requirements && rolePolicyVersion.requirements.some(requirement =>
            requirement.actionName === actionName && requirement.stage !== RequirementStage.Application)))) ||
      (policy.volunteerPolicy?.volunteerRoles && Object.entries(policy.volunteerPolicy.volunteerRoles).some(([role, rolePolicy]) =>
        rolePolicy.policyVersions && Object.entries(rolePolicy.policyVersions).some(([version, rolePolicyVersion]) =>
          rolePolicyVersion.requirements && rolePolicyVersion.requirements.some(requirement =>
            requirement.actionName === actionName && requirement.stage !== RequirementStage.Application)))));
  }
});

export const familyRequirementsData = selector({
  key: 'familyRequirementsData',
  get: ({get}) => {
    const policy = get(policyData);
    return (policy.volunteerPolicy?.volunteerFamilyRoles &&
      Object.entries(policy.volunteerPolicy.volunteerFamilyRoles)
        .reduce((previous, [, familyRolePolicy]) => {
          const requirements = (familyRolePolicy.policyVersions?.map(policyVersion => (policyVersion.requirements
            ?.filter(requirement => requirement.scope === VolunteerFamilyRequirementScope.OncePerFamily)
            ?.map(requirement => requirement.actionName!) || []))) || [];
          return previous.concat(requirements.flat());
        }, [] as string[])
        .reduce((previous, familyApprovalRequirement) => {
          return previous.filter(x => x === familyApprovalRequirement).length > 0
            ? previous
            : previous.concat(familyApprovalRequirement);
        }, [] as string[])
        .sort((a, b) => a < b ? -1 : a > b ? 1 : 0)) || [];
  }});

export const adultRequirementsData = selector({
  key: 'adultRequirementsData',
  get: ({get}) => {
    const policy = get(policyData);
    const familyAllAdultRequirements = (policy.volunteerPolicy?.volunteerFamilyRoles &&
      Object.entries(policy.volunteerPolicy.volunteerFamilyRoles)
        .reduce((previous, [, familyRolePolicy]) => {
          const requirements = (familyRolePolicy.policyVersions?.map(policyVersion => (policyVersion.requirements
            ?.filter(requirement => requirement.scope === VolunteerFamilyRequirementScope.AllAdultsInTheFamily)
            ?.map(requirement => requirement.actionName!) || []))) || [];
          return previous.concat(requirements.flat());
        }, [] as string[])) || [];
    const individualRequirements = (policy.volunteerPolicy?.volunteerRoles &&
      Object.entries(policy.volunteerPolicy.volunteerRoles)
        .reduce((previous, [, rolePolicy]) => {
          const requirements = (rolePolicy.policyVersions?.map(policyVersion => (policyVersion.requirements
            ?.map(requirement => requirement.actionName!) || []))) || [];
          return previous.concat(requirements.flat());
        }, [] as string[])) || [];
    return familyAllAdultRequirements.concat(individualRequirements)
        .reduce((previous, individualApprovalRequirement) => {
          return previous.filter(x => x === individualApprovalRequirement).length > 0
            ? previous
            : previous.concat(individualApprovalRequirement);
        }, [] as string[])
        .sort((a, b) => a < b ? -1 : a > b ? 1 : 0);
  }});

export const featureFlagQuery = selector({
  key: 'featureFlagQuery',
  get: async ({get}) => {
    const organizationId = get(currentOrganizationIdQuery);
    const locationId = get(selectedLocationIdState);
    const accessTokenFetch = get(accessTokenFetchQuery);
    const configurationClient = new ConfigurationClient(process.env.REACT_APP_API_HOST, accessTokenFetch);
    const dataResponse = await configurationClient.getLocationFlags(organizationId, locationId);
    return dataResponse;
  }});

export function useFeatureFlags() {
  return useLoadable(featureFlagQuery);
}
