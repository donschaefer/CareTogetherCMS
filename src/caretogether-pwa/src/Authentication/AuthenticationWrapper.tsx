import React, { useEffect } from 'react';
import { EventType, InteractionType } from "@azure/msal-browser";
import { useMsalAuthentication, useIsAuthenticated, useAccount, useMsal } from '@azure/msal-react';
import { ProgressBackdrop } from '../Shell/ProgressBackdrop';
import { useSetRecoilState } from 'recoil';
import { accessTokenState } from './AuthenticatedHttp';
import { useScopedTrace } from '../Hooks/useScopedTrace';

function SignInScreen() {
  return (
    <ProgressBackdrop opaque>
      <p>Signing in...</p>
      <p>If you are not redirected within a few seconds,<br /> try refreshing this page.</p>
    </ProgressBackdrop>
  );
}

interface AuthenticationWrapperProps {
  children?: React.ReactNode
}
export default function AuthenticationWrapper({ children }: AuthenticationWrapperProps) {
  const trace = useScopedTrace("AuthenticationWrapper");
  trace("start");
  
  // Force the user to sign in if not already authenticated, then render the app.
  // See https://github.com/AzureAD/microsoft-authentication-library-for-js/blob/dev/lib/msal-react/docs/hooks.md
  //TODO: Handle token/session expiration to intercept the automatic redirect and prompt the user first?
  //TODO: Smoother handling of deeplink routing (integrating with React Router)?
  useMsalAuthentication(InteractionType.Redirect, {
    scopes: [process.env.REACT_APP_AUTH_SCOPES]
  });
  const isAuthenticated = useIsAuthenticated();
  const defaultAccount = useAccount();
  const { instance } = useMsal();
  const setAccessToken = useSetRecoilState(accessTokenState);
  trace(`isAuthenticated: ${isAuthenticated} -- defaultAccount: ${defaultAccount?.localAccountId}`);

  // Before rendering any child components, ensure that the user is authenticated and
  // that the default account is set correctly in MSAL.
  useEffect(() => {
    const accounts = instance.getAllAccounts();
    const accountToActivate = accounts.length > 0 ? accounts[0] : null;
    trace(`setActiveAccount: ${accountToActivate?.localAccountId}`);
    instance.setActiveAccount(accountToActivate);
  }, [ instance, isAuthenticated, trace ]);

  // Track the most recently acquired access token as shared state for API clients to reference.
  useEffect(() => {
    const callbackId = instance.addEventCallback((event: any) => {
      trace(`event: ${event?.eventType}`);
      if (event.eventType === EventType.LOGIN_SUCCESS) {
        instance.setActiveAccount(event.payload.account);
      }
      if (event.eventType === EventType.ACQUIRE_TOKEN_SUCCESS ||
        event.eventType === EventType.LOGIN_SUCCESS ||
        event.eventType === EventType.SSO_SILENT_SUCCESS) {
        const accessToken = event.payload.accessToken as string;
        setAccessToken(accessToken);
      }
    });
    trace(`addEventCallback: ${callbackId}`);

    return () => {
      if (callbackId) {
        instance.removeEventCallback(callbackId);
      }
    }
  }, [ instance, setAccessToken, trace ]);

  trace("render");
  return (
    <>
      {isAuthenticated && defaultAccount
        ? children
        //TODO: Handle account selection when multiple accounts are signed in
        : <SignInScreen />}
    </>
  );
}
