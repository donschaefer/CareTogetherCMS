import { atom, selector } from "recoil";
import { accessTokenFetchQuery } from "../Authentication/AuthenticatedHttp";
import { CombinedFamilyInfo, Permission, UserLocationAccess, UsersClient } from "../GeneratedClient";
import { useLoadable } from "../Hooks/useLoadable";
import { localStorageEffect } from "../Utilities/localStorageEffect";
import { useFamilyLookup } from "./DirectoryModel";

export const usersClientQuery = selector({
  key: 'usersClient',
  get: ({get}) => {
    const accessTokenFetch = get(accessTokenFetchQuery);
    return new UsersClient(process.env.REACT_APP_API_HOST, accessTokenFetch);
  }
});

export const userIdState = atom<string | null>({
  key: 'userIdState'
});

export const userOrganizationAccessQuery = selector({
  key: 'userOrganizationAccessQuery',
  get: async ({get}) => {
    const usersClient = get(usersClientQuery);
    const userResponse = await usersClient.getUserOrganizationAccess();
    return userResponse;
  }
});

export const currentOrganizationIdQuery = selector({
  key: 'currentOrganizationQuery',
  get: ({get}) => {
    const userOrganizationAccess = get(userOrganizationAccessQuery);
    return userOrganizationAccess.organizationId!;
  }
});

export const currentOrganizationState = selector({//TODO: Deprecated
  key: 'COMPATIBILITY__currentOrganizationState',
  get: ({get}) => {
    const value = get(currentOrganizationIdQuery);
    return value ?? '';
  }
});

export const availableLocationsQuery = selector({
  key: 'availableLocationsQuery',
  get: ({get}) => {
    const userOrganizationAccess = get(userOrganizationAccessQuery);
    return userOrganizationAccess?.locations ?? null; //TODO: Fix unnecessary nulls
  }
});

export const availableLocationsState = selector({//TODO: Deprecated
  key: 'COMPATIBILITY__availableLocationsState',
  get: ({get}) => {
    const value = get(availableLocationsQuery);
    return value ?? [] as UserLocationAccess[];
  }
});

export const selectedLocationIdState = atom<string>({
  key: 'selectedLocationIdState',
  effects: [
    localStorageEffect('locationId'),
    // ({onSet}) => {
    //   onSet(newId => console.log("SEL_LOC_ID: " + newId))
    // }
  ]
})

export const currentLocationQuery = selector({
  key: 'currentLocationQuery',
  get: ({get}) => {
    const userOrganizationAccess = get(userOrganizationAccessQuery);
    const selectedLocationId = get(selectedLocationIdState);
    return userOrganizationAccess.locations!.find(location => location.locationId === selectedLocationId)!;
  }
});

export const currentLocationState = selector({//TODO: Deprecated
  key: 'COMPATIBILITY__currentLocationState',
  get: ({get}) => {
    const value = get(currentLocationQuery);
    return value.locationId!;
  }
});

function usePermissions(applicablePermissions?: Permission[]) {
  //TODO: If we want to expose a "not-yet-loaded" state, update this to return 'null' from
  //      the callback when 'applicablePermissions' is null (as opposed to undefined).
  return (permission: Permission) => (applicablePermissions || []).includes(permission);
}

export function useGlobalPermissions() {
  const currentLocation = useLoadable(currentLocationQuery);
  return usePermissions(currentLocation?.globalContextPermissions);
}

export function useAllPartneringFamiliesPermissions() {
  const currentLocation = useLoadable(currentLocationQuery);
  return usePermissions(currentLocation?.allPartneringFamiliesContextPermissions);
}

export function useAllVolunteerFamiliesPermissions() {
  const currentLocation = useLoadable(currentLocationQuery);
  return usePermissions(currentLocation?.allVolunteerFamiliesContextPermissions);
}

export function useFamilyIdPermissions(familyId: string) {
  const familyLookup = useFamilyLookup();
  const family = familyLookup(familyId);
  return usePermissions(family?.userPermissions);
}

export function useFamilyPermissions(family?: CombinedFamilyInfo) {
  return usePermissions(family?.userPermissions);
}
