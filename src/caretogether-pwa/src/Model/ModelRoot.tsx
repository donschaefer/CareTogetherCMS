import { useAccount } from "@azure/msal-react";
import { useEffect } from "react";
import { useRecoilStateLoadable, useSetRecoilState } from "recoil";
import { useLoadable } from "../Hooks/useLoadable";
import { useScopedTrace } from "../Hooks/useScopedTrace";
import { visibleFamiliesData, visibleFamiliesInitializationQuery } from "./DirectoryModel";
import { userIdState, selectedLocationIdState, availableLocationsQuery } from "./SessionModel";

interface ModelLoaderProps {
  children?: React.ReactNode
}

export function ModelRoot({children}: ModelLoaderProps) {
  const trace = useScopedTrace("ModelRoot");
  trace("start");
  
  const activeAccount = useAccount();
  trace(`activeAccount: ${activeAccount?.localAccountId}`);
  const setUserId = useSetRecoilState(userIdState);
  const availableLocations = useLoadable(availableLocationsQuery);
  trace(`availableLocations.length: ${availableLocations?.length}`);
  const [selectedLocationId, setSelectedLocationId] = useRecoilStateLoadable(selectedLocationIdState);
  trace(`selectedLocationId: ${selectedLocationId.state} -- ${selectedLocationId.contents}`);
  const visibleFamilies = useLoadable(visibleFamiliesInitializationQuery);
  trace(`visibleFamilies.length: ${visibleFamilies?.length}`);
  const setVisibleFamiliesData = useSetRecoilState(visibleFamiliesData);
  
  // Initialize the root of the model's dataflow graph with the active account's user ID.
  // If the active account is changed, the model will automatically repopulate.
  useEffect(() => {
    const value = activeAccount?.localAccountId ?? null;
    trace(`setUserId: ${value}`);
    setUserId(value);
  }, [activeAccount, setUserId, trace]);

  // Mark the correct location as the currently selected one.
  // This will be the most recently selected location, or the first available location
  // if none was previously saved or the saved location is no longer available.
  useEffect(() => {
    const selectedLocation =
      availableLocations == null
      ? null
      : (selectedLocationId.state === 'hasValue' &&
          availableLocations.some(loc => loc.locationId === selectedLocationId.contents))  
        ? availableLocations.find(loc => loc.locationId === selectedLocationId.contents) || null
        : availableLocations[0];
    const locationIdToSelect = selectedLocation?.locationId || null;
    trace(`locationIdToSelect: ${locationIdToSelect}`);
    if (locationIdToSelect) {
      trace(`Setting selected location ID: ${locationIdToSelect}`);
      setSelectedLocationId(locationIdToSelect);
    }
  }, [availableLocations, selectedLocationId, setSelectedLocationId, trace]);

  // Initialize the families atom that will be used to track family state mutations.
  //TODO: Trigger a refresh when changing locations.
  useEffect(() => {
    trace(`setVisibleFamiliesData: ${visibleFamilies?.length}`)
    setVisibleFamiliesData(visibleFamilies || []);
  }, [visibleFamilies, setVisibleFamiliesData, trace]);

  trace("render");
  return (
    <>
      {children}
    </>
  );
}
