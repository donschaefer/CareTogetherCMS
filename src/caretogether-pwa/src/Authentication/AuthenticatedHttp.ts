import { IPublicClientApplication } from "@azure/msal-browser";
import { atom, selector } from "recoil";
import { globalMsalInstance } from "./Auth";

export const accessTokenState = atom<string>({
  key: 'accessTokenState',
  effects: [
  //   ({onSet}) => {
  //     onSet(newToken => console.log("ACCESS_TOKEN: " + newToken?.substring(0,10)))
  //   }
  ]
});

const acquireAccessToken = async (msalInstance: IPublicClientApplication) => {
  const activeAccount = msalInstance.getActiveAccount();
  const accounts = msalInstance.getAllAccounts();

  if (!activeAccount && accounts.length === 0) {
    /*
    * User is not signed in. Throw error or wait for user to login.
    * Do not attempt to log a user in outside of the context of MsalProvider
    */
    throw new Error("User is not signed in.");
  }
  const request = {
    scopes: [process.env.REACT_APP_AUTH_SCOPES],
    account: activeAccount || accounts[0]
  };

  // This will throw an exception on failure.
  const authResult = await msalInstance.acquireTokenSilent(request);

  return authResult.accessToken
};

class AuthenticatedHttp {
  async fetch(url: RequestInfo, init?: RequestInit): Promise<Response> {
    const accessToken = await acquireAccessToken(globalMsalInstance);

    init && (init.headers = {
      ...init.headers,
      Authorization: `Bearer ${accessToken}`
    });
    return window.fetch(url, init);
  }
}

export const authenticatingFetch = new AuthenticatedHttp();

class AccessTokenHttp {
  constructor(private accessToken: string) { }
  async fetch(url: RequestInfo, init?: RequestInit): Promise<Response> {
    init && (init.headers = {
      ...init.headers,
      Authorization: `Bearer ${this.accessToken}`
    });
    return window.fetch(url, init);
  }
}

export const accessTokenFetchQuery = selector({
  key: 'accessTokenFetch',
  get: ({get}) => {
    const accessToken = get(accessTokenState);
    return new AccessTokenHttp(accessToken);
  }
})
